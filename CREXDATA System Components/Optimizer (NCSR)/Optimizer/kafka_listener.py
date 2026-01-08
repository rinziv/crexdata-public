import json
from kafka import KafkaConsumer

# Kafka configuration
BROKER = 'server.crexdata.eu:9192'
# TOPIC = 'UPB-CREXDATA-BiometricData-Explanation'
TOPIC = 'ga_out'

# Create consumer
consumer = KafkaConsumer(
    TOPIC,
    bootstrap_servers=BROKER,
    value_deserializer=lambda m: json.loads(m.decode('utf-8')),
    auto_offset_reset='latest',
    enable_auto_commit=True
)

print(f"Listening to topic '{TOPIC}' on {BROKER}...")
print("Press Ctrl+C to stop\n")

# Listen for messages
try:
    for message in consumer:
        print(f"Received: {message.value}")
        print(f"Timestamp: {message.timestamp}")
        print(f"Partition: {message.partition}, Offset: {message.offset}")
        print("-" * 50)
except KeyboardInterrupt:
    print("\nStopping listener...")
finally:
    consumer.close()