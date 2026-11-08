"""Puts one Unity test run under a fixed CPU load and counts the other Unity processes around it.

test-unity.sh runs this file as a script, with nothing but the standard library, beside each run
of a target:

    python -I fixed_load.py supervise --log <Unity log> --spinners <n> --root-pid <pid>
        --stop <file> --record <file> [--idle]

It starts <n> spinners, Python processes that wait on their standard input, and lets them spin a
CPU core each once Unity's log holds the line the Unity Test Framework writes, under -automated,
as the first test begins, so that the load does not slow the compile and the project load before
it. It stops them once every test that the run's test plan names has ended, so that the load does
not slow Unity's shutdown after it either, or else once the stop file appears or the process
<pid>, the script, ends. A spinner also ends by itself once this process ends, since its standard
input then closes, so none outlives the run. Meanwhile it counts the Unity processes that do not descend from <pid>, at the
start, every few seconds, and as it stops. It writes what happened to the record file as
key=value lines once the spinners wait, once they spin, and once they have stopped, which alone
says complete=yes.
"""

from __future__ import annotations

import argparse
import functools
import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Iterable

# How often, in seconds, the supervisor looks at the log, the stop file, and the script, and how
# often it counts the Unity processes. A spinner starts within one look of the first test's line.
POLL_SECONDS = 0.05
SAMPLE_SECONDS = 5.0
# How long the spinners have to end by themselves once their input closes before they are killed.
STOP_WAIT_SECONDS = 2.0

# A spinner reads one byte: "g" lets it spin, and the end of its input, before or after, ends it.
# A thread waits for that end while the main thread spins; idle, which only the tool tests ask
# for, waits instead of spinning.
SPINNER_PROGRAM = """\
import os, sys, threading
def end_with_input():
    sys.stdin.buffer.read()
    os._exit(0)
try:
    if sys.stdin.buffer.read(1) != b'g':
        os._exit(0)
    threading.Thread(target=end_with_input, daemon=True).start()
    if sys.argv[1:] == ['idle']:
        threading.Event().wait()
    while True:
        pass
except KeyboardInterrupt:
    os._exit(130)
"""


@dataclass(frozen=True)
class ProcessEntry:
    pid: int
    parent: int
    name: str


def default_unity_process_name() -> str:
    return "Unity.exe" if sys.platform == "win32" else "Unity"


def descendants(
    processes: Iterable[ProcessEntry], root: int, created: Callable[[int], int | None] | None = None
) -> set[int]:
    """The root and every process that descends from it.

    On Windows a process whose parent ended keeps that parent's ID, which a new process can take;
    created, which gives a process's creation time or None, keeps such a process out of the tree
    when it is older than the process whose ID it names as its parent.
    """
    children: dict[int, list[int]] = {}
    for entry in processes:
        if entry.pid != entry.parent:
            children.setdefault(entry.parent, []).append(entry.pid)

    tree = {root}
    pending = [root]
    while pending:
        parent = pending.pop()
        parent_created = created(parent) if created else None
        for child in children.get(parent, ()):
            if child in tree:
                continue
            if parent_created is not None:
                child_created = created(child) if created else None
                if child_created is not None and child_created < parent_created:
                    continue
            tree.add(child)
            pending.append(child)
    return tree


def count_other_processes(
    processes: list[ProcessEntry],
    root: int,
    name: str,
    created: Callable[[int], int | None] | None = None,
) -> int:
    """How many processes of that name, compared without case, do not descend from the root."""
    tree = descendants(processes, root, created)
    wanted = name.casefold()
    return sum(1 for entry in processes if entry.name.casefold() == wanted and entry.pid not in tree)


@functools.lru_cache(maxsize=None)
def _windows_api():
    import ctypes
    from ctypes import wintypes

    class ProcessEntry32W(ctypes.Structure):
        _fields_ = [
            ("dwSize", wintypes.DWORD),
            ("cntUsage", wintypes.DWORD),
            ("th32ProcessID", wintypes.DWORD),
            ("th32DefaultHeapID", ctypes.c_size_t),
            ("th32ModuleID", wintypes.DWORD),
            ("cntThreads", wintypes.DWORD),
            ("th32ParentProcessID", wintypes.DWORD),
            ("pcPriClassBase", wintypes.LONG),
            ("dwFlags", wintypes.DWORD),
            ("szExeFile", wintypes.WCHAR * 260),
        ]

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    for function in (kernel32.Process32FirstW, kernel32.Process32NextW):
        function.argtypes = [wintypes.HANDLE, ctypes.POINTER(ProcessEntry32W)]
        function.restype = wintypes.BOOL
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel32.WaitForSingleObject.restype = wintypes.DWORD
    kernel32.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
    kernel32.GetProcessTimes.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    return ctypes, wintypes, kernel32, ProcessEntry32W


def _windows_processes() -> list[ProcessEntry]:
    ctypes, _wintypes, kernel32, ProcessEntry32W = _windows_api()
    snapshot = kernel32.CreateToolhelp32Snapshot(0x00000002, 0)  # TH32CS_SNAPPROCESS
    if not snapshot or snapshot == ctypes.c_void_p(-1).value:
        raise OSError(ctypes.get_last_error(), "could not list the processes")
    try:
        entry = ProcessEntry32W()
        entry.dwSize = ctypes.sizeof(ProcessEntry32W)
        processes = []
        found = kernel32.Process32FirstW(snapshot, ctypes.byref(entry))
        while found:
            processes.append(ProcessEntry(entry.th32ProcessID, entry.th32ParentProcessID, entry.szExeFile))
            found = kernel32.Process32NextW(snapshot, ctypes.byref(entry))
        return processes
    finally:
        kernel32.CloseHandle(snapshot)


def _windows_creation_time(pid: int) -> int | None:
    ctypes, wintypes, kernel32, _entry = _windows_api()
    handle = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
    if not handle:
        return None
    try:
        times = [wintypes.FILETIME() for _ in range(4)]
        if not kernel32.GetProcessTimes(handle, *(ctypes.byref(item) for item in times)):
            return None
        return (times[0].dwHighDateTime << 32) | times[0].dwLowDateTime
    finally:
        kernel32.CloseHandle(handle)


def _posix_processes() -> list[ProcessEntry]:
    listing = subprocess.run(
        ["ps", "-A", "-o", "pid=", "-o", "ppid=", "-o", "comm="],
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    processes = []
    for line in listing.splitlines():
        parts = line.split(None, 2)
        if len(parts) == 3 and parts[0].isdigit() and parts[1].isdigit():
            # macOS names a process by the path of its executable, Linux by its name.
            processes.append(ProcessEntry(int(parts[0]), int(parts[1]), os.path.basename(parts[2].strip())))
    return processes


def list_processes() -> list[ProcessEntry]:
    return _windows_processes() if sys.platform == "win32" else _posix_processes()


def creation_time_reader() -> Callable[[int], int | None] | None:
    """What descendants needs to tell a stale parent ID, on Windows; other hosts never reuse one
    while it is a parent's, since an orphan takes a new parent there."""
    if sys.platform != "win32":
        return None
    cache: dict[int, int | None] = {}

    def created(pid: int) -> int | None:
        if pid not in cache:
            cache[pid] = _windows_creation_time(pid)
        return cache[pid]

    return created


class ProcessWatch:
    """Tells whether a process still runs, holding it open on Windows so that its ID cannot pass
    to another process meanwhile. Windows' os.kill ends a process whatever the signal, so it is
    not used there."""

    def __init__(self, pid: int) -> None:
        self.pid = pid
        self._handle = None
        if sys.platform == "win32":
            _ctypes, _wintypes, self._kernel32, _entry = _windows_api()
            self._handle = self._kernel32.OpenProcess(0x00100000, False, pid)  # SYNCHRONIZE

    def running(self) -> bool:
        if sys.platform == "win32":
            if not self._handle:
                return False
            return self._kernel32.WaitForSingleObject(self._handle, 0) == 0x00000102  # WAIT_TIMEOUT
        try:
            os.kill(self.pid, 0)
        except ProcessLookupError:
            return False
        except PermissionError:
            return True
        return True

    def close(self) -> None:
        if self._handle:
            self._kernel32.CloseHandle(self._handle)
            self._handle = None


def process_running(pid: int) -> bool:
    watch = ProcessWatch(pid)
    try:
        return watch.running()
    finally:
        watch.close()


class TestLog:
    """Reads the lines a Unity log gains, keeping how many tests the run's plan names, the first
    test that began and when, in milliseconds since the epoch, and how many tests ended."""

    def __init__(self, path: Path) -> None:
        self.path = path
        self._offset = 0
        self._partial = b""
        self.planned: int | None = None
        self.first_test: str | None = None
        self.first_test_time: int | None = None
        self.ended = 0

    def read(self) -> None:
        try:
            with self.path.open("rb") as log:
                log.seek(self._offset)
                chunk = log.read()
        except OSError:
            return
        self._offset += len(chunk)
        *lines, self._partial = (self._partial + chunk).split(b"\n")
        for line in lines:
            self._read_line(line.decode("utf-8", "replace"))

    def _read_line(self, line: str) -> None:
        marker = line.find("##utp:")
        if marker < 0:
            return
        try:
            message = json.loads(line[marker + len("##utp:") :])
        except ValueError:
            return
        if not isinstance(message, dict):
            return
        if message.get("type") == "TestPlan" and isinstance(message.get("tests"), list):
            self.planned = len(message["tests"])
        if message.get("type") != "TestStatus":
            return
        if message.get("phase") == "Begin" and self.first_test is None:
            self.first_test = str(message.get("name", ""))
            began = message.get("time")
            self.first_test_time = began if isinstance(began, int) else None
        elif message.get("phase") == "End":
            self.ended += 1

    def all_planned_ended(self) -> bool:
        return self.planned is not None and self.planned > 0 and self.ended >= self.planned


@dataclass
class Record:
    """What one supervised run writes to its record file."""

    spinners: int
    cpus: int = field(default_factory=lambda: os.cpu_count() or 0)
    load: str = "none"
    first_test: str = ""
    tests_ended_before_load: int = 0
    load_seconds: float = 0.0
    # How long after the first test began the spinners were let spin, from the time in the log's
    # line, and what ended the load: the last planned test, or the end of the run.
    load_lag_ms: int | None = None
    load_ended: str = ""
    others_at_start: int | None = None
    others_during: list[int] = field(default_factory=list)
    count_error: str = ""
    spinner_pids: list[int] = field(default_factory=list)
    spinners_left: list[int] = field(default_factory=list)
    error: str = ""
    # Written while the run goes on too, so that the spinners can be found; only the last has it.
    complete: bool = False

    def lines(self) -> list[str]:
        values: dict[str, object] = {
            "spinners": self.spinners,
            "cpus": self.cpus,
            "load": self.load,
            "first_test": self.first_test,
            "tests_ended_before_load": self.tests_ended_before_load,
            "load_seconds": f"{self.load_seconds:.1f}",
            "load_lag_ms": "" if self.load_lag_ms is None else self.load_lag_ms,
            "load_ended": self.load_ended,
        }
        if self.others_at_start is not None:
            values["others_at_start"] = self.others_at_start
        if self.others_during:
            values["others_during_min"] = min(self.others_during)
            values["others_during_max"] = max(self.others_during)
            values["looks"] = len(self.others_during)
        values.update(
            {
                "count_error": self.count_error,
                "spinner_pids": " ".join(map(str, self.spinner_pids)),
                "spinners_left": " ".join(map(str, self.spinners_left)),
                "error": self.error,
                "complete": "yes" if self.complete else "no",
            }
        )
        # A value stays on its line, whatever a test name or an error message holds.
        return [f"{key}={' '.join(str(value).split())}" for key, value in values.items()]

    def write(self, path: Path) -> None:
        temporary = path.with_name(path.name + ".tmp")
        temporary.write_text("".join(f"{line}\n" for line in self.lines()), encoding="utf-8", newline="\n")
        # Windows refuses for a moment to replace a file that another process has open, as a
        # reader of the record may.
        for attempt in range(10):
            try:
                os.replace(temporary, path)
                return
            except PermissionError:
                if attempt == 9:
                    raise
                time.sleep(0.02)

    def write_while_running(self, path: Path) -> None:
        """Writes the record of a run that goes on, which only helps to find the spinners."""
        try:
            self.write(path)
        except OSError:
            pass


def load_lag(first_test_time: int | None) -> int | None:
    """Milliseconds from the first test's start, as its line in the log dates it, to now, or None
    when the line gave no date this clock can read."""
    if first_test_time is None:
        return None
    lag = round(time.time() * 1000) - first_test_time
    return lag if 0 <= lag <= 600_000 else None


def start_spinners(count: int, idle: bool) -> list[subprocess.Popen[bytes]]:
    command = [sys.executable, "-I", "-c", SPINNER_PROGRAM, *(["idle"] if idle else [])]
    spinners: list[subprocess.Popen[bytes]] = []
    try:
        for _ in range(count):
            spinners.append(
                subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            )
    except OSError:
        stop_spinners(spinners)
        raise
    return spinners


def release_spinners(spinners: list[subprocess.Popen[bytes]]) -> list[int]:
    """Lets every spinner spin, and returns those that had already ended."""
    ended = []
    for spinner in spinners:
        try:
            if spinner.stdin is None:
                raise OSError("the spinner has no input")
            spinner.stdin.write(b"g")
            spinner.stdin.flush()
        except OSError:
            ended.append(spinner.pid)
    return ended


def stop_spinners(spinners: list[subprocess.Popen[bytes]], wait_seconds: float = STOP_WAIT_SECONDS) -> list[int]:
    """Ends every spinner, closing its input first and killing it when that is not enough, and
    returns those still running afterwards."""
    for spinner in spinners:
        try:
            if spinner.stdin is not None:
                spinner.stdin.close()
        except OSError:
            pass
    deadline = time.monotonic() + wait_seconds
    for spinner in spinners:
        try:
            spinner.wait(timeout=max(0.0, deadline - time.monotonic()))
        except subprocess.TimeoutExpired:
            spinner.kill()
    left = []
    for spinner in spinners:
        try:
            spinner.wait(timeout=5)
        except subprocess.TimeoutExpired:
            left.append(spinner.pid)
    return left


def supervise(
    log: Path,
    spinner_count: int,
    root_pid: int,
    stop: Path,
    record_path: Path,
    idle: bool = False,
) -> Record:
    record = Record(spinners=spinner_count)
    name = default_unity_process_name()
    created = creation_time_reader()
    root = ProcessWatch(root_pid)
    test_log = TestLog(log)
    spinners: list[subprocess.Popen[bytes]] = []
    released_at: float | None = None
    stopped_at: float | None = None

    def count_others() -> int | None:
        try:
            return count_other_processes(list_processes(), root_pid, name, created)
        except (OSError, subprocess.SubprocessError) as error:
            record.count_error = f"could not list the processes: {error}"
            return None

    try:
        record.others_at_start = count_others()
        if spinner_count > 0:
            spinners = start_spinners(spinner_count, idle)
            record.spinner_pids = [spinner.pid for spinner in spinners]
            record.load = "waiting"
            record.write_while_running(record_path)
        next_sample = time.monotonic() + SAMPLE_SECONDS

        while not stop.exists():
            if not root.running():
                record.error = "the script ended before it stopped the load"
                break
            if spinners and released_at is None:
                test_log.read()
                if test_log.first_test is not None:
                    ended = release_spinners(spinners)
                    released_at = time.monotonic()
                    record.load = "started"
                    record.first_test = test_log.first_test
                    record.tests_ended_before_load = test_log.ended
                    record.load_lag_ms = load_lag(test_log.first_test_time)
                    if ended:
                        record.error = f"{len(ended)} spinner(s) ended before the load started"
                    record.write_while_running(record_path)
            elif spinners and stopped_at is None:
                test_log.read()
            # The last planned test can end in the same read that lets the spinners spin.
            if released_at is not None and stopped_at is None and test_log.all_planned_ended():
                stopped_at = time.monotonic()
                record.load_ended = "last-test"
                record.spinners_left = stop_spinners(spinners)
                record.write_while_running(record_path)
            if time.monotonic() >= next_sample:
                others = count_others()
                if others is not None:
                    record.others_during.append(others)
                if released_at is not None and stopped_at is None and not record.error:
                    ended = [spinner.pid for spinner in spinners if spinner.poll() is not None]
                    if ended:
                        record.error = f"{len(ended)} spinner(s) ended while the run went on"
                next_sample += SAMPLE_SECONDS
            time.sleep(POLL_SECONDS)
    except KeyboardInterrupt:
        record.error = record.error or "interrupted"
    except OSError as error:
        record.error = record.error or f"could not start the spinners: {error}"
    finally:
        # The last count is taken as the run ends, so even a short run has one.
        others = count_others()
        if others is not None:
            record.others_during.append(others)
        if spinners and released_at is None:
            test_log.read()
            record.load = "none" if test_log.first_test is None else "missed"
            record.first_test = test_log.first_test or ""
        if released_at is not None:
            record.load_seconds = (stopped_at or time.monotonic()) - released_at
            record.load_ended = record.load_ended or "run-end"
        if stopped_at is None:
            record.spinners_left = stop_spinners(spinners)
        if record.spinners_left and not record.error:
            record.error = f"{len(record.spinners_left)} spinner(s) could not be stopped"
        root.close()
        record.complete = True
        record.write(record_path)
    return record


def main(arguments: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)
    run = commands.add_parser("supervise", help="Load one test run and count the other Unity processes.")
    run.add_argument("--log", type=Path, required=True)
    run.add_argument("--spinners", type=int, required=True)
    run.add_argument("--root-pid", type=int, required=True)
    run.add_argument("--stop", type=Path, required=True)
    run.add_argument("--record", type=Path, required=True)
    run.add_argument("--idle", action="store_true")
    options = parser.parse_args(arguments)

    if options.spinners < 0:
        parser.error("--spinners expects a whole number")
    record = supervise(options.log, options.spinners, options.root_pid, options.stop, options.record, options.idle)
    return 1 if record.error else 0


if __name__ == "__main__":
    sys.exit(main())
