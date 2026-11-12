from __future__ import annotations

import os
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from datetime import datetime
from pathlib import Path
from unittest import mock

from mu3_cli import fixed_load
from mu3_cli.fixed_load import DataStore, ProcessEntry, Record, TestLog, count_other_processes, descendants
from mu3_cli.repository import repo_root

HELPER = repo_root() / "tools" / "cli" / "src" / "mu3_cli" / "fixed_load.py"


def utp(phase: str, name: str = "Fixture.Tests.Times") -> str:
    """A line the Unity Test Framework logs, with -automated, as a test begins or ends."""
    return f'##utp:{{"type":"TestStatus","time":1,"version":2,"phase":"{phase}","processId":1,"name":"{name}","state":0}}'


def wait_until(condition, seconds: float = 10.0) -> bool:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if condition():
            return True
        time.sleep(0.05)
    return condition()


class ProcessTreeTests(unittest.TestCase):
    PROCESSES = [
        ProcessEntry(1, 0, "init"),
        ProcessEntry(10, 1, "bash.exe"),
        ProcessEntry(11, 10, "Unity.exe"),
        ProcessEntry(12, 11, "Unity.exe"),
        ProcessEntry(13, 10, "python.exe"),
        ProcessEntry(20, 1, "Unity.exe"),
        ProcessEntry(21, 20, "unity.EXE"),
        # A process whose parent ended keeps that parent's ID, here one of the root's tree.
        ProcessEntry(30, 13, "Unity.exe"),
    ]

    def test_the_tree_holds_the_root_and_its_descendants(self) -> None:
        self.assertEqual({10, 11, 12, 13, 30}, descendants(self.PROCESSES, 10))

    def test_a_process_older_than_the_parent_it_names_is_not_in_the_tree(self) -> None:
        created = {10: 100, 11: 110, 12: 120, 13: 130, 30: 50}

        self.assertEqual({10, 11, 12, 13}, descendants(self.PROCESSES, 10, created.get))

    def test_unity_processes_outside_the_tree_are_counted_without_regard_to_case(self) -> None:
        self.assertEqual(2, count_other_processes(self.PROCESSES, 10, "Unity.exe"))
        self.assertEqual(3, count_other_processes(self.PROCESSES, 10, "Unity.exe", {13: 130, 30: 50}.get))
        self.assertEqual(5, count_other_processes(self.PROCESSES, 99, "Unity.exe"))

    def test_each_count_reads_the_creation_times_afresh(self) -> None:
        # A short-lived child of the root, created between the root and its Unity, ends, and its
        # ID passes to a new import worker of that Unity. A creation time kept from the first
        # count would make the worker older than its parent and leave it out of the tree.
        times = {10: 100, 11: 110, 13: 105}
        first = [ProcessEntry(10, 1, "bash.exe"), ProcessEntry(11, 10, "Unity.exe"), ProcessEntry(13, 10, "bash.exe")]
        second = [ProcessEntry(10, 1, "bash.exe"), ProcessEntry(11, 10, "Unity.exe"), ProcessEntry(13, 11, "Unity.exe")]

        def reader():
            cache: dict[int, int | None] = {}

            def created(pid: int) -> int | None:
                if pid not in cache:
                    cache[pid] = times.get(pid)
                return cache[pid]

            return created

        with mock.patch.object(fixed_load, "list_processes", side_effect=[first, second]), mock.patch.object(
            fixed_load, "creation_time_reader", side_effect=reader
        ):
            self.assertEqual(0, fixed_load.count_other_unity_processes(10, "Unity.exe"))
            times[13] = 200
            self.assertEqual(0, fixed_load.count_other_unity_processes(10, "Unity.exe"))

    def test_a_process_that_names_itself_as_its_parent_ends_the_walk(self) -> None:
        processes = [ProcessEntry(0, 0, "System Idle Process"), ProcessEntry(5, 0, "Unity.exe")]

        self.assertEqual({0, 5}, descendants(processes, 0))

    def test_this_host_lists_the_current_process_under_its_parent(self) -> None:
        processes = fixed_load.list_processes()

        [current] = [entry for entry in processes if entry.pid == os.getpid()]
        self.assertEqual(os.getppid(), current.parent)
        self.assertIn(os.getpid(), descendants(processes, os.getppid(), fixed_load.creation_time_reader()))

    @unittest.skipUnless(sys.platform == "win32", "only Windows reads a service's end from a handle")
    def test_a_service_that_cannot_be_opened_has_no_end_and_says_why(self) -> None:
        # No process can be opened by the ID 0, as one refused or already gone cannot be.
        watch = fixed_load.DataStoreWatch(0)

        self.assertFalse(watch.look())
        self.assertIsNone(watch.service.ended)
        self.assertIsNone(watch.service.exit_code)
        self.assertRegex(watch.service.error, r"^could not open the process \(Windows error \d+\)$")
        self.assertIn(f"data_store_0_error={watch.service.error}", Record(spinners=0, data_stores=[watch.service]).lines())

    def test_a_running_process_is_told_from_one_that_ended(self) -> None:
        ended = subprocess.Popen([sys.executable, "-c", "pass"])
        ended.wait()

        self.assertTrue(fixed_load.process_running(os.getpid()))
        self.assertFalse(fixed_load.process_running(ended.pid))


class TestLogTests(unittest.TestCase):
    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.path = Path(directory.name) / "unity.log"

    def append(self, text: str) -> None:
        with self.path.open("a", encoding="utf-8", newline="") as log:
            log.write(text)

    def test_the_first_test_that_began_is_kept_from_a_line_read_in_parts(self) -> None:
        log = TestLog(self.path)
        log.read()
        self.assertIsNone(log.first_test)

        line = utp("Begin", 'Fixture.Tests.Case(\\"two words\\")')
        self.append("Loading the project\n" + line[:30])
        log.read()
        self.assertIsNone(log.first_test)

        self.append(line[30:] + "\r\n" + utp("Begin", "Fixture.Tests.Later") + "\n")
        log.read()
        self.assertEqual('Fixture.Tests.Case("two words")', log.first_test)
        self.assertEqual(0, log.ended)

    def test_the_plan_and_the_time_the_first_test_began_are_kept(self) -> None:
        self.append(
            '##utp:{"type":"TestPlan","time":5,"tests":["A.One","A.Two"]}\n'
            '##utp:{"type":"TestStatus","phase":"Begin","time":1794134506677,"name":"A.One"}\n'
            '##utp:{"type":"TestStatus","phase":"End","time":1794134507074,"name":"A.One"}\n'
        )
        log = TestLog(self.path)
        log.read()

        self.assertEqual(({"A.One", "A.Two"}, 1794134506677, 1), (log.planned, log.first_test_time, log.ended))
        self.assertFalse(log.all_planned_ended())
        self.append('##utp:{"type":"TestStatus","phase":"End","time":1794134507300,"name":"A.Two"}\n')
        log.read()
        self.assertTrue(log.all_planned_ended())

    def test_a_test_that_runs_again_does_not_stand_in_for_one_that_has_not_ended(self) -> None:
        # Under [Retry], [Repeat], or -repeat a test begins and ends once each time it runs.
        self.append(
            '##utp:{"type":"TestPlan","tests":["A.One","A.Two"]}\n'
            + "\n".join(utp(phase, "A.One") for phase in ("Begin", "End", "Begin", "End", "Begin", "End"))
            + "\n"
        )
        log = TestLog(self.path)
        log.read()

        self.assertEqual(3, log.ended)
        self.assertFalse(log.all_planned_ended())
        self.append(utp("Begin", "A.Two") + "\n" + utp("End", "A.Two") + "\n")
        log.read()
        self.assertTrue(log.all_planned_ended())

    def test_an_empty_plan_never_counts_as_ended(self) -> None:
        self.append('##utp:{"type":"TestPlan","tests":[]}\n' + utp("End") + "\n")
        log = TestLog(self.path)
        log.read()

        self.assertFalse(log.all_planned_ended())

    def test_a_run_without_a_plan_never_counts_as_ended(self) -> None:
        self.append(utp("Begin") + "\n" + utp("End") + "\n")
        log = TestLog(self.path)
        log.read()

        self.assertIsNone(log.planned)
        self.assertFalse(log.all_planned_ended())

    def test_the_tests_that_ended_are_counted_and_other_lines_are_left_out(self) -> None:
        self.append(
            "\n".join(
                [
                    utp("Begin"),
                    '##utp:{"type":"LogEntry","phase":"End"}',
                    "##utp:{not json",
                    utp("End"),
                    utp("End", "Fixture.Tests.Other"),
                    "",
                ]
            )
        )
        log = TestLog(self.path)
        log.read()

        self.assertEqual("Fixture.Tests.Times", log.first_test)
        self.assertEqual(2, log.ended)


class RecordTests(unittest.TestCase):
    def test_every_value_stays_on_its_line(self) -> None:
        record = Record(spinners=2, first_test="A\nB", error="broke\r\nhere", others_at_start=1, others_during=[0, 3])

        lines = record.lines()

        self.assertIn("first_test=A B", lines)
        self.assertIn("error=broke here", lines)
        self.assertIn("others_during_min=0", lines)
        self.assertIn("others_during_max=3", lines)
        self.assertIn("looks=2", lines)
        self.assertTrue(all("\n" not in line and "\r" not in line for line in lines))

    def test_each_data_store_service_has_its_start_its_end_and_its_exit_code(self) -> None:
        started = 1_794_000_000.0
        record = Record(
            spinners=0,
            data_stores=[DataStore(13480, started=started, ended=started + 16.25, exit_code=3221225477), DataStore(13490)],
        )

        lines = record.lines()

        local = datetime.fromtimestamp(started + 16.25).astimezone().isoformat(timespec="milliseconds")
        self.assertIn("data_store_pids=13480 13490", lines)
        self.assertIn(f"data_store_13480_ended={local}", lines)
        self.assertIn("data_store_13480_exit_code=3221225477", lines)
        # A service that still runs, or whose start could not be read, has neither yet.
        self.assertIn("data_store_13490_started=", lines)
        self.assertIn("data_store_13490_ended=", lines)
        self.assertIn("data_store_13490_exit_code=", lines)
        self.assertIn("data_store_pids=", Record(spinners=0).lines())


class SupervisorTests(unittest.TestCase):
    """Runs the helper as test-unity.sh runs it, with spinners that wait instead of spinning."""

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.directory = Path(directory.name)
        self.log = self.directory / "run.log"
        self.stop = self.directory / "run.load-stop"
        self.record_file = self.directory / "run.load"

    def start(self, spinners: int, root_pid: int | None = None) -> subprocess.Popen[bytes]:
        helper = subprocess.Popen(
            [
                sys.executable,
                "-I",
                str(HELPER),
                "supervise",
                "--log",
                str(self.log),
                "--spinners",
                str(spinners),
                "--root-pid",
                str(os.getpid() if root_pid is None else root_pid),
                "--stop",
                str(self.stop),
                "--record",
                str(self.record_file),
                "--idle",
            ]
        )
        self.addCleanup(self.end, helper)
        return helper

    @staticmethod
    def end(process: subprocess.Popen[bytes]) -> None:
        if process.poll() is None:
            process.kill()
        process.wait()

    def spinners_of(self, helper: subprocess.Popen[bytes], count: int) -> list[int]:
        """The spinners the helper started, from the record it writes once they wait."""
        self.assertTrue(wait_until(self.record_file.exists), "the helper wrote no record once its spinners waited")
        record = self.read_record()
        pids = [int(pid) for pid in record["spinner_pids"].split()]
        self.assertEqual("no", record["complete"])
        self.assertEqual(count, len(pids))
        self.assertTrue(all(fixed_load.process_running(pid) for pid in pids))
        return pids

    def stop_and_read(self, helper: subprocess.Popen[bytes]) -> dict[str, str]:
        self.stop.write_text("", encoding="utf-8")
        helper.wait(timeout=30)
        return self.read_record()

    def read_record(self) -> dict[str, str]:
        return dict(line.split("=", 1) for line in self.record_file.read_text(encoding="utf-8").splitlines())

    def assert_gone(self, pids: list[int]) -> None:
        self.assertTrue(
            wait_until(lambda: not any(fixed_load.process_running(pid) for pid in pids)),
            f"spinners {[pid for pid in pids if fixed_load.process_running(pid)]} outlived the run",
        )

    def test_spinners_wait_for_the_first_test_and_end_with_the_run(self) -> None:
        self.log.write_text("Loading the project\n", encoding="utf-8")
        helper = self.start(3)
        pids = self.spinners_of(helper, 3)
        time.sleep(0.3)
        # The project loads, and the spinners still wait for a test to begin.
        self.assertEqual("waiting", self.read_record()["load"])
        with self.log.open("a", encoding="utf-8") as log:
            log.write(utp("Begin") + "\n")
        time.sleep(0.5)

        record = self.stop_and_read(helper)

        self.assertEqual(0, helper.returncode)
        self.assertEqual("started", record["load"])
        self.assertEqual("Fixture.Tests.Times", record["first_test"])
        self.assertEqual("0", record["tests_ended_before_load"])
        # Without a plan in the log, the load lasted from the first test to the stop.
        self.assertEqual("run-end", record["load_ended"])
        self.assertEqual("", record["load_lag_ms"])
        self.assertGreaterEqual(float(record["load_seconds"]), 0.3)
        self.assertLess(float(record["load_seconds"]), 5.0)
        self.assertEqual(sorted(pids), sorted(map(int, record["spinner_pids"].split())))
        self.assertEqual("", record["spinners_left"])
        self.assertEqual("", record["error"])
        self.assertEqual("yes", record["complete"])
        self.assert_gone(pids)

    def test_the_spinners_stop_once_the_last_planned_test_ends(self) -> None:
        began = round(time.time() * 1000)
        self.log.write_text(
            '##utp:{"type":"TestPlan","tests":["Fixture.Tests.Times"]}\n'
            + utp("Begin").replace('"time":1,', f'"time":{began},')
            + "\n",
            encoding="utf-8",
        )
        helper = self.start(2)
        pids = self.spinners_of(helper, 2)
        time.sleep(0.3)
        with self.log.open("a", encoding="utf-8") as log:
            log.write(utp("End") + "\n")

        # The spinners stop before the run does.
        self.assert_gone(pids)
        self.assertEqual("last-test", self.read_record()["load_ended"])
        time.sleep(0.5)
        record = self.stop_and_read(helper)

        self.assertEqual(("started", "last-test", "yes"), (record["load"], record["load_ended"], record["complete"]))
        self.assertLess(float(record["load_seconds"]), 0.8)
        self.assertGreaterEqual(int(record["load_lag_ms"]), 0)
        self.assertLess(int(record["load_lag_ms"]), 5000)

    def test_a_run_in_which_no_test_began_starts_no_load(self) -> None:
        helper = self.start(2)
        pids = self.spinners_of(helper, 2)

        record = self.stop_and_read(helper)

        self.assertEqual("none", record["load"])
        self.assertEqual("0.0", record["load_seconds"])
        self.assert_gone(pids)

    def test_a_test_that_ended_before_the_load_started_is_counted(self) -> None:
        self.log.write_text(f"{utp('Begin')}\n{utp('End')}\n", encoding="utf-8")
        helper = self.start(1)
        self.spinners_of(helper, 1)
        time.sleep(0.3)

        record = self.stop_and_read(helper)

        self.assertEqual("started", record["load"])
        self.assertEqual("1", record["tests_ended_before_load"])

    def test_without_spinners_it_only_counts_the_other_unity_processes(self) -> None:
        helper = self.start(0)
        time.sleep(0.2)

        record = self.stop_and_read(helper)

        self.assertEqual("0", record["spinners"])
        self.assertEqual("", record["spinner_pids"])
        self.assertRegex(record["others_at_start"], r"^\d+$")
        self.assertRegex(record["others_during_max"], r"^\d+$")
        self.assertGreaterEqual(int(record["looks"]), 1)
        self.assertEqual(str(os.cpu_count()), record["cpus"])

    def test_the_record_gives_when_the_run_s_data_store_service_ended_and_its_exit_code(self) -> None:
        # A child of this process, the script here, stands in for the service: the listing names
        # it as one, and it exits with 7 once its input closes.
        service = subprocess.Popen(
            [sys.executable, "-c", "import sys; sys.stdin.read(); sys.exit(7)"], stdin=subprocess.PIPE
        )
        self.addCleanup(self.end, service)
        listing = fixed_load.list_processes
        listings: list[float] = []

        def processes() -> list[ProcessEntry]:
            listings.append(time.monotonic())
            name = fixed_load.default_data_store_process_name()
            return [ProcessEntry(entry.pid, entry.parent, name) if entry.pid == service.pid else entry for entry in listing()]

        def named() -> dict[str, str]:
            return self.read_record() if self.record_file.exists() else {}

        before = time.time()
        # The count of the other Unity processes, which lists them too, runs only at the start and the end.
        with mock.patch.object(fixed_load, "list_processes", side_effect=processes), mock.patch.object(
            fixed_load, "SAMPLE_SECONDS", 3600.0
        ):
            supervisor = threading.Thread(
                target=fixed_load.supervise,
                args=(self.log, 0, os.getpid(), self.stop, self.record_file),
                daemon=True,
            )
            supervisor.start()
            self.addCleanup(supervisor.join, 30)
            self.addCleanup(self.stop.write_text, "", encoding="utf-8")

            # The helper writes its record as it finds the service, and again as the service ends.
            self.assertTrue(wait_until(lambda: named().get("data_store_pids") == str(service.pid)))
            self.assertEqual("", named()[f"data_store_{service.pid}_ended"])
            # Once it has found the service, it lists the processes no more, over several looks.
            found = len(listings)
            time.sleep(4 * fixed_load.SERVICE_LOOK_SECONDS)
            self.assertEqual(found, len(listings))
            assert service.stdin is not None
            service.stdin.close()
            self.assertEqual(7, service.wait(timeout=30))
            after = time.time()
            self.assertTrue(wait_until(lambda: named()[f"data_store_{service.pid}_ended"] != ""))
            self.stop.write_text("", encoding="utf-8")
            supervisor.join(timeout=30)

        record = self.read_record()
        self.assertEqual("yes", record["complete"])
        self.assertEqual(str(service.pid), record["data_store_pids"])
        ended = datetime.fromisoformat(record[f"data_store_{service.pid}_ended"]).timestamp()
        self.assertLessEqual(before - 1, ended)
        self.assertLessEqual(ended, after + 1)
        self.assertEqual("", record[f"data_store_{service.pid}_error"])
        if sys.platform == "win32":
            # Windows gives a process's start and its exit code to a handle held open on it.
            self.assertEqual("7", record[f"data_store_{service.pid}_exit_code"])
            started = datetime.fromisoformat(record[f"data_store_{service.pid}_started"]).timestamp()
            self.assertLessEqual(started, ended)
        else:
            self.assertEqual("", record[f"data_store_{service.pid}_exit_code"])

    def test_the_spinners_end_when_the_script_ends_without_stopping_them(self) -> None:
        script = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        self.addCleanup(self.end, script)
        helper = self.start(2, root_pid=script.pid)
        pids = self.spinners_of(helper, 2)

        script.kill()
        script.wait()

        helper.wait(timeout=30)
        self.assertEqual(1, helper.returncode)
        self.assertEqual("the script ended before it stopped the load", self.read_record()["error"])
        self.assert_gone(pids)

    def test_the_spinners_end_by_themselves_when_the_helper_is_killed(self) -> None:
        self.log.write_text(utp("Begin") + "\n", encoding="utf-8")
        helper = self.start(2)
        pids = self.spinners_of(helper, 2)
        time.sleep(0.3)

        helper.kill()
        helper.wait()

        self.assert_gone(pids)


if __name__ == "__main__":
    unittest.main()
