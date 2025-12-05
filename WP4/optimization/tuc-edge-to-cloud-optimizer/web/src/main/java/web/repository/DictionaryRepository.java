package web.repository;

import org.springframework.data.elasticsearch.repository.ElasticsearchRepository;
import web.document.DictionaryDocument;

public interface DictionaryRepository extends ElasticsearchRepository<DictionaryDocument, String> {
}
