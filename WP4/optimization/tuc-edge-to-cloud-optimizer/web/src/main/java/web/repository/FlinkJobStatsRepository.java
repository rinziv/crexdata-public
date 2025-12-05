package web.repository;

import org.springframework.data.elasticsearch.repository.ElasticsearchRepository;
import web.document.FlinkJobDocument;

public interface FlinkJobStatsRepository extends ElasticsearchRepository<FlinkJobDocument, String> {

}
