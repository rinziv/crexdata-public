package core.utils;

import core.graph.*;
import core.iterable.DAGIterable;
import core.parser.dictionary.Dictionary;
import core.parser.network.Network;
import core.parser.workflow.OptimizationRequest;
import core.parser.workflow.Operator;
import core.structs.Tuple;

import java.util.*;
import java.util.stream.Collectors;

public final class FileUtils {
    private static final Random RNG = new Random(0);


    public static ThreadSafeDAG<ChainPayload<Operator>> getOperatorGraphContracted(OptimizationRequest optimizationRequest) {
        return Graphs.contractChains(getOperatorGraph(optimizationRequest));
    }

    public static ThreadSafeDAG<Operator> getOperatorGraph(OptimizationRequest optimizationRequest) {
        Map<String, Vertex<Operator>> operatorMap = new HashMap<>();
        optimizationRequest.getOperators().forEach(op -> operatorMap.put(op.getName(), new Vertex<>(op)));

        List<Edge<Operator>> edges = optimizationRequest.getOperatorConnections().stream()
                .map(conn -> new Edge<>(new Vertex<>(operatorMap.get(conn.getFromOperator())), new Vertex<>(operatorMap.get(conn.getToOperator()))))
                .collect(Collectors.toList());

        return new ThreadSafeDAG<>(operatorMap.values(), edges);
    }

    @SuppressWarnings("unchecked")
    public static LinkedHashMap<String, Map<String, List<String>>> getOperatorImplementations(
            ThreadSafeDAG<?> operatorGraph,
            Network network,
            Dictionary dictionary,
            Map<String, String> classKeyMapping) {

        LinkedHashMap<String, Map<String, List<String>>> opMap = new LinkedHashMap<>();

        for (Vertex<?> v : operatorGraph.getVertices()) {
            Object payload = v.getData();

            if (payload instanceof Operator) {
                Operator op = (Operator) payload;
                String opName = op.getName();
                opMap.put(opName, getImplementations(opName, dictionary, network, classKeyMapping));

            } else if (payload instanceof ChainPayload) {
                ChainPayload<Operator> chain = (ChainPayload<Operator>) payload;
                String chainId = chain.getChainId();

                Map<String, List<String>> intersection = null;
                for (Vertex<Operator> member : chain.getMembers()) {
                    String name = member.getData().getName();
                    Map<String, List<String>> impls =
                            getImplementations(name, dictionary, network, classKeyMapping);

                    if (intersection == null) {
                        // Start from the first member's map (make a deep, mutable copy)
                        intersection = deepCopy(impls);
                    } else {
                        intersection = intersectImplMaps(intersection, impls);
                        if (intersection.isEmpty()) break; // early exit if nothing in common
                    }
                }

                if (intersection == null) {
                    intersection = Collections.emptyMap(); // defensive (shouldn't happen)
                }
                // Optionally wrap as unmodifiable if you don't want callers to mutate:
                // intersection = unmodifiableDeepCopy(intersection);

                opMap.put(chainId, intersection);

            } else {
                throw new IllegalArgumentException(
                        "Unsupported vertex payload type: " + (payload == null ? "null" : payload.getClass().getName())
                );
            }
        }
        return opMap;
    }

    /** Get implementations for a single operator name (already provided). */
    private static Map<String, List<String>> getImplementations(String operatorName,
                                                                Dictionary dictionary,
                                                                Network network,
                                                                Map<String, String> classKeyMapping) {
        String classKey = classKeyMapping.getOrDefault(operatorName, operatorName);
//        System.out.println("Getting implementations for operator: " + operatorName +
//                " with class key: " + classKey);
        return dictionary.getImplementationsForClassKey(classKey, network);
    }

    /** Deep copy map<String, List<String>> preserving insertion order of keys and list order. */
    private static Map<String, List<String>> deepCopy(Map<String, List<String>> src) {
        LinkedHashMap<String, List<String>> copy = new LinkedHashMap<>();
        for (Map.Entry<String, List<String>> e : src.entrySet()) {
            copy.put(e.getKey(), new ArrayList<>(e.getValue()));
        }
        return copy;
    }

    /**
     * Intersection of two implementation maps:
     * - Keep only keys present in BOTH maps.
     * - For each key, keep only list elements present in BOTH lists.
     * - Preserve the order from the first map's list for that key.
     */
    private static Map<String, List<String>> intersectImplMaps(Map<String, List<String>> a,
                                                               Map<String, List<String>> b) {
        LinkedHashMap<String, List<String>> res = new LinkedHashMap<>();
        for (Map.Entry<String, List<String>> e : a.entrySet()) {
            String key = e.getKey();
            List<String> listA = e.getValue();
            List<String> listB = b.get(key);
            if (listB == null) continue;

            // Intersect lists, preserving order of listA
            Set<String> setB = new LinkedHashSet<>(listB);
            List<String> inter = new ArrayList<>();
            for (String s : listA) {
                if (setB.contains(s)) inter.add(s);
            }
            if (!inter.isEmpty()) {
                res.put(key, inter);
            }
        }
        return res;
    }

    @SuppressWarnings("unchecked")
    public static LinkedHashMap<String, List<Tuple<String, String>>> getOperatorImplementationsAsTuples(
            ThreadSafeDAG<?> operatorGraph,
            Network network,
            Dictionary dictionary,
            Map<String, String> classKeyMapping) {

        LinkedHashMap<String, List<Tuple<String, String>>> opMap = new LinkedHashMap<>();

        for (Vertex<?> v : operatorGraph.getVertices()) {
            Object payload = v.getData();

            if (payload instanceof Operator) {
                Operator op = (Operator) payload;
                String operatorName = op.getName();

                Map<String, List<String>> impls =
                        getImplementationsMap(operatorName, dictionary, network, classKeyMapping);

                opMap.put(operatorName, flattenToTuples(impls));

            } else if (payload instanceof ChainPayload) {
                ChainPayload<Operator> chain = (ChainPayload<Operator>) payload;
                String chainId = chain.getChainId();

                // Intersect implementations across chain members
                Map<String, List<String>> intersection = null;
                for (Vertex<Operator> member : chain.getMembers()) {
                    String name = member.getData().getName();
                    Map<String, List<String>> impls =
                            getImplementationsMap(name, dictionary, network, classKeyMapping);

                    if (intersection == null) {
                        intersection = deepCopy(impls);
                    } else {
                        intersection = intersectImplMaps(intersection, impls);
                        if (intersection.isEmpty()) break; // early-exit
                    }
                }
                if (intersection == null) intersection = Collections.emptyMap();

                opMap.put(chainId, flattenToTuples(intersection));

            } else {
                throw new IllegalArgumentException(
                        "Unsupported vertex payload type: " + (payload == null ? "null" : payload.getClass().getName()));
            }
        }
        return opMap;
    }

    private static List<Tuple<String, String>> flattenToTuples(Map<String, List<String>> impls) {
        // Use a LinkedHashSet to avoid duplicate tuples while preserving order.
        LinkedHashSet<Tuple<String, String>> set = new LinkedHashSet<>();
        for (Map.Entry<String, List<String>> e : impls.entrySet()) {
            String site = e.getKey();
            List<String> platforms = e.getValue();
            if (platforms == null) continue;
            for (String platform : platforms) {
                set.add(new Tuple<>(site, platform));
            }
        }
        return new ArrayList<>(set);
    }

    private static Map<String, List<String>> getImplementationsMap(String operatorName,
                                                                   Dictionary dictionary,
                                                                   Network network,
                                                                   Map<String, String> classKeyMapping) {
        String classKey = classKeyMapping.getOrDefault(operatorName, operatorName);
//        System.out.println("Getting implementations for operator: " + operatorName +
//                " with class key: " + classKey);
        return dictionary.getImplementationsForClassKey(classKey, network);
    }


    //Place everything on the first platform and site that are available, present networks ensure that this is possible.
    public static LinkedHashMap<String, Tuple<String, String>> generateStartingOperatorImplementations(ThreadSafeDAG<Operator> operatorGraph,
                                                                                                       Map<String, Map<String, List<String>>> operatorImplementations) {
        LinkedHashMap<String, Tuple<String, String>> operatorImplementation = new LinkedHashMap<>();
        for (Vertex<Operator> operator : new DAGIterable<>(operatorGraph)) {
            String opName = operator.getData().getName();
            Map<String, List<String>> opSAP = operatorImplementations.get(opName);
            if (opSAP.isEmpty()) {
                throw new IllegalStateException("Empty operator site implementation list");
            }
            List<String> allSites = new ArrayList<>(opSAP.keySet());
            String site = allSites.get(RNG.nextInt(allSites.size()));

            List<String> allPlatforms = new ArrayList<>(opSAP.get(site));
            String platform = allPlatforms.get(RNG.nextInt(allPlatforms.size()));

            operatorImplementation.put(opName, new Tuple<>(site, platform));
        }
        return operatorImplementation;
    }

    public static LinkedHashMap<String, Tuple<String, String>> generateStartingOperatorImplementationsWithSeeds(ThreadSafeDAG<Operator> operatorGraph,
                                                                                                                List<Integer> platformSeeds,
                                                                                                                List<Integer> siteSeeds,
                                                                                                                Map<String, Map<String, List<String>>> operatorImplementations) {
        LinkedHashMap<String, Tuple<String, String>> operatorImplementation = new LinkedHashMap<>();
        int opCnt = 0;
        for (Vertex<Operator> operator : new DAGIterable<>(operatorGraph)) {
            String opName = operator.getData().getName();
            Map<String, List<String>> opSAP = operatorImplementations.get(opName);
            if (opSAP.isEmpty()) {
                throw new IllegalStateException("Empty operator site implementation list");
            }
            List<String> allSites = new ArrayList<>(opSAP.keySet());
            Collections.sort(allSites);
            String site = allSites.get(Math.min(siteSeeds.get(opCnt), allSites.size() - 1));

            List<String> allPlatforms = new ArrayList<>(opSAP.get(site));
            Collections.sort(allPlatforms);
            String platform = allPlatforms.get(Math.min(platformSeeds.get(opCnt), allPlatforms.size() - 1));

            operatorImplementation.put(opName, new Tuple<>(site, platform));
            opCnt++;
        }
        return operatorImplementation;
    }

    public static Map<String, String> getOpNameToClassKeyMapping(OptimizationRequest optimizationRequest) {
        Map<String, String> classNames = new HashMap<>();
        for (Operator operator : optimizationRequest.getOperators()) {
            classNames.put(operator.getName(), operator.getClassKey());
        }
        return classNames;
    }
}
