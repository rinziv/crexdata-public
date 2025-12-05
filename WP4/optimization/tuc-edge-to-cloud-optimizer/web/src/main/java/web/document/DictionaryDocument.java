package web.document;

import core.parser.dictionary.Dictionary;
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

@Document(indexName = "crexdata_dictionary")
@EqualsAndHashCode(onlyExplicitlyIncluded = true)
@Data
public class DictionaryDocument implements RepositoryDocument<Dictionary>, Serializable {
    @Id
    @EqualsAndHashCode.Include
    private String id;

    @Field(name = "dictionary", type = FieldType.Text)
    private String dictionary;

    @Field(name = "@timestamp", type = FieldType.Date, format = DateFormat.date_optional_time)
    private Date modifiedAt;

    public DictionaryDocument() {
    }

    public DictionaryDocument(Dictionary dictionary) {
        this.id = dictionary.getName();
        this.dictionary = gson.toJson(dictionary);
        this.modifiedAt = new Date();
    }

    @Override
    public String id() {
        return this.id;
    }

    @Override
    public Dictionary getObject() {
        return gson.fromJson(dictionary, Dictionary.class);
    }
}
