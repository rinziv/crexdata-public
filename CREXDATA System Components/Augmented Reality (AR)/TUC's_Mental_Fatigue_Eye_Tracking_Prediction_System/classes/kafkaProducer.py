import time
import json
from uuid import uuid4
from confluent_kafka import Producer

jsonString1 = """ {"name":"Gal", "email":"Gadot84@gmail.com", "salary": "8345.55"} """
jsonString2 = (
    """ {"name":"Dwayne", "email":"Johnson52@gmail.com", "salary": "7345.75"} """
)
jsonString3 = """ {"name":"Momoa", "email":"Jason91@gmail.com", "salary": "3345.25"} """

jsonv1 = jsonString1.encode()
jsonv2 = jsonString2.encode()
jsonv3 = jsonString3.encode()


def delivery_report(errmsg, msg):
    """
    Reports the Failure or Success of a message delivery.
    Args:
        errmsg  (KafkaError): The Error that occurred while message producing.
        msg    (Actual message): The message that was produced.
    Note:
        In the delivery report callback the Message.key() and Message.value()
        will be the binary format as encoded by any configured Serializers and
        not the same object that was passed to produce().
        If you wish to pass the original object(s) for key and value to delivery
        report callback we recommend a bound callback or lambda where you pass
        the objects along.
    """
    if errmsg is not None:
        print("Delivery failed for Message: {} : {}".format(msg.key(), errmsg))
        return
    print(
        "Message: {} successfully produced to Topic: {} Partition: [{}] at offset {}".format(
            msg.key(), msg.topic(), msg.partition(), msg.offset()
        )
    )


def kafka_producer(rows, producerConfig):
    kafka_topic_name = "test_topic"
    # Change your Kafka Topic Name here. For this Example: lets assume our Kafka Topic has 3 Partitions==>  0,1,2
    # And We are producing messages uniformly to all partitions.
    # We are sending the message as ByteArray.
    # If We want read the same message from a Java Consumer Program
    # We can configure KEY_DESERIALIZER_CLASS_CONFIG = ByteArrayDeserializer.class
    # and VALUE_DESERIALIZER_CLASS_CONFIG = ByteArrayDeserializer.class

    # Trigger any available delivery report callbacks from previous produce() calls
    producerConfig.poll(0)
    payload = rows.to_json(orient="records").encode()
    try:
        # Asynchronously produce a message, the delivery report callback
        # will be triggered from poll() above, or flush() below, when the message has
        # been successfully delivered or failed permanently.
        producerConfig.produce(
            topic=kafka_topic_name,
            key=str(uuid4()),
            value=payload,
            on_delivery=delivery_report,
        )

        # Wait for any outstanding messages to be delivered and delivery report
        # callbacks to be triggered.
        producerConfig.flush()

    except Exception as ex:
        print("Exception happened :", ex)

    print("\n Stopping Kafka Producer")
