from __future__ import annotations

import platform
import subprocess
from pathlib import Path


def stop_process_tree(process: subprocess.Popen) -> None:
    """Stop a process a tool started and the processes it started, such as Unity's build workers."""
    if platform.system() == "Windows":
        try:
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(process.pid)], capture_output=True, check=False)
        except OSError:
            # Without taskkill only the process itself stops below; the run still reports the timeout
            # rather than this error.
            pass
    process.kill()
    process.wait()


def run_with_timeout(command: list[str], timeout: float, cwd: Path | None = None) -> int | None:
    """Run a command and return its exit code, or None after stopping it at the timeout."""
    process = subprocess.Popen(command, cwd=cwd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        return process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        stop_process_tree(process)
        return None
    except BaseException:
        stop_process_tree(process)
        raise
