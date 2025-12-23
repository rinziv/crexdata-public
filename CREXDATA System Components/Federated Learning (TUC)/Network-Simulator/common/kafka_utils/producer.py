import base64
import json
import sys
import uuid
import logging

logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s"
)

from confluent_kafka import KafkaException, Producer

from .serialization import SerializerRegistry


class KafkaProducer:
    def __init__(self, config=None, logger=None):
        self.logger = logging.getLogger(__name__) if logger is None else logger
        config = config or {}
        self.broker = config.get("bootstrap.servers", "localhost:9092")
        config.setdefault("bootstrap.servers", self.broker)

        self.producer = Producer(config)

    def send(self, topic, message, format_type="json", max_size=1_000_000):
        registry = SerializerRegistry()
        serialized_data = registry.serialize(message, format_type)

        if sys.getsizeof(serialized_data) < max_size:
            self._send_kafka_message(topic, serialized_data)
        else:
            self._send_large_message(topic, serialized_data, max_size)

        self.producer.flush()

    def _send_kafka_message(self, topic, message):
        try:
            self.producer.produce(topic, value=message)
            self.logger.info(f"✅ Message sent to {topic}")
        except KafkaException as e:
            self.logger.error(f"❌ Failed to send message to {topic}: {e}")
            raise

    def _send_large_message(self, topic, data, max_size):
        encoded_data = base64.b64encode(data).decode("utf-8")
        message_id = str(uuid.uuid4())

        BUFFER = 200
        chunk_size = max_size - BUFFER
        chunks = [
            encoded_data[i : i + chunk_size]
            for i in range(0, len(encoded_data), chunk_size)
        ]
        total_chunks = len(chunks)

        for chunk_id, chunk_data in enumerate(chunks):
            payload = json.dumps(
                {
                    "message_id": message_id,
                    "chunk_id": chunk_id,
                    "total_chunks": total_chunks,
                    "data": chunk_data,
                }
            ).encode("utf-8")
            self._send_kafka_message(topic, payload)

        self.logger.info(f"✅ Large message sent in {total_chunks} chunks.")

    def close(self):
        """Flush any remaining messages (confluent_kafka Producer doesn't require an explicit close)."""
        self.producer.flush()
