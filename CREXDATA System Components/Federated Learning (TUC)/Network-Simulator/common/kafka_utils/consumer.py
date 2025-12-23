import base64
from collections import defaultdict
import logging

logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s"
)

from confluent_kafka import Consumer
from .serialization import SerializerRegistry


class KafkaConsumer:
    def __init__(self, topic: str, config=None):
        self.logger = logging.getLogger(__name__)
        config = config or {}
        self.topic = topic
        self.broker = config.get("bootstrap.servers", "localhost:9092")
        config.setdefault("bootstrap.servers", self.broker)

        self.consumer = Consumer(config)
        self.consumer.subscribe([self.topic])

    def receive(self, format_type="json", timeout=1.0):
        chunks_buffer = defaultdict(lambda: {"chunks": {}, "total_chunks": None})
        registry = SerializerRegistry()

        while True:
            msg = self.consumer.poll(timeout=timeout)
            if msg is None:
                continue
            if msg.error():
                self.logger.error(f"❌ Kafka error: {msg.error()}")
                continue

            raw = msg.value()
            decoded = registry.safe_json_load(raw) if format_type != "model" else raw

            if isinstance(decoded, dict) and "message_id" in decoded:
                mid = decoded["message_id"]
                cid = decoded["chunk_id"]
                total = decoded["total_chunks"]
                chunks_buffer[mid]["chunks"][cid] = decoded["data"]
                chunks_buffer[mid]["total_chunks"] = total

                if len(chunks_buffer[mid]["chunks"]) == total:
                    self.logger.info(
                        f"🔄 All chunks received for message {mid}, reconstructing..."
                    )
                    full_data = "".join(
                        [chunks_buffer[mid]["chunks"][i] for i in range(total)]
                    )
                    try:
                        binary = base64.b64decode(full_data)
                        obj = registry.deserialize(binary, format_type)
                        self.logger.info(
                            f"✅ Final message {mid} reconstructed successfully!"
                        )
                        del chunks_buffer[mid]
                        return obj
                    except Exception as e:
                        self.logger.error(f"❌ Base64 decoding error: {e}")
                        break
            else:
                try:
                    data = registry.deserialize(raw, format_type)
                    # self.logger.info(f"✅ Message Deserialized successfully!")
                    return data
                except Exception as e:
                    self.logger.error(f"❌ Failed Deserialization: {e}")
                    return None

    def close(self):
        """Close the Kafka consumer cleanly."""
        if self.consumer:
            # self.logger.info("🛑 Closing Kafka consumer...")
            self.consumer.close()
