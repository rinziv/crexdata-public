from datetime import datetime
import os
import sys
import math
import glob
import logging
from typing import Iterable, Union

import json
import pandas as pd
from omegaconf import OmegaConf, DictConfig

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from common.utils import (
    extract_cfg_fields,
    safe_load_json,
    extract_nested_value,
    cleanup_files,
    get_hash_fingerprint,
)


# **************** Config Helper ****************#


def merge_cofigs(
    base_cfg: DictConfig, override_cfg: Union[dict, DictConfig]
) -> DictConfig:
    """Merge base config with overrides and resolve derived fields."""
    override_flat = translate_to_base_format(override_cfg)
    cfg = OmegaConf.merge(base_cfg, override_flat)
    if cfg.sync_method is not None and cfg.sync_method != "":
        sizes = cfg.pop("monitor_msg_sizes")
        cfg["monitor_msg_size"] = sizes[cfg.sync_method]
    return cfg


def prepare_round_configs(cfg, start_round=1):
    configs = []
    for roundNo in range(start_round, cfg.num_rounds + 1):
        round_cfg = OmegaConf.create(OmegaConf.to_container(cfg, resolve=True))
        round_cfg.roundNo = roundNo
        round_cfg.experiment_dir = cfg.experiment_dir
        configs.append(round_cfg)
    return configs


# **************** Dir Management Helper ****************#
def create_experiment_dir(cfg: DictConfig, output_dir: str = "records") -> str:
    """Create and return a directory path based on config parameters."""
    experiment_name = get_experiment_name(cfg)
    experiment_dir = os.path.join(output_dir, experiment_name)
    os.makedirs(experiment_dir, exist_ok=True)
    store_config(cfg, experiment_dir)
    return experiment_dir


def get_experiment_name(cfg: DictConfig) -> str:
    """Generate deterministic directory name from config contents."""
    model_size_ceiled = math.ceil(cfg.model_size)
    mobile = "mob" if cfg.client_mobility == True else "stat"
    perf = (
        "fast"
        if cfg.network_standard == 2
        else "basic" if cfg.network_standard == 1 else "slow"
    )
    descriptor = (
        f"{cfg.sync_method}_{model_size_ceiled}Mb_"
        f"{cfg.num_clients}clients_{cfg.max_packet_size}packet_"
        f"{cfg.tx_gain}txGain_{cfg.server_datarate}rate_{perf}_{mobile}"
    )
    topo_fingerprint = get_hash_fingerprint(cfg, length=10, filter={"topology"})
    return f"{descriptor}_{topo_fingerprint}"


# **************** CRUD Helper  ****************#
# * Create/Update


def store_config(
    cfg: DictConfig, experiment_dir: str, filter: Iterable[str] = None
) -> str:
    """Store essential experiment parameters to a JSON file."""
    essential_keys = (
        {
            "sync_method",
            "num_clients",
            "network_standard",
            "max_packet_size",
            "client_mobility",
            "tx_gain",
            "model_size",
            "server_datarate",
            "topology",
        }
        if filter is None
        else filter
    )
    extracted_dict = extract_cfg_fields(cfg, essential_keys)
    cfg_path = os.path.join(experiment_dir, "config.json")
    if not os.path.exists(cfg_path):
        with open(cfg_path, "w", encoding="utf-8") as f:
            json.dump(extracted_dict, f, indent=2)
    return cfg_path


def update_config_rounds(config_path: str, unique_rounds: int):
    """Update config.json with total number of rounds."""
    with open(config_path, "r") as f:
        config = json.load(f)
        config["num_rounds"] = unique_rounds
    with open(config_path, "w") as f:
        json.dump(config, f, indent=2)


def consolidate_sim_results(
    cfg: DictConfig,
    output_file: str,
    cached_df: pd.DataFrame = None,
    logger=None,
) -> Union[pd.DataFrame, None]:
    """Consolidate results and optionally merge with cache, then update config."""
    logger = logging.getLogger(__file__) if logger is None else logger

    try:
        logger.info(f"Consolidating results to {output_file}...")
        new_df = merge_round_results(cfg.experiment_dir, output_file, cleanup=True)
        ret_df = new_df

        if cached_df is not None:
            logger.info("Merging cached results with new results...")
            ret_df = pd.concat([cached_df, new_df], ignore_index=True)
            ret_df = ret_df.drop_duplicates(subset=["round", "id"])

        ret_df.to_csv(os.path.join(cfg.experiment_dir, output_file), index=False)
        logger.info(
            f"✅ Results stored at {os.path.join(cfg.experiment_dir, output_file)}"
        )
        update_config_rounds(
            config_path=os.path.join(cfg.experiment_dir, "config.json"),
            unique_rounds=ret_df["round"].nunique(),
        )
        return ret_df

    except Exception as e:
        logger.error(f"[!] Failed to consolidate results: {e}")
        return None


# * Read


def merge_round_results(
    experiment_dir: str, filename: str = "summary.csv", cleanup: bool = True
) -> Union[pd.DataFrame, None]:
    """Merge all 'round_*.csv' files into a single DataFrame."""
    assert filename.endswith((".csv", ".txt")), "File must be .csv or .txt"
    round_files = sorted(glob.glob(os.path.join(experiment_dir, "round_*.csv")))
    if not round_files:
        return None

    all_round_data = []
    for round_file in round_files:
        try:
            df = pd.read_csv(round_file)
            all_round_data.append(df)
        except Exception as e:
            raise RuntimeError(f"Failed to read '{round_file}': {e}")

    combined_df = pd.concat(all_round_data, ignore_index=True)
    combined_df.sort_values(by=["round", "id"], inplace=True)

    if cleanup:
        cleanup_files(round_files)
    return combined_df


def use_cache(
    output_pth: str, force_log: bool = False, logger=None
) -> Union[pd.DataFrame, None]:
    """Load cached CSV file if it exists and force_log is False."""
    logger = logging.getLogger(__file__) if logger is None else logger

    if force_log:
        return None
    if os.path.exists(output_pth):
        logger.info("📦 Results already exist for this configuration!")
        try:
            cached = pd.read_csv(output_pth)
            logger.info(f"✅ Cached results loaded from {output_pth}")
            return cached
        except Exception as e:
            logger.error(f"[!] Failed to load cached results: {e}")
            return None
    return None


def get_all_run_entries(results_dir: str) -> list[dict]:
    """Retrieve all run entries and associated config data."""
    entries = []
    for name in os.listdir(results_dir):
        run_path = os.path.join(results_dir, name)
        if os.path.isdir(run_path):
            try:
                config = safe_load_json(os.path.join(run_path, "config.json"))
                num_rounds = config.pop("num_rounds")
                entries.append(
                    {"name": name, "num_rounds_cached": num_rounds, "config": config}
                )
            except Exception as e:
                raise (f"Warning: Skipped {name} due to error: {e}")
    return entries


# **************** Transformations Helper ****************#
def summary_df_to_dict(df: pd.DataFrame, cfg: DictConfig) -> dict:
    """Convert round-based summary DataFrame to nested dictionary with metadata."""
    if isinstance(cfg, DictConfig):
        cfg = OmegaConf.to_container(cfg, resolve=True)

    json_data = {
        "metadata": {
            "timestamp": datetime.now().isoformat(),
            "rounds": int(df["round"].nunique()),
            "clients": int(df["id"].nunique()),
            "config": cfg,
        },
        "rounds": [],
    }
    for round_no, round_df in df.groupby("round"):
        json_data["rounds"].append(
            {
                "round": int(round_no),
                "records": round_df.drop(columns="round").to_dict(orient="records"),
            }
        )
    return json_data


def translate_to_base_format(json_data: Union[dict, DictConfig]) -> DictConfig:
    """Map nested JSON keys to a flat structure aligned with YAML schema."""

    mapping = {
        "pid": "pid",
        "mode": "mode",
        "federated_learning.model_size": "model_size",
        "federated_learning.total_rounds": "num_rounds",
        "federated_learning.num_clients": "num_clients",
        "federated_learning.clients_per_round": "clients_per_round",
        "federated_learning.sync_method": "sync_method",
        "simulation.parallelism": "num_cpus",
        "simulation.forceRun": "forceLog",
        "output.topic_out": "topic_out",
        "output.kafka_broker.ip": "broker_ip",
        "output.kafka_broker.port": "broker_port",
        "network.server_datarate": "server_datarate",
        "network.client_mobility": "client_mobility",
        "network.max_packet_size": "max_packet_size",
        "network.tx_gain": "tx_gain",
        "network.wifi_performance_standard": "network_standard",
        "network.topology": "topology",
    }

    transformed = {}
    for src, dst in mapping.items():
        val = extract_nested_value(json_data, src)
        if val is not None:
            transformed[dst] = val

    if "network_standard" in transformed:
        level = transformed["network_standard"].lower()
        transformed["network_standard"] = (
            0 if level == "low" else 1 if level == "basic" else 2
        )

    if "sync_method" in transformed:
        transformed["sync_method"] = transformed["sync_method"].lower()

    return OmegaConf.create(transformed)
