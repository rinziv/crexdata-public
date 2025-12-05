package web.document;

import core.parser.network.Network;
import lombok.Data;
import lombok.EqualsAndHashCode;
import org.springframework.data.annotation.Id;
import org.springframework.data.elasticsearch.annotations.DateFormat;
import org.springframework.data.elasticsearch.annotations.Document;
import org.springframework.data.elasticsearch.annotations.Field;
import org.springframework.data.elasticsearch.annotations.FieldType;
import web.repository.RepositoryDocument;

import java.io.Serializable;
import java.util.Date;

@Document(indexName = "crexdata_network")
@EqualsAndHashCode(onlyExplicitlyIncluded = true)
@Data
public class NetworkDocument implements RepositoryDocument<Network>, Serializable {
    @Id
    @EqualsAndHashCode.Include
    private String id;

    @Field(name = "network", type = FieldType.Text)
    private String network;

    @Field(name = "@timestamp", type = FieldType.Date, format = DateFormat.date_optional_time)
    private Date modifiedAt;

    public NetworkDocument() {
    }

    public NetworkDocument(Network network) {
        this.id = network.getNetwork();
        this.network = gson.toJson(network);
        this.modifiedAt = new Date();
    }

    @Override
    public String id() {
        return this.id;
    }

    @Override
    public Network getObject() {
        return gson.fromJson(this.network, Network.class);
    }
}
