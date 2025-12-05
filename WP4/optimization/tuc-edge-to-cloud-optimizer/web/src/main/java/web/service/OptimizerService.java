package web.service;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import core.exception.OptimizerException;
import core.graph.ChainPayload;
import core.graph.Graphs;
import core.graph.ThreadSafeDAG;
import core.graph.Vertex;
import core.parser.dictionary.Dictionary;
import core.parser.network.Network;
import core.parser.workflow.Operator;
import core.parser.workflow.OptimizationParameters;
import core.parser.workflow.OptimizationRequest;
import core.structs.BoundedPriorityQueue;
import core.structs.Tuple;
import core.utils.FileUtils;
import core.utils.GraphUtils;
import lombok.Getter;
import lombok.extern.java.Log;
import optimizer.OptimizationRequestStatisticsBundle;
import optimizer.OptimizationResourcesBundle;
import optimizer.algorithm.*;
import optimizer.algorithm.flowoptimizer.FlowOptimizer;
import optimizer.cost.CostEstimator;
import optimizer.cost.DAGStarCostEstimator;
import optimizer.cost.SimpleCostEstimator;
import optimizer.plan.OptimizationPlan;
import optimizer.plan.SimpleOptimizationPlan;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.web.client.RestTemplateBuilder;
import org.springframework.data.rest.webmvc.ResourceNotFoundException;
import org.springframework.http.*;
import org.springframework.http.client.HttpComponentsClientHttpRequestFactory;
import org.springframework.messaging.MessagingException;
import org.springframework.messaging.simp.SimpMessagingTemplate;
import org.springframework.scheduling.annotation.Async;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.DefaultResponseErrorHandler;
import org.springframework.web.client.RestTemplate;
import web.configuration.WebSocketConfig;
import web.document.OptimizationRequestDocument;
import web.document.OptimizerResponseDocument;
import web.repository.*;

import javax.annotation.PostConstruct;
import javax.annotation.concurrent.ThreadSafe;
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.security.Principal;
import java.time.Duration;
import java.time.Instant;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.stream.Collectors;

@Service
@Log
public class OptimizerService {
    @Value("#{systemEnvironment['ATHENA_BO_URL']}")
    private String BO_URL;

    @Value("#{systemEnvironment['OPTIMIZER_USER']}")
    private String OPTIMIZER_USER;

    @Value("#{systemEnvironment['OPTIMIZER_PASS']}")
    private String OPTIMIZER_PASS;

    @Value("#{systemEnvironment['ATHENA_OPTIMIZER_OVERRIDE_OPT_ALGO_TO_AUTO']}")
    private boolean OVERRIDE_OPT_ALGO_TO_AUTO;

    @Value("#{systemEnvironment['ATHENA_OPTIMIZER_REQUEST_TIMEOUT'] ?: 300000}")
    private int OPTIMIZATION_REQUEST_TIMEOUT;

    @Value("#{systemEnvironment['ATHENA_OPTIMIZER_CONSECUTIVE_CONTINUOUS_OPTIMIZATION_REQUESTS_DELAY_MS'] ?: 0}")
    private int CONSECUTIVE_CONTINUOUS_OPTIMIZATION_REQUESTS_DELAY_MS;

    @Value("#{systemEnvironment['ATHENA_OPTIMIZER_CONTINUOUS_OPTIMIZATION_ATTEMPTS_LIMIT'] ?: 10}")
    private int CONTINUOUS_OPTIMIZATION_ATTEMPTS_LIMIT;

    @Autowired
    private RestTemplate restTemplate;

    @Autowired
    private ExecutorProviderService executorProviderService;

    @Autowired
    private StatisticsRetrievalService statService;

    @Autowired
    private DictionaryRepository dictionaryRepository;

    @Autowired
    private NetworkRepository networkRepository;

    @Autowired
    private OptimizerRequestRepository optimizerRequestRepository;

    @Autowired
    private OptimizerResultRepository optimizerResultRepository;

    @Autowired
    private SimpMessagingTemplate simpTemplate;

    @Autowired
    @Qualifier("optimizationRequestHandlerExecutor1")
    private ExecutorService requestExecutorService;

    //Local context
    private Gson gson;
    private Random random;
    private Map<OptimizationRequestContext, Future<OptimizationResourcesBundle>> pendingRequests;
    private Map<String, AtomicInteger> requestSubmissionCount;

    @PostConstruct
    private void init() {
        this.gson = new GsonBuilder().create();
        this.random = new Random(0);
        this.pendingRequests = new ConcurrentHashMap<>();
        this.requestSubmissionCount = new ConcurrentHashMap<>();
        this.restTemplate = new RestTemplateBuilder()
                .basicAuthentication(OPTIMIZER_USER, OPTIMIZER_PASS)
                .errorHandler(new DefaultResponseErrorHandler())
                .requestFactory(() -> {
                    HttpComponentsClientHttpRequestFactory f = new HttpComponentsClientHttpRequestFactory();
                    f.setBufferRequestBody(false);  //Recommended for large files.
                    return f;
                })
                .build();
    }

    //Asynchronously handle the optimization process of a request with the given ID
    @Async("submitRequestWithIdAsyncTaskExecutor1")
    public void submitRequestWithId(Principal principal, String requestId) {
        log.info(String.format("Optimization process STARTED for user=[%s] and requestID=[%s]", principal.getName(), requestId));
        simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "STARTED OPTIMIZATION", requestId));

        //Create context
        final ExecutorService requestExecutorService = this.executorProviderService.createOptimizationTaskExecutor1();
        final OptimizationRequestContext requestContext = new OptimizationRequestContext(principal, requestId, requestExecutorService);

        //Create the request and tag it as pending
        final Future<OptimizationResourcesBundle> future = this.requestExecutorService.submit(requestContext);
        this.pendingRequests.put(requestContext, future);

        this.requestSubmissionCount.putIfAbsent(requestId, new AtomicInteger(0));

        try {
            log.info(String.format("RequestID:%s",requestId));
            //Submit the task and block
            OptimizationResourcesBundle bundle = future.get(OPTIMIZATION_REQUEST_TIMEOUT, TimeUnit.MILLISECONDS);

            if (bundle == null) {
                log.info("OptimizationRequestBundle is null");
                return;
            }

            log.info(Arrays.toString(OptimizationRequestStatisticsBundle.getCSVHeaders()));
            log.info(Arrays.toString(bundle.getStatisticsBundle().toCSVEntry()));

            //Check for result plans
            try {
                //Retrieve the result
                simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "COMPLETED!", requestId));
                OptimizerResponseDocument result = new OptimizerResponseDocument(bundle);

                //Retrieve the Optimizer`s response
                simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "SAVING TO ELASTIC", requestId));
                OptimizerResponseDocument savedOptResponse = optimizerResultRepository.save(result);
                this.requestSubmissionCount.get(requestId).incrementAndGet();
                String optimizedWorkflow = gson.toJson(savedOptResponse);
                //String optimizedWorkflow = gson.toJson(result);log.info(String.format(
                log.info(String.format("***** Optimized workflow ***//Send the optimized workflow to the user**: %s ", optimizedWorkflow)); //Xenia

                //Send the optimized workflow to the user
                try {
                    this.simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "SENDING RESULTS BACK", requestId));
                    this.simpTemplate.convertAndSendToUser(principal.getName(), "/queue/optimization_results", optimizedWorkflow);
                } catch (MessagingException e) {
                    log.severe(String.format("Failed to send optimization results back to user for ID=[%s] due to [%s].", bundle.getRequestId(), e.getMessage()));
                }
                log.config(String.format("Sent optimization results back to user for ID=[%s].", bundle.getRequestId()));
                int sizeInBytes = optimizedWorkflow.getBytes(StandardCharsets.UTF_8).length;
                log.info("Message size of the optimized workflow: " + sizeInBytes);

                //simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "OPTIMIZATION_COMPLETED", requestId)); //Xenia
                //log.config(String.format("Optimization COMPLETED. ID=[%s].", bundle.getRequestId())); //Xenia

            } catch (OptimizerException e) {
                simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "TIME_OUT", requestId));
                log.config(String.format("Optimization did not produce any plans. ID=[%s].", bundle.getRequestId()));
            }
        } catch (InterruptedException | TimeoutException | CancellationException e) {
            log.info(String.format("Time-out for requestID=[%s], actual exception is [%s].", requestId, e));
        } catch (Exception e) {
            //Uncaught exceptions are logged as sever
            e.printStackTrace(); //Xenia
            String errorMsg = e.fillInStackTrace().toString(); //Xenia
            //String errorMsg = e.getMessage(); //Original
            simpTemplate.convertAndSendToUser(principal.getName(), "/queue/errors", String.format("{\"id\": \"%s\",\"cause\":\"%s\"}", requestId, errorMsg));
            log.severe(String.format("Error for requestID=[%s] is [%s]", requestId, errorMsg));
        } finally {
            //Shutdown the executor service
            shutdownAndAwaitTermination(requestContext.getRequestExecutorService());

            //Remove completed task from the pending requests map
            this.pendingRequests.remove(requestContext);
            log.config(String.format("Removed container [%s]", requestContext));
        }

        //Re-submit if the request is a continuous optimization query and hasn't hit the optimization request limit
        if (requestContext.isContinuous() && this.requestSubmissionCount.get(requestId).get() < CONTINUOUS_OPTIMIZATION_ATTEMPTS_LIMIT) {
            log.info(String.format("Request with ID [%s] is continuous so it's resubmitted.", requestId));
            //Delay the submission of another query
            try {
                Thread.sleep(CONSECUTIVE_CONTINUOUS_OPTIMIZATION_REQUESTS_DELAY_MS);
            } catch (Exception ignored) {
            }
            submitRequestWithId(principal, requestId);
        } else {
            simpTemplate.convertAndSendToUser(principal.getName(), "/queue/info", String.format("{\"action\":\"%s\",\"id\": \"%s\"}", "OPTIMIZATION_COMPLETED", requestId)); //Xenia
            log.info(String.format("Request with ID [%s] COMPLETED!", requestId));
        }
    }

    //Cancel a pending request
    public boolean cancelRequestWithId(String requestId) {
        boolean result = this.pendingRequests.entrySet().stream()
                .filter(entry -> entry.getKey().getRequestId().equals(requestId))
                .findFirst()
                .map(Map.Entry::getValue)
                .map(future -> future.cancel(true))
                .orElse(false);
        log.info(String.format("Optimization process CANCELED for requestID=[%s] with result=[%s]", requestId, result));
        return result;
    }

    //This is the standard way of gracefully shutting down an executor service
    private void shutdownAndAwaitTermination(ExecutorService pool) {
        pool.shutdown(); // Disable new tasks from being submitted
        try {
            // Wait a while for existing tasks to terminate
            if (!pool.awaitTermination(0, TimeUnit.SECONDS)) {
                pool.shutdownNow(); // Cancel currently executing tasks
                // Wait a while for tasks to respond to being cancelled
                if (!pool.awaitTermination(5, TimeUnit.SECONDS)) {
                    log.severe("Pool did not terminate");
                }
            }
        } catch (InterruptedException ie) {
            // (Re-)Cancel if current thread also interrupted
            pool.shutdownNow();
            // Preserve interrupt status
            Thread.currentThread().interrupt();
        }
    }

    public Dictionary adjustDictionaryWithBO(String payload) {
        //Send message
        log.config(String.format("Sent dict to BO. [%s]", payload.replace("\n", "")));

        HttpHeaders headers = new HttpHeaders();
        headers.setContentType(MediaType.MULTIPART_FORM_DATA);
        headers.setCacheControl(CacheControl.noCache());

        MultiValueMap<String, Object> body = new LinkedMultiValueMap<>();
        body.add("dictionary", payload);
        HttpEntity<MultiValueMap<String, Object>> requestEntity = new HttpEntity<>(body, headers);

        ResponseEntity<String> response = restTemplate.exchange(String.format("http://%s/dictionary", BO_URL), HttpMethod.POST, requestEntity, String.class);

        //Block and wait for response
        String responseMessage = response.getBody();
        log.config(String.format("Got dict from BO: [%s]", responseMessage.replace("\n", "")));

        //Parse to Dictionary
        return new Dictionary(responseMessage);
    }

    @Scheduled(fixedRate = 10000)
    protected void updates() {
        for (Map.Entry<OptimizationRequestContext, Future<OptimizationResourcesBundle>> entry : this.pendingRequests.entrySet()) {
            log.info(String.format("Request: %s -> %s", entry.getKey().getRequestId(), entry.getValue().toString()));
        }
    }

    @Getter
    private class OptimizationRequestContext implements Callable<OptimizationResourcesBundle> {
        private final Principal principal;
        private final String requestId;
        private final ExecutorService requestExecutorService;
        private final Instant submittedAt;
        private boolean isContinuous;

        public OptimizationRequestContext(Principal principal, String requestId, ExecutorService requestExecutorService) {
            this.principal = principal;
            this.requestId = requestId;
            this.requestExecutorService = requestExecutorService;
            this.submittedAt = Instant.now();
            this.isContinuous = false;
        }

        @Override
        public boolean equals(Object o) {
            if (this == o) return true;
            if (o == null || getClass() != o.getClass()) return false;
            OptimizationRequestContext container = (OptimizationRequestContext) o;
            return principal.equals(container.principal) && requestId.equals(container.requestId) && submittedAt.equals(container.submittedAt);
        }

        @Override
        public int hashCode() {
            return Objects.hash(principal, requestId, submittedAt);
        }

        @Override
        public OptimizationResourcesBundle call() {
            //Keep track of all optimization phases
            final Instant startInstant = Instant.now();

            //Retrieve request from elasticsearch based on provided requestID
            OptimizationRequestDocument optReqDoc = optimizerRequestRepository.findById(requestId).orElseThrow(() -> {
                throw new ResourceNotFoundException(String.format("Request ID [%s] not found.", requestId));
            });

            //Parse the Optimization request
            final OptimizationRequest request = gson.fromJson(optReqDoc.getRequest(), OptimizationRequest.class);
            final OptimizationParameters optParams = request.getOptimizationParameters();

            //Gather the optimizer resources
            final Network network = networkRepository.findById(optParams.getNetworkName())
                    .map(RepositoryDocument::getObject)
                    .orElseThrow(() -> new ResourceNotFoundException("Network not found"));

            final Dictionary dictionary = dictionaryRepository.findById(optParams.getDictionaryName())
                    .map(RepositoryDocument::getObject)
                    .map(Dictionary -> {
                        switch (optParams.getCost_model() == null ? "model1" : optParams.getCost_model().toLowerCase()) {
                            case "model1":
                                return statService.adjustDictionaryWithModel1(Dictionary);
                            case "bo":
                                return adjustDictionaryWithBO(Dictionary.getOriginalInput());
                            default:
                                throw new IllegalStateException("Unknown cost model.");
                        }
                    })
                    .orElseThrow(() -> new ResourceNotFoundException("Dictionary not found"));

            //Comparators
            final Comparator<OptimizationPlan> costFormula = Comparator.comparingInt(o -> -o.totalCost());

            //Find an algorithm matching the given name
            final GraphTraversalAlgorithm gta;
            final String algorithmName = OVERRIDE_OPT_ALGO_TO_AUTO ? "auto" : optParams.getAlgorithm();
            if (algorithmName == null) {
                throw new IllegalStateException("No optimization algorithm provided.");
            }
            int numOfPlans = optParams.getNumOfPlans();
            final BoundedPriorityQueue<OptimizationPlan> validPlansQueue;
            switch (algorithmName) {
                case "auto":
                case "op-GS":
                    gta = new GreedySearchAlgorithm();
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, false);
                    break;
                case "op-ES":
                    gta = new ExhaustiveSearchAlgorithm();
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, false);
                    break;
                case "op-A*":
                    gta = new AStarSearchAlgorithm(AStarSearchAlgorithm.AggregationStrategy.MAX, false);
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, false);
                    break;
                case "op-HS":
                    gta = new HeuristicSearchAlgorithm();
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, false);
                    break;
                case "p-ES":
                    gta = new ParallelExhaustiveSearchAlgorithm();
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, true);
                    break;
                case "p-GS":
                    gta = new ParallelGreedySearchAlgorithm();
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, true);
                    break;
                case "p-HS":
                    gta = new ParallelHeuristicSearchAlgorithm();
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, numOfPlans, true);
                    break;
                case "e-gsp":
                case "e-esq":
                case "e-gsg":
                case "e-qp":
                case "e-escp":
                case "e-esc":
                    gta = new FlowOptimizer(algorithmName);
                    validPlansQueue = new BoundedPriorityQueue<>(costFormula, 1, true);
                    break;
                default:
                    throw new IllegalStateException("Supported algorithms are: [op-ES,op-A*,op-GS,p-ES,p-GS,p-HS]");
            }

            //Cost estimator
            final Map<String, String> opNamesToClassKeysMap = FileUtils.getOpNameToClassKeyMapping(request);
            final ThreadSafeDAG<Operator> operatorGraph = FileUtils.getOperatorGraph(request);
            final ThreadSafeDAG<ChainPayload<Operator>> contractedGraph = Graphs.contractChains(operatorGraph);
            Set<Vertex<ChainPayload<Operator>>> superVertices = contractedGraph.getVertices();
            // SUPER VERTICES
            System.out.println("Super-vertices: ");
            for (Vertex<ChainPayload<Operator>> superVertex : superVertices) {
                List<Vertex<Operator>> members = superVertex.getData().getMembers();
                String membersStr = members.stream().map(v -> v.getData().getName()).collect(Collectors.joining(","));
                System.out.println("Super-vertex -> " + membersStr);
            }

            final Map<String, Set<String>> operatorParents = GraphUtils.getOperatorParentMap(operatorGraph);
            CostEstimator costEstimator = new SimpleCostEstimator(operatorParents, dictionary, opNamesToClassKeysMap);

            if (algorithmName.equals("op-A*")) {
                costEstimator = new DAGStarCostEstimator(dictionary, opNamesToClassKeysMap);
                log.info("Initializing DAG* cost estimator with dictionary " + dictionary.getName());
            }

            //Root plan seeds
            final List<Integer> platformSeeds = random
                    .ints(0, network.getPlatforms().size())
                    .limit(operatorGraph.getVertices().size())
                    .boxed()
                    .collect(Collectors.toList());
            final List<Integer> siteSeeds = random
                    .ints(0, network.getSites().size())
                    .limit(operatorGraph.getVertices().size())
                    .boxed()
                    .collect(Collectors.toList());


            LinkedHashMap<String, Map<String, List<String>>> impls = FileUtils
                    .getOperatorImplementations(contractedGraph, network, dictionary, opNamesToClassKeysMap);
            System.out.println("Implementations for each super-vertex: ");
            for (Map.Entry<String, Map<String, List<String>>> entry : impls.entrySet()) {
                String superVertexId = entry.getKey();
                System.out.println("For super-vertex: " + superVertexId);
                for (Map.Entry<String, List<String>> sitePlatform : entry.getValue().entrySet()) {
                    System.out.println("Impl: " + sitePlatform.getKey() + " --- " + sitePlatform.getValue());
                }
            }

            //Get the root plan
            final LinkedHashMap<String, Tuple<String, String>> rootPlanImpls = FileUtils.generateStartingOperatorImplementationsWithSeeds(operatorGraph,
                    platformSeeds, siteSeeds, FileUtils.getOperatorImplementations(operatorGraph, network, dictionary, opNamesToClassKeysMap));
            final int rootCost = costEstimator.getPlanTotalCost(rootPlanImpls);
            final SimpleOptimizationPlan rootPlan = new SimpleOptimizationPlan(rootPlanImpls, 0, rootCost);

            //Bundle everything in a single obj
            OptimizationResourcesBundle bundle = OptimizationResourcesBundle.builder()
                    .withNetwork(network)
                    .withNewDictionary(dictionary)
                    .withWorkflow(request)
                    .withAlgorithm(algorithmName)
                    .withUser(optReqDoc.getUser())
                    .withThreads(Math.toIntExact(optParams.getParallelism()))
                    .withTimeout((int) optParams.getTimeout_ms())
                    .withRequestId(requestId)
                    .withPlanQueue(validPlansQueue)
                    .withExecutorService(requestExecutorService)
                    .withCostEstimator(costEstimator)
                    .withLogger(log)
                    .withRootPlan(rootPlan)
                    .build();

            //Inject dependencies
            try {
                gta.setup(bundle);
            } catch (OptimizerException e) {
                throw new IllegalStateException("Failed to construct plan graph.");
            }
            Instant setupDoneInstant = Instant.now();
            bundle.addStat("Setup time (ms)", Math.toIntExact(Duration.between(startInstant, setupDoneInstant).toMillis()));

            //Compute the plan
            gta.doWork();
            Instant optimizationDoneInstant = Instant.now();
            bundle.addStat("Path exploration time (ms)", Math.toIntExact(Duration.between(setupDoneInstant, optimizationDoneInstant).toMillis()));

            //Clean up
            gta.teardown();
            Instant teardownDoneInstant = Instant.now();
            bundle.addStat("Teardown time (ms)", Math.toIntExact(Duration.between(optimizationDoneInstant, teardownDoneInstant).toMillis()));
            bundle.addStat("E2E elapsed time (ms)", Math.toIntExact(Duration.between(startInstant, teardownDoneInstant).toMillis()));

            //If the result was produced from a continuous query then submit it again
            this.isContinuous = optParams.isContinuous();

            //Return the bundle
            return bundle;
        }
    }
}
