# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Fails on DevSkim banned-function findings.
#
# Exit codes: 0 = no findings, 1 = findings, 2 = a report is missing or malformed,
# or has no results although --require-results lists it. With --expect-findings, 0
# and 1 are the other way around. DevSkim itself exits with 0 when it reports
# findings and when it analyzes nothing, so the SARIF reports are the only reliable
# result.

import argparse
import json
import sys
from pathlib import Path


BANNED_FUNCTION_TAG = "API.DangerousAPI.BannedFunction"


def banned_findings(document):
    if not isinstance(document, dict) or document.get("version") != "2.1.0":
        raise ValueError("Expected SARIF 2.1.0")
    runs = document.get("runs")
    if not isinstance(runs, list) or not runs:
        raise ValueError("Expected at least one SARIF run")

    findings = []
    for run in runs:
        if not isinstance(run, dict):
            raise ValueError("Expected a DevSkim run")
        tool = run.get("tool")
        driver = tool.get("driver") if isinstance(tool, dict) else None
        if not isinstance(driver, dict) or driver.get("name") != "devskim":
            raise ValueError("Expected a DevSkim run")
        results = run.get("results")
        if not isinstance(results, list):
            raise ValueError("Expected a results array")
        for result in results:
            if not isinstance(result, dict) or not isinstance(result.get("ruleId"), str):
                raise ValueError("Expected a result with a ruleId")
            properties = result.get("properties")
            tags = properties.get("tags") if isinstance(properties, dict) else None
            # DevSkim 1.0.100 writes rule tags on results, not driver.rules.
            if not isinstance(tags, list) or not all(isinstance(tag, str) for tag in tags):
                raise ValueError("Expected DevSkim result tags")
            if BANNED_FUNCTION_TAG in tags:
                findings.append(result)
    return findings


def result_count(document):
    # Only valid for a document that banned_findings accepted.
    return sum(len(run["results"]) for run in document["runs"])


def main(argv=None):
    parser = argparse.ArgumentParser(description="Fail on DevSkim banned-function findings, at any severity.")
    parser.add_argument(
        "--expect-findings",
        action="store_true",
        help="Fail unless a banned-function finding is reported. Use it on a file with a known banned call, to prove that the scanner works.",
    )
    parser.add_argument(
        "--require-results",
        type=Path,
        action="append",
        default=[],
        metavar="REPORT",
        help="Fail if this report has no results at all, which means that DevSkim probably analyzed no files. "
        "The report must also be listed as a report. Can be repeated.",
    )
    parser.add_argument("reports", type=Path, nargs="+")
    args = parser.parse_args(argv)
    required = {path.resolve() for path in args.require_results}
    if not required <= {path.resolve() for path in args.reports}:
        parser.error("--require-results takes reports that are also listed as reports")
    count = 0
    for report in args.reports:
        try:
            document = json.loads(report.read_text(encoding="utf-8"))
            findings = banned_findings(document)
        except (OSError, ValueError) as error:
            print(f"DevSkim report error in {report}: {error}", file=sys.stderr)
            return 2
        if report.resolve() in required and not result_count(document):
            print(f"DevSkim reported nothing at all in {report}, so it probably did not analyze any files.", file=sys.stderr)
            return 2
        for finding in findings:
            location = (finding.get("locations") or [{}])[0].get("physicalLocation", {})
            uri = location.get("artifactLocation", {}).get("uri", str(report))
            line = location.get("region", {}).get("startLine", 1)
            message = finding.get("message", {}).get("text", "Banned API detected")
            print(f"{uri}:{line}: {finding['ruleId']}: {message}")
        count += len(findings)
    print(f"DevSkim: {count} banned-function finding(s). Full results are in the SARIF reports.")
    if args.expect_findings:
        if not count:
            print("DevSkim did not report the known banned function, so a clean scan would prove nothing.", file=sys.stderr)
        return 0 if count else 1
    return 1 if count else 0


if __name__ == "__main__":
    sys.exit(main())
