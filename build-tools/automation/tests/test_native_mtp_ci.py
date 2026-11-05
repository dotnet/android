"""Real host integration probes. Run with python3 -m unittest discover -s build-tools/automation/tests."""

from collections import Counter
import csv
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import tempfile
import time
import unittest
from unittest import mock
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "build-tools/automation/scripts/run-native-mtp-tests.py"
PROJECT = "tests/Xamarin.SourceWriter-Tests/Xamarin.SourceWriter-Tests.csproj"
PROBE_PROJECT = "build-tools/automation/tests/NativeMtpProbe/NativeMtpProbe.csproj"
spec = importlib.util.spec_from_file_location("native_mtp", SCRIPT)
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)
NS = adapter.NS


def process_is_running(pid):
    if os.name == "posix":
        result = subprocess.run(["ps", "-o", "stat=", "-p", str(pid)],
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=False)
        if result.returncode not in (0, 1):
            raise AssertionError(f"Unable to inspect owned PID {pid}: {result.stderr}")
        return bool(result.stdout.strip()) and not result.stdout.strip().startswith("Z")
    result = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
    return any(len(row) > 1 and row[1] == str(pid) for row in csv.reader(result.stdout.splitlines()))


class NativeMtpIntegration(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        destination = os.environ.get("MTP_CI_EVIDENCE")
        cls.temporary = None if destination else tempfile.TemporaryDirectory(prefix="native-mtp-ci-")
        cls.evidence = Path(destination or cls.temporary.name).resolve()
        cls.evidence.mkdir(parents=True, exist_ok=True)
        cls.dotnet = os.environ.get("MTP_DOTNET", "dotnet")
        cls.command("dotnet-info", [cls.dotnet, "--info"], 0)
        cls.command("python-info", [os.sys.executable, "--version"], 0)
        cls.sourcewriter = ROOT / "bin/TestDebug/net11.0/Xamarin.SourceWriter-Tests.dll"
        cls.probe = ROOT / "build-tools/automation/tests/NativeMtpProbe/bin/Debug/net11.0/NativeMtpProbe.dll"
        for name, project, configuration in (
            ("build-sourcewriter", PROJECT, "Debug"),
            ("build-sourcewriter-release", PROJECT, "Release"),
            ("build-probe", PROBE_PROJECT, "Debug"),
        ):
            cls.command(name, [cls.dotnet, "build", project, "-c", configuration, "-v:q"], 0)

    @classmethod
    def tearDownClass(cls):
        adapter.write_json(cls.evidence / "command-index.json", [
            {"path": str(path.relative_to(cls.evidence)), **json.loads(path.read_text())}
            for path in sorted(cls.evidence.rglob("command.json"))
        ])
        if cls.temporary:
            cls.temporary.cleanup()

    @classmethod
    def command(cls, name, argv, expected, env=None, cwd=ROOT):
        directory = cls.evidence / name
        directory.mkdir()
        environment = os.environ.copy()
        environment.pop("DOTNET_TEST_RUNNER", None)
        environment.pop("TESTINGPLATFORM_EXITCODE_IGNORE", None)
        environment.update(env or {})
        with (directory / "stdout.log").open("w") as stdout, (directory / "stderr.log").open("w") as stderr:
            result = subprocess.run(argv, cwd=cwd, env=environment, stdout=stdout, stderr=stderr, timeout=180)
        adapter.write_json(directory / "command.json", {
            "argv": argv, "cwd": str(cwd), "exitCode": result.returncode, "environment": env or {},
        })
        if result.returncode != expected:
            raise AssertionError(
                f"{name}: expected exit {expected}, got {result.returncode}\n"
                + (directory / "stdout.log").read_text()
                + (directory / "stderr.log").read_text()
            )
        return directory

    def execute(self, name, module=None, filter="", slices=1, number=1, expected=0, retries=1, extra_env=None):
        result = self.evidence / (name + "-results")
        state = self.evidence / (name + "-state")
        state.mkdir()
        # These are the actual PythonScript@0 template inputs; no shell quoting or mock runner.
        environment = {
            "MTP_MODULE": str(module or self.probe), "MTP_FILTER": filter,
            "MTP_RESULTS_DIRECTORY": str(result), "MTP_SLICE_NUMBER": str(number),
            "MTP_TOTAL_SLICES": str(slices), "MTP_RETRIES": str(retries),
            "MTP_DOTNET": self.dotnet, "MTP_PROBE_STATE": str(state),
        }
        environment.update(extra_env or {})
        self.command(name, [os.sys.executable, str(SCRIPT)], expected, environment)
        self.assertEqual(expected, json.loads((result / "exit.json").read_text())["exitCode"])
        return result

    def effective(self, directory):
        cases = []
        for path in directory.glob("attempt-*/effective.trx"):
            root = ET.parse(path).getroot()
            results = root.findall(f"{NS}Results/{NS}UnitTestResult")
            counters = root.find(f"{NS}ResultSummary/{NS}Counters")
            self.assertEqual(len(results), int(counters.get("total")))
            cases.extend((r.get("testId"), r.get("outcome")) for r in results)
            for result in results:
                # Resolve TRX attachments exactly as the publication task does.
                deployment = root.find(f"{NS}TestSettings/{NS}Deployment").get("runDeploymentRoot")
                for file in result.findall(f"{NS}ResultFiles/{NS}ResultFile"):
                    attachment = path.parent / deployment / "In" / result.get("relativeResultsDirectory") / file.get("path")
                    self.assertTrue(attachment.is_file(), str(attachment))
                    self.assertTrue(attachment.read_text())
        self.assertEqual(len(cases), len({uid for uid, _ in cases}))
        return cases

    def test_sourcewriter_slices_and_release(self):
        union = []
        baselines = []
        for number in range(1, 4):
            directory = self.execute(f"sourcewriter-{number}", self.sourcewriter, slices=3, number=number)
            selection = json.loads((directory / "selection.json").read_text())
            baselines.append(sorted(t["uid"] for t in selection["baseline"]))
            cases = self.effective(directory)
            self.assertTrue(all(outcome == "Passed" for _, outcome in cases))
            self.assertCountEqual(selection["selected"], [uid for uid, _ in cases])
            union.extend(uid for uid, _ in cases)
        self.assertEqual(baselines[0], baselines[1])
        self.assertEqual(baselines[0], baselines[2])
        self.assertEqual(11, len(union))
        self.assertEqual(Counter(baselines[0]), Counter(union))
        release = self.execute("sourcewriter-release", ROOT / "bin/TestRelease/net11.0/Xamarin.SourceWriter-Tests.dll")
        self.assertEqual(Counter(union), Counter(uid for uid, _ in self.effective(release)))
        filtered = self.execute("sourcewriter-qualified", self.sourcewriter,
                                "FullyQualifiedName=Xamarin.SourceWriter.Tests.TypeReferenceWriterTests.Constructor")
        self.assertEqual(1, len(self.effective(filtered)))

    def test_discovery_only_and_duplicate_data_slices(self):
        state = self.evidence / "discovery-only-state"
        state.mkdir()
        baseline = None
        for index in range(2):
            directory = self.command(
                f"discovery-only-{index}",
                [self.dotnet, str(self.probe), "--list-tests", "json", "--filter", "TestCategory=selection", "--no-ansi"],
                0, {"MTP_PROBE_STATE": str(state)},
            )
            tests = json.loads((directory / "stdout.log").read_text())["tests"]
            if baseline is None:
                baseline = tests
            else:
                self.assertEqual(baseline, tests)
        self.assertFalse((state / "executions.txt").exists(), "discovery executed test bodies")
        self.assertEqual(6, len(baseline))
        self.assertEqual(2, sum(t["displayName"] == "same display" for t in baseline))
        self.assertEqual(2, sum(t["displayName"] == "duplicate data" for t in baseline))
        union = []
        empty = 0
        for number in range(1, 9):
            directory = self.execute(f"data-slice-{number}", filter="TestCategory=selection", slices=8, number=number)
            selection = json.loads((directory / "selection.json").read_text())
            cases = self.effective(directory)
            self.assertCountEqual(selection["selected"], [uid for uid, _ in cases])
            union.extend(uid for uid, _ in cases)
            if not cases:
                empty += 1
                self.assertEqual("empty-slice", json.loads((directory / "summary.json").read_text())["status"])
                self.assertFalse(list(directory.glob("attempt-*")))
        self.assertEqual(2, empty)
        self.assertEqual(Counter(t["uid"] for t in baseline), Counter(union))

    def test_native_filters(self):
        for name, expression, count in (
            ("method", "FullyQualifiedName~ContractTests.Rows", 5),
            ("duplicate-display", "Name=same display", 2),
            ("escaped-category", r"TestCategory=escaped\&category", 1),
            ("escaped-display", r'Name=a\&b\|\(c\)\=\!\~\\"; percent%2C comma, space', 1),
            ("compound", "(TestCategory=selection&FullyQualifiedName~Rows)|TestCategory=escaped\\&category", 6),
        ):
            directory = self.execute(name, filter=expression)
            self.assertEqual(count, len(self.effective(directory)), expression)
        self.execute("no-tests", filter="TestCategory=nonexistent", expected=8)
        self.execute("unsupported-filter", filter="NotAProperty=foo", expected=8)
        self.execute("invalid-filter", filter="(TestCategory=selection", expected=8)

    def test_retry_success_and_final_failure(self):
        for name, expression, expected in (
            ("retry-success", "TestCategory=retry", 0),
            ("retry-failure", "TestCategory=retry|TestCategory=failure|TestCategory=selection", 2),
            ("data-retry", "TestCategory=data-retry", 0),
        ):
            directory = self.execute(name, filter=expression, expected=expected)
            summary = json.loads((directory / "summary.json").read_text())
            self.assertEqual(2, summary["attempts"])
            first = ET.parse(directory / "attempt-1/raw.trx").getroot()
            second = ET.parse(directory / "attempt-2/raw.trx").getroot()
            initial = {r.get("testId"): r for r in first.findall(f"{NS}Results/{NS}UnitTestResult")}
            retried = {r.get("testId"): r for r in second.findall(f"{NS}Results/{NS}UnitTestResult")}
            self.assertEqual({uid for uid, r in initial.items() if r.get("outcome") == "Failed"}, set(retried))
            message = "deliberate first-row failure" if name == "data-retry" else "deliberate first-attempt failure"
            method = "ContractTests.RetryRows" if name == "data-retry" else "ContractTests.FailsOnce"
            self.assertIn(message, (directory / "attempt-1/raw.trx").read_text(encoding="utf-8-sig"))
            self.assertIn(method, (directory / "attempt-1/raw.trx").read_text(encoding="utf-8-sig"))
            final = self.effective(directory)
            self.assertEqual(len(initial), len(final))
            self.assertEqual(1 if expected else 0, sum(outcome == "Failed" for _, outcome in final))
            for attempt in ("attempt-1", "attempt-2"):
                self.assertTrue(list((directory / attempt).glob("*.diag")))
                self.assertIn("probe output:", (directory / attempt / "raw.trx").read_text(encoding="utf-8-sig"))
                for file in (directory / attempt).rglob("*.txt"):
                    self.assertTrue(file.read_text())
        once = self.execute("retry-disabled", filter="TestCategory=failure", expected=2, retries=0)
        self.assertFalse((once / "attempt-2").exists())

    def test_infrastructure_and_skips(self):
        crash = self.execute("crash", filter="TestCategory=infrastructure", expected=7)
        self.assertFalse((crash / "attempt-2").exists())
        self.assertFalse(list(crash.glob("attempt-*/effective.trx")))
        retry_crash = self.execute("retry-crash", filter="TestCategory=retry-infrastructure", expected=7)
        self.assertTrue((retry_crash / "attempt-1/raw.trx").is_file())
        self.assertFalse(list(retry_crash.glob("attempt-*/effective.trx")))
        timeout = self.execute("timeout", filter="TestCategory=timeout", expected=10,
                               extra_env={"MTP_TIMEOUT_SECONDS": "3"})
        self.assertTrue((timeout / "attempt-1/command.json").is_file())
        self.assertFalse(list(timeout.glob("attempt-*/effective.trx")))
        skipped = self.execute("skipped", filter="TestCategory=skipped")
        self.assertEqual(["NotExecuted"], [outcome for _, outcome in self.effective(skipped)])
        self.execute("invalid-slice", filter="TestCategory=selection", number=0, expected=10)
        self.execute("ignored-exit-code", filter="TestCategory=selection", expected=10,
                     extra_env={"TESTINGPLATFORM_EXITCODE_IGNORE": "2"})
        self.command("bad-numeric-argument", [os.sys.executable, str(SCRIPT),
                     "--module", str(self.probe), "--results-directory", str(self.evidence / "bad-number"),
                     "--total-slices", "not-an-integer"], 5)

    def test_timeout_cleans_owned_child_and_grandchild(self):
        state = self.evidence / "child-timeout-state"
        sentinel = subprocess.Popen([os.sys.executable, "-c", "import time; time.sleep(60)"])
        try:
            directory = self.execute(
                "child-timeout", filter="TestCategory=child-timeout", expected=10,
                extra_env={
                    "MTP_TIMEOUT_SECONDS": "5", "MTP_PROBE_CHILD_EXECUTABLE": os.sys.executable,
                    "MTP_PROBE_CHILD_SCRIPT": str(ROOT / "build-tools/automation/tests/timeout_child.py"),
                },
            )
            attempt = directory / "attempt-1"
            command = json.loads((attempt / "command.json").read_text())
            self.assertIn("error", command)
            self.assertEqual(command["pid"], command["timeoutCleanup"]["rootPid"])
            self.assertFalse(process_is_running(command["pid"]))
            self.assertIsNone(sentinel.poll(), "timeout cleanup killed an unrelated sibling process")
            descendants = []
            for role in ("child", "grandchild"):
                record = json.loads((state / f"{role}.json").read_text())
                if os.name == "posix":
                    self.assertEqual(command["pid"], record["processGroup"])
                deadline = time.monotonic() + 5
                while process_is_running(record["pid"]) and time.monotonic() < deadline:
                    time.sleep(0.05)
                self.assertFalse(process_is_running(record["pid"]), f"surviving owned {role}: {record}")
                descendants.append({"role": role, **record, "runningAfterTimeout": False})
                heartbeat = (state / f"{role}.heartbeat").read_text()
                time.sleep(0.2)
                self.assertEqual(heartbeat, (state / f"{role}.heartbeat").read_text())
            self.assertFalse(list(directory.glob("attempt-*/effective.trx")))
            self.assertTrue(list(attempt.glob("*.diag")))
            self.assertIn("timeout probe child PID", (attempt / "stdout.log").read_text())
            self.assertIn("timeout probe grandchild PID", (attempt / "stdout.log").read_text())
            self.assertIsNone(sentinel.poll())
            adapter.write_json(directory / "timeout-tree-verification.json", {
                "exitCode": 10, "rootPid": command["pid"], "rootReaped": True,
                "descendants": descendants, "unrelatedSiblingPid": sentinel.pid,
                "unrelatedSiblingStillRunning": True, "effectiveTrxCount": 0,
            })
        finally:
            sentinel.terminate()
            sentinel.wait(timeout=10)
            # A failing regression must not leave the deliberately spawned workers running.
            for role in ("child", "grandchild"):
                path = state / f"{role}.json"
                if path.exists():
                    record = json.loads(path.read_text())
                    pid = record["pid"]
                    if process_is_running(pid):
                        if os.name == "posix":
                            self.assertEqual(record["processGroup"], os.getpgid(pid), "PID ownership changed")
                            os.kill(pid, signal.SIGKILL)
                        else:
                            subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], check=True)

    def test_project_and_module_boundaries_and_nunit_neighbor(self):
        module_time = self.sourcewriter.stat().st_mtime_ns
        for name, arguments in (
            ("project-execution-only", ["--project", PROJECT, "--no-build", "-c", "Debug"]),
            ("sdk-module-execution-only", ["--test-modules", "bin/TestDebug/net11.0/Xamarin.SourceWriter-Tests.dll"]),
        ):
            results = self.evidence / (name + "-results")
            self.command(name, [self.dotnet, "test", *arguments, "--report-trx",
                                "--results-directory", str(results)], 0,
                         {"DOTNET_TEST_RUNNER": "Microsoft.Testing.Platform"})
            paths = list(results.rglob("*.trx"))
            self.assertEqual(1, len(paths))
            self.assertEqual(11, len(ET.parse(paths[0]).findall(f"{NS}Results/{NS}UnitTestResult")))
        self.assertEqual(module_time, self.sourcewriter.stat().st_mtime_ns, "execution-only rebuilt the module")
        neighbor = "tests/Xamarin.Android.Tools.Aidl-Tests/Xamarin.Android.Tools.Aidl-Tests.csproj"
        for configuration in ("Debug", "Release"):
            results = self.evidence / f"nunit-{configuration}-results"
            self.command(f"nunit-{configuration}", [self.dotnet, "test", neighbor, "-c", configuration,
                         "--logger", "trx", "--results-directory", str(results)], 0)
            trx = list(results.glob("*.trx"))
            self.assertEqual(1, len(trx))
            cases = ET.parse(trx[0]).findall(f"{NS}Results/{NS}UnitTestResult")
            self.assertEqual(16, len(cases))
            self.assertTrue(all(r.get("outcome") == "Passed" for r in cases))
        results = self.evidence / "nunit-module-results"
        self.command("nunit-module", [self.dotnet, "test",
                     str(ROOT / "bin/TestDebug/net11.0/Xamarin.Android.Tools.Aidl-Tests.dll"),
                     "--logger", "trx", "--results-directory", str(results)], 0)
        self.assertEqual(16, len(ET.parse(next(results.glob("*.trx"))).findall(f"{NS}Results/{NS}UnitTestResult")))


class AdapterContractTests(unittest.TestCase):
    def test_execution_identity_mismatch_is_not_success(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "incomplete.trx"
            path.write_text(f'<TestRun xmlns="{NS[1:-1]}"><Results /></TestRun>')
            with self.assertRaisesRegex(adapter.ContractError, "multiset"):
                adapter.read_results(path, ["missing"], 0)

    def test_bad_discovery_contracts_fail_closed(self):
        uid = "124d6eed-8b12-8185-88ea-3d3f9c2fcbef"
        for document, message in (
            ({"schemaVersion": 2, "tests": []}, "schema"),
            ({"schemaVersion": 1, "tests": [{"displayName": "no UID"}]}, "UID"),
            ({"schemaVersion": 1, "tests": [{"uid": uid}, {"uid": uid}]}, "duplicate UIDs"),
        ):
            with self.subTest(document=document), tempfile.TemporaryDirectory() as temporary:
                output = Path(temporary)
                directory = output / "discovery"
                directory.mkdir()
                adapter.write_json(directory / "stdout.log", document)
                args = type("Args", (), {"dotnet": "dotnet", "filter": "", "timeout": 10})()
                with mock.patch.object(adapter, "invoke", return_value=0):
                    with self.assertRaisesRegex(adapter.ContractError, message):
                        adapter.discovery(Path("unused.dll"), args, output)


if __name__ == "__main__":
    unittest.main()
