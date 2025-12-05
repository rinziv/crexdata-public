package core.utils;

import core.graph.ChainPayload;
import core.graph.ThreadSafeDAG;
import core.graph.Vertex;
import core.parser.workflow.Operator;

import java.util.*;

public final class GraphUtils {

    private GraphUtils() {}

    /** childId -> { parentIds } */
    public static <X> Map<String, Set<String>> getOperatorParentMap(ThreadSafeDAG<X> operatorGraph) {
        Map<String, Set<String>> operatorParents = new HashMap<>();
        for (Vertex<X> v : operatorGraph.getVertices()) {
            String parentId = idOf(v.getData());
            operatorParents.putIfAbsent(parentId, new HashSet<>());

            for (Vertex<X> child : operatorGraph.getChildren(v)) {
                String childId = idOf(child.getData());
                operatorParents.putIfAbsent(childId, new HashSet<>());
                operatorParents.get(childId).add(parentId);
            }
        }
        return operatorParents;
    }

    /** parentId -> { childIds } */
    public static <X> Map<String, Set<String>> getOperatorChildrenMap(ThreadSafeDAG<X> operatorGraph) {
        Map<String, Set<String>> parentChildren = new HashMap<>();
        for (Vertex<X> v : operatorGraph.getVertices()) {
            String parentId = idOf(v.getData());
            parentChildren.putIfAbsent(parentId, new HashSet<>());

            for (Vertex<X> child : operatorGraph.getChildren(v)) {
                String childId = idOf(child.getData());
                parentChildren.get(parentId).add(childId);
            }
        }
        return parentChildren;
    }

    /** Resolve a stable string ID for either Operator or ChainPayload<Operator>. */
    private static String idOf(Object payload) {
        if (payload instanceof Operator) {
            return ((Operator) payload).getName();
        }
        if (payload instanceof ChainPayload) {
            @SuppressWarnings("rawtypes")
            ChainPayload cp = (ChainPayload) payload;
            return cp.getChainId();
        }
        // Fallback to toString to avoid NPEs on unexpected payloads
        return String.valueOf(payload);
    }
}
