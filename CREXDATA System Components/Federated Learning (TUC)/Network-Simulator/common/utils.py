import hashlib
import os
import json
import shutil
import subprocess
from typing import Iterable, Union
import tempfile
from omegaconf import DictConfig, OmegaConf


def safe_load_json(filename: str) -> dict:
    """
    Safely load a JSON file from disk.

    Raises:
        FileNotFoundError: If the file does not exist.
        ValueError: If the file is empty or the contents are not valid JSON.
    """
    if not os.path.exists(filename):
        raise FileNotFoundError(f"File not found: {filename}")

    try:
        with open(filename, "r", encoding="utf-8") as file:
            data = json.load(file)
            if not isinstance(data, dict):
                raise ValueError(f"JSON content is not a dictionary: {filename}")
            return data
    except json.JSONDecodeError as e:
        raise ValueError(f"Failed to parse JSON file '{filename}': {e}") from e


def safe_load_yaml(file_path: str) -> DictConfig:
    try:
        return OmegaConf.load(file_path)
    except Exception:
        return OmegaConf.create({})


def resolve_configs(cfg_val_path, cfg_to_resolve_path) -> DictConfig:
    cfg1 = safe_load_yaml(cfg_val_path)
    cfg2 = safe_load_yaml(cfg_to_resolve_path)
    merged = OmegaConf.merge(cfg1, cfg2)
    try:
        resolved = OmegaConf.to_container(merged, resolve=True)
    except Exception as e:
        print(e)
    print(resolved)
    return


def extract_cfg_fields(cfg: Union[dict, DictConfig], filter: set) -> dict:
    """
    Extract a subset of keys from a configuration object (dict or DictConfig) and resolve nested values.
    """
    extracted_dict = {k: cfg[k] for k in filter if k in cfg}

    # If the original cfg is a DictConfig, ensure values are fully resolved
    return (
        OmegaConf.to_container(OmegaConf.create(extracted_dict), resolve=True)
        if isinstance(cfg, DictConfig)
        else extracted_dict
    )


def extract_nested_value(data: DictConfig, nested_path: str) -> DictConfig:
    for key in nested_path.split("."):
        data = data.get(key)
        if data is None:
            return None
    return data


def is_dir_nonempty(dir: str) -> bool:
    """Check if the results directory exists and has subdirectories."""
    return os.path.exists(dir) and any(
        os.path.isdir(os.path.join(dir, d)) for d in os.listdir(dir)
    )


def cleanup_files(all_files: list):
    """Delete list of round_*.csv files from disk."""
    for file in all_files:
        os.remove(file)


def get_hash_fingerprint(
    cfg: DictConfig, length: int = 10, filter: Iterable[str] = None
) -> str:
    """Generate SHA256 hash fingerprint for selected config keys."""
    length = min(length, 255)
    if isinstance(cfg, DictConfig):
        cfg = OmegaConf.to_container(cfg, resolve=True)
    sanitized_cfg = {k: v for k, v in cfg.items() if k in filter} if filter else cfg
    cfg_str = json.dumps(sanitized_cfg, sort_keys=True)
    return hashlib.sha256(cfg_str.encode()).hexdigest()[:length]


def launch_process(script, config_path=None, cwd=None, text_logs=False):
    try:
        if isinstance(script, list):
            cmd = script  # custom command like ./waf ...
        elif isinstance(script, str):
            cmd = ["python", script]
            if config_path:
                cmd.extend(["--configPath", config_path])
        else:
            raise TypeError("script must be a str or list")

        env = None
        if cwd is not None:
            env = os.environ.copy()
            env["PYTHONPATH"] = os.path.abspath(cwd)

        return subprocess.Popen(
            cmd,
            text=text_logs,
            bufsize=1 if text_logs else -1,
            stdout=subprocess.PIPE if text_logs else None,
            stderr=subprocess.STDOUT if text_logs else None,
            cwd=cwd,
            env=env,
        )
    except Exception as e:
        print(f"Exception Launching process: {e}")


def write_temp_config(cfg: dict) -> str:
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as tmp:
        json.dump(cfg, tmp, indent=4)
        return tmp.name


def cleanup_logs(logs_dir="logs"):
    if not os.path.isdir(logs_dir):
        return

    for name in os.listdir(logs_dir):
        path = os.path.join(logs_dir, name)
        if os.path.isdir(path):
            shutil.rmtree(path)
        else:
            os.remove(path)
