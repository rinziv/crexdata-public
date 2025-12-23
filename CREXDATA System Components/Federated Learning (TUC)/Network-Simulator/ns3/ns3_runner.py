import argparse
import logging
import multiprocessing
import os
import sys
import time
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed

import pandas as pd
from omegaconf import DictConfig

from runner_utils import (
    consolidate_sim_results,
    create_experiment_dir,
    get_all_run_entries,
    merge_cofigs,
    summary_df_to_dict,
    use_cache,
    prepare_round_configs,
)
from scratch.fl_simulation.utils.steps_generator import generate_and_save_steps

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from common.kafka_utils import KafkaProducer, KafkaTopicManager
from common.log_manager import LogManager
from common.utils import (
    is_dir_nonempty,
    safe_load_json,
    launch_process,
)

BASE_CFG_PTH = "conf/base_config.json"


def run_simulation(cfg: DictConfig, output_file="summary.csv", logger=None):
    """Main entry point to run NS3-based simulation with caching and retry logic."""
    ns3_logger = logging.getLogger(__file__) if logger is None else logger
    ns3_logger.info("Starting Federated Learning Simulation in NS3")
    ns3_logger.info(f"Config: {cfg}")

    # generate experiment files
    cfg.experiment_dir = create_experiment_dir(cfg, output_dir="records")
    steps_pth = os.path.join(cfg.experiment_dir, "steps.csv")
    min_val = 0 if cfg.sync_method == "sync" else 80
    max_val = 0 if cfg.sync_method == "sync" else 500
    generate_and_save_steps(
        cfg.num_rounds,
        cfg.num_clients,
        cfg.clients_per_round,
        min_val,
        max_val,
        steps_pth,
        mode="linear",
    )

    cached_df = use_cache(
        output_pth=os.path.join(cfg.experiment_dir, output_file),
        force_log=cfg.forceLog,
        logger=ns3_logger,
    )
    start_round = 1

    if cached_df is not None:
        completed_rounds = cached_df["round"].nunique()
        if completed_rounds >= cfg.num_rounds:
            ns3_logger.info("✅ All rounds already completed. Using cached result.")
            ns3_logger.info("Simulation Over!")
            return cached_df[: cfg.num_rounds]
        start_round = completed_rounds + 1
        ns3_logger.info(
            f"🔁 Resuming from round {start_round}. {completed_rounds} rounds already cached."
        )

    os.makedirs(cfg.experiment_dir, exist_ok=True)
    ns3_logger.info(f"Experiment directory: {cfg.experiment_dir}")

    round_configs = prepare_round_configs(cfg, start_round=start_round)
    max_workers = min(cfg.num_cpus, multiprocessing.cpu_count())
    _run_simulation_rounds(round_configs, max_workers, max_retries=3, logger=ns3_logger)

    result = consolidate_sim_results(cfg, output_file, cached_df, ns3_logger)
    ns3_logger.info("Simulation Over!")
    return result


def round_sim(cfg: DictConfig, logger=None):
    ns3_logger = logging.getLogger(__file__) if logger is None else logger

    command = [
        "./waf",
        "--run-no-build",
        # "--run",
        f"{cfg.program} "
        f"--forceLog={cfg.forceLog} "
        f"--roundNo={cfg.roundNo} "
        f"--numClients={cfg.num_clients} "
        f"--networkType={cfg.network_type} "
        f"--maxPacketSize={cfg.max_packet_size} "
        f"--txGain={cfg.tx_gain} "
        f"--modelSize={cfg.model_size} "
        f"--clientMobility={cfg.client_mobility} "
        f"--serverDatarate={cfg.server_datarate} "
        f"--networkStandard={cfg.network_standard} "
        f"--monitorMsgSize={cfg.monitor_msg_size} "
        f"--experimentDir={cfg.experiment_dir}",
    ]

    try:
        start_time = time.time()

        process = launch_process(command, text_logs=True)

        error_detected = False
        for line in process.stdout:
            ns3_logger.info(f"[Round {cfg.roundNo}] {line.strip()}")
            if "Traceback" in line or "Error" in line or "Exception" in line:
                error_detected = True

        process.wait()
        elapsed = time.time() - start_time
        if error_detected:
            ns3_logger.warning(
                f"[Round {cfg.roundNo}] ❌ Error (elapsed {elapsed:.2f}s)"
            )
        else:
            ns3_logger.info(
                f"[Round {cfg.roundNo}] ✅ Completed in {elapsed:.2f} seconds."
            )
        return not error_detected
    except Exception as e:
        ns3_logger.warning(f"[Round {cfg.roundNo}] ❌ Failed: {e}")
        return False


def _run_simulation_rounds(configs, max_workers, max_retries=3, logger=None):
    logger = logging.getLogger(__file__) if logger is None else logger

    retry_counts = defaultdict(int)
    completed_rounds = set()

    logger.info(f"🚀 Launching {len(configs)} rounds with {max_workers} workers...")

    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        future_to_cfg = {
            executor.submit(round_sim, cfg, logger): cfg for cfg in configs
        }

        while future_to_cfg:
            for future in as_completed(future_to_cfg):
                cfg = future_to_cfg.pop(future)
                try:
                    result = future.result()
                    if result:
                        logger.info(f"[Round {cfg.roundNo}] ✅ Finished successfully")
                        completed_rounds.add(cfg.roundNo)
                    else:
                        retry_counts[cfg.roundNo] += 1
                        if retry_counts[cfg.roundNo] < max_retries:
                            logger.warning(
                                f"[Round {cfg.roundNo}] ❌ Failed, retrying (attempt {retry_counts[cfg.roundNo]})..."
                            )
                            new_future = executor.submit(round_sim, cfg, logger)
                            future_to_cfg[new_future] = cfg
                        else:
                            logger.error(
                                f"[Round {cfg.roundNo}] ❌ Max retries reached. Skipping."
                            )
                except Exception as e:
                    retry_counts[cfg.roundNo] += 1
                    logger.warning(f"[Round {cfg.roundNo}] ❌ Exception occurred: {e}")
                    if retry_counts[cfg.roundNo] < max_retries:
                        new_future = executor.submit(round_sim, cfg, logger)
                        future_to_cfg[new_future] = cfg
                    else:
                        logger.error(
                            f"[Round {cfg.roundNo}] ❌ Max retries after crash. Skipping."
                        )
    logger.info(f"🏁 All {len(completed_rounds)} rounds completed!")


def _setup_kafka(cfg):
    bootstrap_server = f"{cfg.broker_ip}:{cfg.broker_port}"
    topic_manager = KafkaTopicManager(bootstrap_server, logger=ns3_logger)
    kafka_producer = KafkaProducer(
        {"bootstrap.servers": bootstrap_server}, logger=ns3_logger
    )
    topic_manager.create_topic(cfg.topic_out)
    return kafka_producer


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="NS3 Experiment Runner")
    parser.add_argument(
        "--configPath", type=str, metavar="FILE", help="Path to JSON config file"
    )
    args = parser.parse_args()

    # Setup full conifig
    base_cfg = safe_load_json(BASE_CFG_PTH)
    cfg = (
        merge_cofigs(base_cfg, safe_load_json(args.configPath))
        if args.configPath is not None
        else base_cfg
    )

    # Init logging
    log_manager = LogManager()
    ns3_logger = log_manager.get_logger(name="ns3", mode="job", pid=cfg.pid)

    # Handle requests
    mode = cfg.mode.lower()
    kafka_producer = _setup_kafka(cfg)
    response = None
    if mode == "simulation":
        ns3_logger.info("Simulation Request!")
        consolidated_df = run_simulation(cfg, logger=ns3_logger)
        response = (
            summary_df_to_dict(consolidated_df, cfg)
            if isinstance(consolidated_df, pd.DataFrame)
            else consolidated_df
        )

    elif mode == "list_results":
        ns3_logger.info("List Results Request!")
        results_dir = "records"
        if is_dir_nonempty(results_dir):
            ns3_logger.info("Retrieving cached results info...")
            run_entries = get_all_run_entries(results_dir=results_dir)
            response = {
                "mode": "list_results",
                "status": "success",
                "num_cached": len(run_entries),
                "runs": run_entries,
            }
        else:
            ns3_logger.info("Results Directory empty...")
            response = {
                "mode": "list_results",
                "status": "empty",
                "num_cached": 0,
                "runs": [],
            }

    ns3_logger.info(f"Response: {response}")
    kafka_producer.send(cfg.topic_out, response)
    sys.exit(0 if response is not None else 1)
