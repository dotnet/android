#!/usr/bin/env python3
"""Verify captured native results, including identities, lifecycle and artifacts."""

import argparse
import base64
from collections import Counter
import json
from pathlib import Path
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
FIXTURES = {
    "AndroidMSTestProof.ProofTests": (
        "MSTest.Android.Tests",
        {"AndroidBinding": 1, "AsyncTask": 1, "AsyncValueTask": 1, "DataRows": 3,
         "DynamicRows": 3, "Attachment": 1, "Ignored": 1, "GeneratedFailure": 1}),
    "AndroidMSTestProof.TestLibrary.LibraryTests": (
        "MSTest.Android.TestLibrary.Tests",
        {"ReferencedLibrary": 1, "AsyncLibrary": 1, "InconclusiveLibrary": 1, "GeneratedLibraryFailure": 1}),
    "AndroidMSTestProof.JavaInterop.InteropTests": (
        "MSTest.Android.JavaInterop.Tests",
        {"ReferencedJNI": 1, "StringRoundtrip": 2, "GeneratedInteropFailure": 1}),
}
STATES = {
    "PassedTestNodeStateProperty": "passed",
    "FailedTestNodeStateProperty": "failed",
    "SkippedTestNodeStateProperty": "skipped",
}
FAILURES = {"GeneratedFailure", "GeneratedLibraryFailure", "GeneratedInteropFailure"}
SKIPS = {"Ignored", "InconclusiveLibrary"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify(directory, selection):
    bundle = json.loads((directory / "bundle.json").read_text())
    expected = Counter()
    for classname, (assembly, methods) in FIXTURES.items():
        for method, count in methods.items():
            if selection == "proof" and method in FAILURES:
                continue
            if selection == "diagnostic" and method not in FAILURES:
                continue
            expected[assembly, classname, method] = count
    finals = [node for node in ET.parse(directory / "D.Nodes.xml").getroot() if node.get("state") in STATES]
    actual = Counter((node.get("assembly", "").split(",")[0], f'{node.get("namespace")}.{node.get("type")}', node.get("method")) for node in finals)
    require(actual == expected, f"{directory}: missing {expected - actual}; extra {actual - expected}")
    by_uid = {node.get("uid"): node for node in finals}
    require(len(by_uid) == len(finals), f"{directory}: duplicate native UIDs")
    for node in finals:
        wanted = "failed" if node.get("method") in FAILURES else "skipped" if node.get("method") in SKIPS else "passed"
        require(STATES[node.get("state")] == wanted, f"{directory}: unexpected outcome {node.attrib}")
    outcomes = Counter(STATES[node.get("state")] for node in finals)
    for outcome in ("passed", "failed", "skipped"):
        require(int(bundle[outcome]) == outcomes[outcome], f"{directory}: inconsistent {outcome} bundle")
    require(int(bundle["nativeExit"]) == (2 if outcomes["failed"] else 0), f"{directory}: wrong native exit")
    reports = list(directory.glob("*.trx"))
    require(len(reports) == 1, f"{directory}: expected one native TRX")
    results = ET.parse(reports[0]).findall(".//t:Results/t:UnitTestResult", NS)
    require(Counter(result.get("testId") for result in results) == Counter(by_uid.keys()), f"{directory}: native TRX UID mismatch")
    row_values = {"DataRows": Counter(), "DynamicRows": Counter()}
    for result in results:
        node = by_uid[result.get("testId")]
        outcome = STATES[node.get("state")]
        require(result.get("outcome") == {"passed": "Passed", "failed": "Failed", "skipped": "NotExecuted"}[outcome],
                f"{directory}: native TRX outcome mismatch")
        stdout = result.findtext("t:Output/t:StdOut", "", NS)
        method = node.get("method")
        if method == "Ignored":
            require("[D INITIALIZE]" not in stdout and "[D CLEANUP]" not in stdout, f"{directory}: ignored lifecycle ran")
        else:
            require(stdout.count("[D INITIALIZE]") == stdout.count("[D CLEANUP]") == 1,
                    f"{directory}: missing or repeated lifecycle for {node.attrib}")
            require("calls=1" in stdout, f"{directory}: incorrect lifecycle count")
        if method in row_values:
            lines = [line for line in stdout.splitlines() if line.startswith(f"[D ROW] {method} value=")]
            require(len(lines) == 1, f"{directory}: row argument evidence missing")
            row_values[method][int(lines[0].partition("value=")[2])] += 1
        if outcome == "failed":
            stack = result.findtext("t:Output/t:ErrorInfo/t:StackTrace", "", NS)
            require("MSTest.SourceGenerated.MSTestReflectionMetadata" in stack, f"{directory}: no generated invocation stack")
    if selection != "diagnostic":
        for method, values in row_values.items():
            require(values == {0: 2, 1: 1}, f"{directory}: lost argument multiplicity for {method}")
        attachments = list((directory / "device-results").rglob("d-proof-attachment.txt"))
        require(attachments, f"{directory}: no exported real attachment")
        require(all(path.read_text() == "D attachment\nline two" for path in attachments), f"{directory}: attachment content mismatch")
        require(any(result.findall(".//t:ResultFile", NS) for result in results), f"{directory}: attachment not linked from TRX")
    events, current = [], {}
    for line in (directory / "instrumentation.txt").read_text().splitlines():
        if line.startswith("INSTRUMENTATION_STATUS: "):
            key, separator, value = line.removeprefix("INSTRUMENTATION_STATUS: ").partition("=")
            require(bool(separator), f"{directory}: invalid protocol field")
            current[key] = value
        elif line.startswith("INSTRUMENTATION_STATUS_CODE: "):
            events.append(current)
            current = {}
    finishes = [event for event in events if event.get("event") == "finish"]
    require(Counter((event["test"], event["outcome"]) for event in finishes) ==
            Counter((uid, STATES[node.get("state")]) for uid, node in by_uid.items()), f"{directory}: protocol UID/outcome mismatch")
    for event in finishes:
        if event["outcome"] == "failed":
            require("intentional" in base64.b64decode(event["message-b64"]).decode(), f"{directory}: failure message not exported")
            require("MSTest.SourceGenerated.MSTestReflectionMetadata" in base64.b64decode(event["stack-b64"]).decode(),
                    f"{directory}: generated stack not exported")
    require(not list((directory / "device-results").rglob("D.Sourcegen.xml")), f"{directory}: temporary observer is still present")
    return Counter((node.get("uid"), node.get("assembly"), node.get("namespace"), node.get("type"),
                    node.get("method"), node.get("display"), STATES[node.get("state")]) for node in finals)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directories", nargs="+", type=Path)
    parser.add_argument("--selection", choices=("all", "proof", "diagnostic"), default="all")
    args = parser.parse_args()
    reference = None
    for directory in args.directories:
        actual = verify(directory, args.selection)
        if reference is not None:
            require(actual == reference, f"{directory}: native identity/multiplicity/outcome changed")
        reference = actual
        print(f"{directory}: {sum(actual.values())} exact native rows verified")


if __name__ == "__main__":
    main()
