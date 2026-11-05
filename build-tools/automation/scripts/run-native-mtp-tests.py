#!/usr/bin/env python3
"""Execution-only, single-module MSTest/MTP CI slicing and retry adapter."""

import argparse
from collections import Counter
import copy
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET


NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
ET.register_namespace("", NS[1:-1])


class ContractError(Exception):
    pass


class ArgumentParser(argparse.ArgumentParser):
    def error(self, message):
        self.print_usage(sys.stderr)
        self.exit(5, f"Native MTP argument error: {message}\n")


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def invoke(command, directory, timeout):
    directory.mkdir(parents=True, exist_ok=True)
    record = {"argv": command, "cwd": str(Path.cwd())}
    try:
        with (directory / "stdout.log").open("w", encoding="utf-8") as stdout, (
            directory / "stderr.log"
        ).open("w", encoding="utf-8") as stderr:
            result = subprocess.run(
                command, stdout=stdout, stderr=stderr, timeout=timeout, check=False
            )
        record["exitCode"] = result.returncode
    except (OSError, subprocess.TimeoutExpired) as error:
        record["error"] = str(error)
        raise ContractError(f"Unable to complete {command[0]}: {error}") from error
    finally:
        write_json(directory / "command.json", record)
    print((directory / "stdout.log").read_text(encoding="utf-8"), end="")
    print((directory / "stderr.log").read_text(encoding="utf-8"), end="", file=sys.stderr)
    return result.returncode


def discovery(module, args, output):
    directory = output / "discovery"
    command = [
        args.dotnet, str(module), "--list-tests", "json", "--no-ansi",
        "--results-directory", str(directory), "--diagnostic",
        "--diagnostic-output-directory", str(directory),
    ]
    if args.filter:
        command.extend(["--filter", args.filter])
    code = invoke(command, directory, args.timeout)
    if code:
        return code, []
    document = json.loads((directory / "stdout.log").read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict) or document.get("schemaVersion") != 1 or not isinstance(document.get("tests"), list):
        raise ContractError("Unsupported native discovery schema; expected schemaVersion 1")
    tests = document["tests"]
    uids = []
    for test in tests:
        if not isinstance(test, dict):
            raise ContractError("Discovery test must be an object with a GUID UID")
        uid = test.get("uid")
        # This adapter's TRX identity contract is the pinned MSTest GUID UID.
        if not isinstance(uid, str) or str(uuid.UUID(uid)) != uid:
            raise ContractError(f"Unsupported MSTest/TRX UID: {uid!r}")
        uids.append(uid)
    if len(set(uids)) != len(uids):
        raise ContractError("Discovery returned duplicate UIDs; cannot select occurrences independently")
    return (0 if tests else 8), tests


def read_results(path, expected, code):
    root = ET.parse(path).getroot()
    results = root.findall(f"{NS}Results/{NS}UnitTestResult")
    actual = [result.get("testId") for result in results]
    if Counter(actual) != Counter(expected):
        raise ContractError(f"Execution UID multiset differs from selection: {actual!r}")
    definitions = root.findall(f"{NS}TestDefinitions/{NS}UnitTest")
    entries = root.findall(f"{NS}TestEntries/{NS}TestEntry")
    if Counter(d.get("id") for d in definitions) != Counter(expected):
        raise ContractError("TRX definitions do not match selected UIDs")
    if Counter(e.get("testId") for e in entries) != Counter(expected):
        raise ContractError("TRX entries do not match selected UIDs")
    outcomes = {r.get("testId"): r.get("outcome") for r in results}
    if any(value not in ("Passed", "Failed", "NotExecuted") for value in outcomes.values()):
        raise ContractError(f"Unsupported/incomplete TRX outcomes: {outcomes!r}")
    failed = sorted(uid for uid, outcome in outcomes.items() if outcome == "Failed")
    if (code == 2) != bool(failed):
        raise ContractError("Native exit code and TRX failures disagree")
    return root, outcomes, failed


def publish_attempt(root, path, keep):
    """Keep the original TRX layout so attachment paths remain unchanged."""
    root = copy.deepcopy(root)
    for container, tag, key in (
        ("Results", "UnitTestResult", "testId"),
        ("TestDefinitions", "UnitTest", "id"),
        ("TestEntries", "TestEntry", "testId"),
    ):
        parent = root.find(NS + container)
        if parent is None:
            raise ContractError(f"Missing TRX {container}")
        for node in list(parent):
            if node.tag != NS + tag or node.get(key) not in keep:
                parent.remove(node)
    outcomes = Counter(
        r.get("outcome") for r in root.findall(f"{NS}Results/{NS}UnitTestResult")
    )
    summary = root.find(NS + "ResultSummary")
    if summary is None:
        raise ContractError("Missing TRX ResultSummary")
    summary.set("outcome", "Failed" if outcomes["Failed"] else "Completed")
    counters = summary.find(NS + "Counters")
    if counters is None:
        raise ContractError("Missing TRX counters")
    for key in list(counters.attrib):
        counters.set(key, "0")
    counters.set("total", str(sum(outcomes.values())))
    counters.set("executed", str(outcomes["Passed"] + outcomes["Failed"]))
    counters.set("passed", str(outcomes["Passed"]))
    counters.set("failed", str(outcomes["Failed"]))
    counters.set("notExecuted", str(outcomes["NotExecuted"]))
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)


def run(args, output):
    if not 1 <= args.slice_number <= args.total_slices:
        raise ContractError("Require 1 <= slice-number <= total-slices")
    if args.retries < 0 or args.retries > 10 or args.timeout <= 0:
        raise ContractError("Require 0 <= retries <= 10 and a positive timeout")
    module = Path(args.module).resolve(strict=True)
    if module.suffix.lower() != ".dll":
        raise ContractError("Select exactly one built native-MTP DLL, not a project or glob")
    code, tests = discovery(module, args, output)
    if code:
        return code if 0 < code < 256 else 10
    ordered = sorted(test["uid"] for test in tests)
    selected = ordered[args.slice_number - 1::args.total_slices]
    write_json(output / "selection.json", {
        "module": str(module), "filter": args.filter,
        "sliceNumber": args.slice_number, "totalSlices": args.total_slices,
        "baseline": tests, "selected": selected,
    })
    if not selected:
        write_json(output / "summary.json", {"status": "empty-slice", "exitCode": 0})
        print("Explicit empty slice of a nonempty baseline: no execution or TRX publication.")
        return 0

    pending = selected
    attempts = []
    latest = {}
    for index in range(args.retries + 1):
        directory = output / f"attempt-{index + 1}"
        directory.mkdir()
        response = directory / "selection.rsp"
        response.write_text("--filter-uid\n" + "\n".join(pending) + "\n", encoding="utf-8")
        command = [
            args.dotnet, str(module), "@" + str(response), "--no-ansi",
            "--report-trx", "--report-trx-filename", "raw.trx",
            "--results-directory", str(directory), "--diagnostic",
            "--diagnostic-output-directory", str(directory),
        ]
        code = invoke(command, directory, args.timeout)
        # A crash, timeout, invalid argument or adapter failure is never retried as a test failure.
        if code not in (0, 2):
            return code if 0 < code < 256 else 10
        root, outcomes, failed = read_results(directory / "raw.trx", pending, code)
        attempts.append((root, directory))
        for uid, outcome in outcomes.items():
            latest[uid] = {"attempt": index + 1, "outcome": outcome}
        if not failed:
            break
        pending = failed

    for index, (root, directory) in enumerate(attempts, start=1):
        keep = {uid for uid, result in latest.items() if result["attempt"] == index}
        if keep:
            publish_attempt(root, directory / "effective.trx", keep)
    code = 2 if any(r["outcome"] == "Failed" for r in latest.values()) else 0
    write_json(output / "summary.json", {
        "status": "failed" if code else "passed", "exitCode": code, "cases": latest,
        "attempts": len(attempts),
    })
    return code


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--module", default=os.environ.get("MTP_MODULE"), required="MTP_MODULE" not in os.environ)
    parser.add_argument("--results-directory", default=os.environ.get("MTP_RESULTS_DIRECTORY"),
                        required="MTP_RESULTS_DIRECTORY" not in os.environ)
    parser.add_argument("--dotnet", default=os.environ.get("MTP_DOTNET", "dotnet"))
    parser.add_argument("--filter", default=os.environ.get("MTP_FILTER", ""))
    parser.add_argument("--slice-number", type=int, default=os.environ.get("MTP_SLICE_NUMBER", "1"))
    parser.add_argument("--total-slices", type=int, default=os.environ.get("MTP_TOTAL_SLICES", "1"))
    parser.add_argument("--retries", type=int, default=os.environ.get("MTP_RETRIES", "1"))
    parser.add_argument("--timeout", type=int, default=os.environ.get("MTP_TIMEOUT_SECONDS", "600"))
    args = parser.parse_args()
    output = Path(args.results_directory).resolve()
    try:
        # Reusing a directory can publish stale passing results after a failed invocation.
        output.mkdir(parents=True, exist_ok=False)
    except OSError as error:
        print(f"Native MTP results directory must be new: {error}", file=sys.stderr)
        return 10
    try:
        # Exit-code suppression would mask crashes or failed tests before validation.
        if os.environ.get("TESTINGPLATFORM_EXITCODE_IGNORE"):
            raise ContractError("Unset TESTINGPLATFORM_EXITCODE_IGNORE for CI execution")
        code = run(args, output)
    except (ContractError, OSError, ValueError, ET.ParseError) as error:
        print(f"Native MTP infrastructure/contract error: {error}", file=sys.stderr)
        write_json(output / "error.json", {"error": str(error)})
        code = 10
    write_json(output / "exit.json", {"exitCode": code})
    return code


if __name__ == "__main__":
    sys.exit(main())
