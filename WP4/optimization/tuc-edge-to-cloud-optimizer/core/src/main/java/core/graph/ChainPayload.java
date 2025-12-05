package core.graph;

import core.parser.workflow.Operator;

import java.util.List;
import java.util.Objects;
import java.util.stream.Collectors;

public class ChainPayload<T> {
    private final List<Vertex<T>> members;
    private final String chainId;
    public static final String CHAIN_ID_DELIMITER = "%";

    public ChainPayload(List<Vertex<T>> members) {
        if (members == null || members.isEmpty()) {
            throw new IllegalArgumentException("ChainPayload must have at least one member");
        }

        this.members = List.copyOf(members);
        this.chainId = this.members.stream()
                .map(v -> ((Operator) v.getData()).getName())
                .collect(Collectors.joining(CHAIN_ID_DELIMITER));
    }

    public String getChainId() {
        return this.chainId;
    }

    public List<Vertex<T>> getMembers() {
        return members;
    }

    public Vertex<T> first() {
        return members.get(0);
    }

    public Vertex<T> last() {
        return members.get(members.size() - 1);
    }

    @Override
    public String toString() {
        return "ChainPayload{" +
                "members=" + members +
                '}';
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (!(o instanceof ChainPayload)) return false;
        ChainPayload<?> that = (ChainPayload<?>) o;
        return members.equals(that.members);
    }

    @Override
    public int hashCode() {
        return Objects.hash(members);
    }
}
