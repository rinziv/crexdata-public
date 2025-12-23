import argparse
import os
import uuid
import traceback
import threading

from common.kafka_utils import KafkaConsumer, KafkaTopicManager
from common.log_manager import LogManager
from common.utils import cleanup_logs, launch_process, write_temp_config

# Constants
NS3_PROC = "ns3_runner.py"
ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
mediator_logger = None
log_manager = None


def listen_for_config(broker, topic, group_id):
    consumer_config = {
        "bootstrap.servers": broker,
        "group.id": group_id,
        "auto.offset.reset": "latest",
        "enable.auto.commit": True,
    }
    kafka_consumer = KafkaConsumer(topic, consumer_config)
    mediator_logger.info(f"Listening for configurations on topic '{topic}'...")
    try:
        while True:
            message = kafka_consumer.receive(format_type="json")
            mediator_logger.info(f"Received new configuration!")
            log_manager.enforce_capacity()
            yield message
            mediator_logger.info(f"Waiting for new configurations...")
    finally:
        kafka_consumer.close()


def handle_config(config):
    mode = config["mode"]
    pid = uuid.uuid4().hex[:12]

    mediator_logger.info(f"Launching {mode.capitalize()} job...")
    mediator_logger.info(f"Config: {config[mode]}")

    if mode == "ns3":
        thread = threading.Thread(
            target=run_ns3_thread,
            args=(config[mode], pid),
            daemon=True,
        )
    else:
        mediator_logger.error(f"Invalid mode '{mode}' in config. Skipping...")
        return
    thread.start()
    mediator_logger.info(f"Successfully Launched {mode.capitalize()} job [PID: {pid}]!")


def run_ns3_thread(cfg, pid):
    try:
        cfg["pid"] = pid
        tmp_path = write_temp_config(cfg)
        process = launch_process(script=NS3_PROC, config_path=tmp_path, cwd="ns3")
        process.wait()  # Wait for process to finish

        if process.returncode != 0:
            mediator_logger.error(
                f"[PID: {pid}] ❌ NS3 exited with non-zero code: {process.returncode}"
            )
        else:
            mediator_logger.info(f"[PID: {pid}] ✅ NS3 job completed successfully.")
    except Exception as e:
        mediator_logger.error(f"NS3 failed [PID: {pid}]: {e}")
        mediator_logger.error(traceback.format_exc())
    finally:
        # Ensure file is deleted even on error
        if os.path.exists(tmp_path):
            os.remove(tmp_path)


def main():
    global log_manager, mediator_logger
    parser = argparse.ArgumentParser(description="Mediator Listener")
    parser.add_argument("--broker", type=str, default="localhost:9092")
    parser.add_argument("--topic", type=str, default="configTopic")
    parser.add_argument("--group_id", type=str, default="crex-mediator")
    parser.add_argument("--log_capacity", type=int, default=100)
    parser.add_argument(
        "--cleanup",
        action="store_true",
        help="Remove all files inside the logs/ directory before starting",
    )
    args = parser.parse_args()
    if args.cleanup:
        cleanup_logs("logs")

    log_manager = LogManager(capacity=args.log_capacity)
    mediator_logger = log_manager.get_logger(name="Mediator", mode="global")
    mediator_logger.info("Mediator starting...")
    mediator_logger.info(
        f"Setup attributes: Kafka Broker: {args.broker} | Topic: {args.topic} | Log capacity: {args.log_capacity}"
    )

    topic_mgr = KafkaTopicManager(args.broker, logger=mediator_logger)
    topic_mgr.create_topic(args.topic)

    for config in listen_for_config(args.broker, args.topic, args.group_id):
        handle_config(config)


if __name__ == "__main__":
    main()
