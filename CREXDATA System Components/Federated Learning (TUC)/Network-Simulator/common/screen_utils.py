import subprocess
import logging


def start_screen_session(name, command=None):
    """Starts a new detached screen session. If command is None, an empty session is created."""
    try:
        if command:
            subprocess.run(
                ["screen", "-L", "-dmS", name, "bash", "-c", f"{command}; exec bash"]
            )
        else:
            subprocess.run(["screen", "-dmS", name])  # Start an empty screen session
        logging.info(f"Started screen session {name}")
    except Exception as e:
        logging.error(f"Failed to start screen session {name}: {e}")


def start_screen_terminal(session_name, terminal_name, command):
    """Starts a new terminal inside an existing screen session."""
    try:
        subprocess.run(
            [
                "screen",
                "-S",
                session_name,
                "-X",
                "screen",
                "-t",
                terminal_name,
                "bash",
                "-c",
                f"{command}; exec bash",
            ]
        )
    except Exception as e:
        logging.error(
            f"Failed to start terminal {terminal_name} in session {session_name}: {e}"
        )


def kill_screen_session(session_name):
    """Terminates a screen session."""
    subprocess.run(
        ["screen", "-S", session_name, "-X", "quit"], stderr=subprocess.DEVNULL
    )
    logging.info(f"Terminated screen session {session_name}.")
