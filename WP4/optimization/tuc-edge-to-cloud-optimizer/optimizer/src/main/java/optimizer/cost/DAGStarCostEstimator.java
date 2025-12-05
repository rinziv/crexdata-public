package optimizer.cost;

import core.graph.ChainPayload;
import core.parser.dictionary.Dictionary;
import core.structs.Tuple;
import optimizer.algorithm.AStarSearchAlgorithm;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Random;

public class DAGStarCostEstimator implements CostEstimator {

    private final Dictionary dictionary;
    private final Map<String, String> classKeyMapping;

    public DAGStarCostEstimator(Dictionary dictionary, Map<String, String> classKeyMapping) {
        this.dictionary = dictionary;
        this.classKeyMapping = classKeyMapping;
    }

    /**
     * Not implemented!
     */
    @Override
    public int getPlanTotalCost(LinkedHashMap<String, Tuple<String, String>> implementationMap) {
        return 0;
    }

    /**
     * Νot implemented!
     */
    @Override
    public int getHeuristicCostForOperator(String operator) {
        return 0;
    }

    /**
     * Not implemented!
     */
    @Override
    public int getMigrationCost(String prevOp, Tuple<String, String> prevImpl, Tuple<String, String> newImpl) {
        return 0;
    }

    @Override
    public int getOperatorAndImplementationCost(String newOp, Tuple<String, String> newImpl) {
        if (newOp == null) throw new IllegalStateException("Null operator received...");

        if (newOp.contains(ChainPayload.CHAIN_ID_DELIMITER)) {
            String[] chainedOperators = newOp.split(ChainPayload.CHAIN_ID_DELIMITER);
            int totalCost = 0;
            for (String chainedOp : chainedOperators) {
                String chainedOpClassKey = classKeyMapping.get(chainedOp);
                if (chainedOpClassKey == null) throw new IllegalStateException("Class key not found for operator: " + newOp);
                String site = newImpl._1;
                int siteCost = dictionary.getSiteStaticCostForClassKey(chainedOpClassKey, site);
                String platform = newImpl._2;
                int platformCost = dictionary.getPlatformStaticCostForClassKey(chainedOpClassKey, platform);
                totalCost += siteCost + platformCost;
            }

            return totalCost;
        }

        String classKey = classKeyMapping.get(newOp);
        if (classKey == null) {
            throw new IllegalStateException("Class key not found for operator: " + newOp);
        }

        // Get the site static cost for the class key
        String site = newImpl._1;
        int siteCost = dictionary.getSiteStaticCostForClassKey(classKey, site);

        // Get the platform static cost for the class key
        String platform = newImpl._2;
        int platformCost = dictionary.getPlatformStaticCostForClassKey(classKey, platform);

        return siteCost + platformCost;
    }

    /**
     * Not implemented!
     */
    @Override
    public int getPlanPlatformCost(LinkedHashMap<String, Tuple<String, String>> implementationMap) {
        return 0;
    }


    @Override
    public int getCommunicationCost(String site1, String site2) {
        if (site1.equals(AStarSearchAlgorithm.VIRTUAL_START) || site2.equals(AStarSearchAlgorithm.VIRTUAL_END)) return 0;
        Random random = new Random(7);
        return random.nextInt(500);
    }

    @Override
    public int getMinCostForOperator(String operator) {
        if (operator.equals(AStarSearchAlgorithm.VIRTUAL_END) || operator.equals(AStarSearchAlgorithm.VIRTUAL_START))
            return 0;

        String classKey = classKeyMapping.get(operator);
        if (classKey == null)
            throw new IllegalStateException("Class key not found for operator: " + operator);

        // Get the minimum site cost for the operator
        return dictionary.getMinSiteCostForOperator(classKey);
    }
}
