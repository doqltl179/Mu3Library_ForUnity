from __future__ import annotations

import platform
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

from mu3_common.process import run_with_timeout


# Starts a child that sleeps, writes the child's PID to the file it is given, and sleeps too, as
# Unity starts its import workers and build tools.
PARENT_WITH_CHILD = """
import subprocess, sys, time
child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
with open(sys.argv[1], "w") as pid_file:
    pid_file.write(str(child.pid))
time.sleep(60)
"""


def windows_process_alive(pid: int) -> bool:
    listed = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"], capture_output=True, text=True, check=False)
    return str(pid) in listed.stdout


class RunWithTimeoutTests(unittest.TestCase):
    def test_returns_the_exit_code_of_a_command_that_ends_in_time(self) -> None:
        self.assertEqual(3, run_with_timeout([sys.executable, "-c", "import sys; sys.exit(3)"], 60))

    def test_stops_a_command_at_the_timeout_and_returns_none(self) -> None:
        started = time.monotonic()

        status = run_with_timeout([sys.executable, "-c", "import time; time.sleep(60)"], 0.5)

        self.assertIsNone(status)
        self.assertLess(time.monotonic() - started, 30)

    @unittest.skipUnless(platform.system() == "Windows", "taskkill /T stops the tree only on Windows")
    def test_stops_the_processes_the_command_started_too(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            pid_file = Path(folder) / "child.pid"
            child: int | None = None
            try:
                status = run_with_timeout([sys.executable, "-c", PARENT_WITH_CHILD, str(pid_file)], 5)
                child = int(pid_file.read_text(encoding="utf-8"))

                self.assertIsNone(status)
                self.assertFalse(windows_process_alive(child), f"the child {child} still runs")
            finally:
                if child is not None and windows_process_alive(child):
                    subprocess.run(["taskkill", "/F", "/PID", str(child)], capture_output=True, check=False)


if __name__ == "__main__":
    unittest.main()
