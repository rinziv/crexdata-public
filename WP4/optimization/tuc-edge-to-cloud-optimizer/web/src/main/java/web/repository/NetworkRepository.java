package web.repository;

import org.springframework.data.elasticsearch.repository.ElasticsearchRepository;
import web.document.NetworkDocument;

public interface NetworkRepository extends ElasticsearchRepository<NetworkDocument, String> {
}
