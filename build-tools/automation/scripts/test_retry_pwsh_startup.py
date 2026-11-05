import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("retry-pwsh-startup.sh")
STARTUP_ERROR = (
    "Call to 'procargs' failed with errno 5\n"
    "Unhandled exception. Microsoft.PowerShell.ManagedPSEntry+StartupException\n"
)


class RetryPowerShellStartupTests(unittest.TestCase):
    def run_launcher(self, attempts, arguments=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            pwsh = root / "pwsh"
            pwsh.write_text(
                f"#!{sys.executable}\n"
                "import json, os, pathlib, sys\n"
                "root = pathlib.Path(os.environ['FAKE_PWSH_ROOT'])\n"
                "counter = root / 'count'\n"
                "count = int(counter.read_text()) if counter.exists() else 0\n"
                "counter.write_text(str(count + 1))\n"
                "(root / 'arguments').write_text(json.dumps(sys.argv[1:]))\n"
                "attempts = json.loads(os.environ['FAKE_PWSH_ATTEMPTS'])\n"
                "status, output = attempts[min(count, len(attempts) - 1)]\n"
                "sys.stderr.write(output)\n"
                "sys.exit(status)\n"
            )
            pwsh.chmod(0o755)
            sleep = root / "sleep"
            sleep.write_text('#!/bin/sh\nprintf "%s\\n" "$1" >> "$FAKE_PWSH_ROOT/delays"\n')
            sleep.chmod(0o755)
            env = dict(
                os.environ,
                PATH=f"{root}{os.pathsep}{os.environ['PATH']}",
                TMPDIR=directory,
                FAKE_PWSH_ROOT=directory,
                FAKE_PWSH_ATTEMPTS=json.dumps(attempts),
            )
            result = subprocess.run(
                ["bash", str(SCRIPT), *(arguments or [])],
                env=env,
                capture_output=True,
                text=True,
                timeout=10,
            )
            count = int((root / "count").read_text())
            actual_arguments = json.loads((root / "arguments").read_text())
            delays = root / "delays"
            actual_delays = delays.read_text().splitlines() if delays.exists() else []
            self.assertEqual(list(root.glob("tmp.*")), [])
            return result, count, actual_arguments, actual_delays

    def test_success_preserves_arguments_and_output(self):
        arguments = ["-NoProfile", "-Command", 'Write-Output "path with spaces"']
        result, count, actual_arguments, delays = self.run_launcher([(0, "success\n")], arguments)
        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "success\n")
        self.assertEqual(count, 1)
        self.assertEqual(actual_arguments, arguments)
        self.assertEqual(delays, [])

    def test_startup_crash_recovers(self):
        result, count, _, delays = self.run_launcher([(134, STARTUP_ERROR), (0, "success\n")])
        self.assertEqual(result.returncode, 0)
        self.assertEqual(count, 2)
        self.assertEqual(delays, ["1"])
        self.assertIn(STARTUP_ERROR, result.stdout)
        self.assertIn("##vso[task.logissue type=warning]", result.stdout)
        self.assertTrue(result.stdout.endswith("success\n"))

    def test_two_startup_crashes_recover(self):
        result, count, _, delays = self.run_launcher(
            [(134, STARTUP_ERROR), (134, STARTUP_ERROR), (0, "success\n")]
        )
        self.assertEqual(result.returncode, 0)
        self.assertEqual(count, 3)
        self.assertEqual(delays, ["1", "4"])

    def test_persistent_startup_crash_preserves_failure(self):
        result, count, _, delays = self.run_launcher([(134, STARTUP_ERROR)])
        self.assertEqual(result.returncode, 134)
        self.assertEqual(count, 3)
        self.assertEqual(delays, ["1", "4"])
        self.assertEqual(result.stdout.count(STARTUP_ERROR), 3)

    def test_other_failures_are_not_retried(self):
        for output in [
            "slicer failed\n",
            "Call to 'procargs' failed with errno 5\n",
            "Microsoft.PowerShell.ManagedPSEntry+StartupException\n",
            "Call to 'procargs' failed with errno 6\n"
            "Microsoft.PowerShell.ManagedPSEntry+StartupException\n",
        ]:
            with self.subTest(output=output):
                result, count, _, delays = self.run_launcher([(42, output)])
                self.assertEqual(result.returncode, 42)
                self.assertEqual(count, 1)
                self.assertEqual(delays, [])
                self.assertEqual(result.stdout, output)

    def test_script_failure_after_startup_retry_is_not_retried(self):
        result, count, _, delays = self.run_launcher(
            [(134, STARTUP_ERROR), (23, "slicer failed\n")]
        )
        self.assertEqual(result.returncode, 23)
        self.assertEqual(count, 2)
        self.assertEqual(delays, ["1"])
        self.assertTrue(result.stdout.endswith("slicer failed\n"))

    def test_success_is_not_retried_even_if_output_matches(self):
        result, count, _, delays = self.run_launcher([(0, STARTUP_ERROR)])
        self.assertEqual(result.returncode, 0)
        self.assertEqual(count, 1)
        self.assertEqual(delays, [])


if __name__ == "__main__":
    unittest.main()
