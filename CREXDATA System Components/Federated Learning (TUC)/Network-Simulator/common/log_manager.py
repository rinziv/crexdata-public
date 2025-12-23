import logging
import os
import subprocess
from datetime import datetime
from pathlib import Path


class LogManager:
    def __init__(self, root_dir=None, capacity=100):
        self.root_dir = (
            Path(__file__).resolve().parent.parent / "logs"
            if root_dir is None
            else Path(root_dir).resolve()
        )
        self.capacity = capacity
        self.logger = None
        self.root_dir.mkdir(parents=True, exist_ok=True)

    def _create_job_dir(self, pid: str) -> Path:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        job_dir = self.root_dir / f"{timestamp}_{pid}"
        job_dir.mkdir(parents=True, exist_ok=True)
        return job_dir

    def enforce_capacity(self):
        dirs = sorted(
            [d for d in self.root_dir.iterdir() if d.is_dir()],
            key=os.path.getmtime,
        )
        # Proactive: we're about to create one more, so only keep capacity - 1
        if len(dirs) >= self.capacity:
            num_to_delete = len(dirs) - (self.capacity - 1)
            for d in dirs[:num_to_delete]:
                try:
                    subprocess.run(["rm", "-rf", str(d)], check=True)
                except Exception as e:
                    print(f"[LogManager] Could not delete {d}: {e}")

    def get_logger(
        self,
        name: str,
        mode="global",
        pid: str = None,
        log_level=logging.INFO,
    ) -> logging.Logger:
        """
        :param name: e.g. 'mediator', 'server', 'client_0', 'ns3'
        :param mode: 'global' | 'job' |
        :param pid: required for mode != 'global'
        """
        if self.logger is not None:
            return self.logger

        write_mode = "a"
        if mode == "global":
            log_path = self.root_dir / "mediator.log"
            write_mode = "w"
        elif mode == "job":
            if pid is None:
                raise ValueError(f"PID is required for mode='{mode}'")
            job_dir = self._create_job_dir(pid)
            log_path = job_dir / f"{name}.log"
        else:
            raise ValueError(f"Unknown log mode: {mode}")

        logger = logging.getLogger(name)
        logger.setLevel(log_level)
        logger.propagate = False  # avoid duplicate output

        formatter = logging.Formatter(
            "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
        )

        file_handler = logging.FileHandler(str(log_path), mode=write_mode)
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)

        # Only print to console for the global mediator logger
        if mode == "global":
            console_handler = logging.StreamHandler()
            console_handler.setFormatter(formatter)
            logger.addHandler(console_handler)

        return logger

    def get_job_log_dir(self, pid: str) -> Path:
        """
        Returns the full path to the directory logs/{timestamp}_{pid}.
        WARNING: Only call this after you've created a logger for this pid.
        """
        matching_dirs = sorted(
            [d for d in self.root_dir.iterdir() if d.is_dir() and d.name.endswith(pid)],
            key=os.path.getmtime,
            reverse=True,
        )
        return matching_dirs[0] if matching_dirs else None
