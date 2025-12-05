package core.graph;

import java.util.*;


public final class Graphs {

    private Graphs() {}

    public static <T> ThreadSafeDAG<ChainPayload<T>> contractChains(ThreadSafeDAG<T> g) {
        if (g == null || g.isEmpty()) {
            return new ThreadSafeDAG<>(Collections.emptyList(), Collections.emptyList());
        }

        // --- 1) Degree maps + children/parents
        Map<Vertex<T>, Integer> inDeg = new HashMap<>();
        Map<Vertex<T>, Integer> outDeg = new HashMap<>();
        Map<Vertex<T>, List<Vertex<T>>> children = new HashMap<>();
        Map<Vertex<T>, List<Vertex<T>>> parents = new HashMap<>();

        for (Vertex<T> v : g.getVertices()) {
            children.put(v, new ArrayList<>(g.getChildren(v)));
            outDeg.put(v, children.get(v).size());
            inDeg.put(v, 0);
            parents.put(v, new ArrayList<>());
        }
        for (Edge<T> e : g.getEdges()) {
            Vertex<T> u = e.getFirst();
            Vertex<T> v = e.getSecond();
            inDeg.put(v, inDeg.get(v) + 1);
            parents.get(v).add(u);
        }

        // Helper: internal iff in=1 && out=1
        final java.util.function.Predicate<Vertex<T>> isInternal =
                v -> inDeg.get(v) == 1 && outDeg.get(v) == 1;

        // --- 2) Topological order (Kahn) for deterministic, upstream-first processing
        List<Vertex<T>> topo = new ArrayList<>(g.totalVertices());
        Map<Vertex<T>, Integer> inDegWork = new HashMap<>(inDeg);
        Deque<Vertex<T>> q = new ArrayDeque<>();
        for (Vertex<T> v : g.getVertices()) {
            if (inDegWork.get(v) == 0) q.add(v);
        }
        while (!q.isEmpty()) {
            Vertex<T> u = q.removeFirst();
            topo.add(u);
            for (Vertex<T> w : children.get(u)) {
                int d = inDegWork.get(w) - 1;
                inDegWork.put(w, d);
                if (d == 0) q.add(w);
            }
        }
        // index for sorting
        Map<Vertex<T>, Integer> topoIndex = new HashMap<>();
        for (int i = 0; i < topo.size(); i++) topoIndex.put(topo.get(i), i);

        // --- 3) Start vertices: cannot extend backward
        List<Vertex<T>> starts = new ArrayList<>();
        for (Vertex<T> v : g.getVertices()) {
            if (inDeg.get(v) != 1) {
                starts.add(v);
            } else {
                // unique predecessor
                Vertex<T> p = parents.get(v).get(0);
                if (!(inDeg.get(p) == 1 && outDeg.get(p) == 1)) {
                    // predecessor not internal -> v starts a chain
                    starts.add(v);
                }
            }
        }
        // process upstream first
        starts.sort(Comparator.comparingInt(topoIndex::get));

        // --- 4) Build maximal chains
        Set<Vertex<T>> visited = new HashSet<>();
        List<List<Vertex<T>>> chains = new ArrayList<>();

        for (Vertex<T> s : starts) {
            if (visited.contains(s)) continue;

            List<Vertex<T>> members = new ArrayList<>();
            Vertex<T> curr = s;
            members.add(curr);
            visited.add(curr);

            while (outDeg.get(curr) == 1) {
                Vertex<T> next = children.get(curr).get(0);

                if (visited.contains(next)) break;

                // HARD STOP: never include a successor that has multiple inputs
                if (inDeg.get(next) > 1) {
                    break;  // do NOT add 'next'; the chain ends at 'curr'
                }

                members.add(next);
                visited.add(next);

                if (!isInternal.test(next)) {
                    break;  // include other non-internal endpoints (e.g., sinks or fan-out), but stop
                }
                curr = next;
            }

            chains.add(members);
        }

        // Safety: any leftovers (shouldn’t really occur) as singletons
        for (Vertex<T> v : g.getVertices()) {
            if (!visited.contains(v)) {
                chains.add(Collections.singletonList(v));
                visited.add(v);
            }
        }

        // --- 5) Create merged vertices
        Map<Vertex<T>, Vertex<ChainPayload<T>>> rep = new HashMap<>();
        List<Vertex<ChainPayload<T>>> newVertices = new ArrayList<>(chains.size());
        for (List<Vertex<T>> chain : chains) {
            Vertex<ChainPayload<T>> merged = new Vertex<>(new ChainPayload<>(chain));
            newVertices.add(merged);
            for (Vertex<T> old : chain) rep.put(old, merged);
        }

        Vertex<ChainPayload<T>> newRoot = rep.get(g.getRoot());
        ThreadSafeDAG<ChainPayload<T>> result = (newRoot != null)
                ? new ThreadSafeDAG<>(newRoot)
                : new ThreadSafeDAG<>(newVertices, Collections.emptyList());

        for (Vertex<ChainPayload<T>> v : newVertices) {
            if (newRoot == null || !v.equals(newRoot)) result.addVertex(v);
        }

        // --- 6) Rewire edges (dedup)
        Set<Edge<ChainPayload<T>>> newEdges = new HashSet<>();
        for (Edge<T> e : g.getEdges()) {
            Vertex<ChainPayload<T>> u = rep.get(e.getFirst());
            Vertex<ChainPayload<T>> v = rep.get(e.getSecond());
            if (u != null && v != null && !u.equals(v)) {
                newEdges.add(new Edge<>(u, v));
            }
        }
        for (Edge<ChainPayload<T>> e : newEdges) result.addEdge(e);

        return result;
    }
}
