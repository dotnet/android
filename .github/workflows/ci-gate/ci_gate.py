#!/usr/bin/env python3
"""Report the `dotnet-android-gate` commit status for a PR head commit.

The `dotnet-android` check mirrors the Azure DevOps build result, so a lost
agent or a DNS outage turns a PR red even though the PR did not cause it. This
gate reads the build timeline and the failed-task logs and reports:

* success  - the build succeeded;
* failure  - at least one failed job is not a known infrastructure failure
             (test failures, compiler errors, timeouts, anything unrecognized);
* pending  - inconclusive: every failed job is a known infrastructure failure.
             When an Azure DevOps token is available, the failed stages are
             retried, up to MAX_STAGE_ATTEMPTS attempts per stage.

Inconclusive is never success: the PR stays unmergeable until a real pass.
"""

import argparse
import json
import os
import re
import urllib.error
import urllib.request

AZDO = "https://dev.azure.com/dnceng-public/public/_apis/build/builds"
DEFINITION_ID = "333"
CHECK_NAME = "dotnet-android"
CONTEXT = "dotnet-android-gate"
MAX_STAGE_ATTEMPTS = 3
LOG_TAIL_CHARS = 256 * 1024

# Matched against the job's own issues: the job died, whatever ran inside it.
AGENT_LOST = re.compile(
    r"We stopped hearing from agent|lost communication with the server|"
    r"The agent did not connect within the alloted time|cancelled due to worker timeout"
)

# Evidence that the tests or the build itself failed. These win over any
# infrastructure marker found in the same job.
CONCLUSIVE = [
    ("tests", re.compile(
        r"Failed!\s+- Failed:\s+[1-9]|Test Run Failed|Tests failed|\[FAIL\]|"
        r"Test run summary: Failed|^\s*failed:\s*[1-9]", re.M)),
    ("build", re.compile(
        r"error (CS|MSB|NETSDK|NU1102|XA|WMC)\d+|Error (CS|WMC)\d+|git tree has modified")),
    ("timeout", re.compile(r"ran longer than the maximum time")),
]

INFRA = [
    ("dns", re.compile(
        r"Temporary failure resolving|Temporary failure in name resolution|Could not resolve host|"
        r"nodename nor servname provided|Name or service not known|No such host is known")),
    ("network", re.compile(
        r"Connection reset by peer|The SSL connection could not be established|"
        r"An existing connection was forcibly closed|No route to host")),
    ("azdo-service", re.compile(
        r"##\[error\]Service Unavailable|503 \(Service Unavailable\)|##\[error\]Too Many Requests|"
        r"##\[error\]The HTTP request timed out")),
    ("agent-startup", re.compile(r"ManagedPSEntry\+StartupException|Call to 'procargs' failed")),
    ("download", re.compile(r"Server returned HTTP response code: 5\d\d for URL: https://github\.com/gradle")),
]


def http(url, method="GET", token=None, body=None, text=False):
    headers = {"Accept": "text/plain" if text else "application/json", "User-Agent": CONTEXT}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=data, method=method, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        payload = response.read()
    if text:
        return payload.decode("utf-8", "replace")
    return json.loads(payload) if payload else None


def azdo_get(path, text=False):
    separator = "&" if "?" in path else "?"
    return http(f"{AZDO}/{path}{separator}api-version=7.1", text=text)


def classify_job(job, descendants, read_log):
    """Return ("infra" | "conclusive", category) for a failed job."""
    job_issues = "\n".join(i.get("message", "") for i in job.get("issues") or [])
    if AGENT_LOST.search(job_issues):
        return "infra", "agent-lost"

    evidence = [job_issues]
    for record in descendants:
        if record.get("result") not in ("failed", "succeededWithIssues", "canceled"):
            continue
        evidence.extend(i.get("message", "") for i in record.get("issues") or [])
        log_id = (record.get("log") or {}).get("id")
        if log_id:
            evidence.append(read_log(log_id))
    evidence = "\n".join(evidence)

    for category, pattern in CONCLUSIVE:
        if pattern.search(evidence):
            return "conclusive", category
    for category, pattern in INFRA:
        if pattern.search(evidence):
            return "infra", category
    return "conclusive", "unknown"


def evaluate(records, read_log):
    """Classify every failed job of the latest attempt.

    Returns (failures, stages): failures is a list of (stage, job, kind,
    category); stages maps the identifier of each stage that contains a failed
    job to that stage's attempt number.
    """
    by_id = {r["id"]: r for r in records}
    children = {}
    for record in records:
        children.setdefault(record.get("parentId"), []).append(record)

    def descendants(record_id):
        for child in children.get(record_id, []):
            yield child
            yield from descendants(child["id"])

    def stage_of(record):
        while record and record.get("type") != "Stage":
            record = by_id.get(record.get("parentId"))
        return record or {}

    failures = []
    stages = {}
    for job in records:
        if job.get("type") != "Job" or job.get("result") != "failed":
            continue
        stage = stage_of(job)
        kind, category = classify_job(job, list(descendants(job["id"])), read_log)
        failures.append((stage.get("name", "?"), job.get("name", "?"), kind, category))
        if stage.get("identifier"):
            stages[stage["identifier"]] = stage.get("attempt", 1)
    return failures, stages


def find_build(repo, sha, gh_token):
    """Return the newest definition-333 build reported on the commit, if any."""
    runs = http(
        f"https://api.github.com/repos/{repo}/commits/{sha}/check-runs?check_name={CHECK_NAME}&per_page=100",
        token=gh_token)
    builds = []
    for run in runs.get("check_runs", []):
        parts = (run.get("external_id") or "").split("|")
        if run.get("app", {}).get("slug") == "azure-pipelines" and len(parts) > 1 and parts[0] == DEFINITION_ID:
            builds.append((run.get("started_at") or "", int(parts[1])))
    return max(builds)[1] if builds else None


def gate(build_id, azdo_token, dry_run):
    """Return (state, description), retrying infra-only failures when possible."""
    build = azdo_get(f"{build_id}")
    if build["status"] != "completed":
        return "pending", "dotnet-android is running"
    if build["result"] in ("succeeded", "partiallySucceeded"):
        return "success", "dotnet-android passed"
    if build["result"] != "failed":
        return "failure", f"dotnet-android {build['result']}"

    def read_log(log_id):
        try:
            return azdo_get(f"{build_id}/logs/{log_id}", text=True)[-LOG_TAIL_CHARS:]
        except urllib.error.HTTPError:
            return ""

    failures, stages = evaluate(azdo_get(f"{build_id}/timeline")["records"], read_log)
    for stage, job, kind, category in failures:
        print(f"{kind:10} {category:14} {stage} > {job}")
    if not failures:
        return "failure", "dotnet-android failed"

    conclusive = [f for f in failures if f[2] == "conclusive"]
    if conclusive:
        _, job, _, category = conclusive[0]
        more = f" (+{len(conclusive) - 1} more)" if len(conclusive) > 1 else ""
        return "failure", f"{category} failure: {job}{more}"[:140]

    causes = ", ".join(sorted({f[3] for f in failures}))
    if any(attempt >= MAX_STAGE_ATTEMPTS for attempt in stages.values()):
        return "pending", f"Inconclusive ({causes}) after {MAX_STAGE_ATTEMPTS} attempts; retry manually"[:140]
    if not azdo_token or dry_run:
        return "pending", f"Inconclusive ({causes}); retry failed jobs in Azure DevOps"[:140]

    for stage in sorted(stages):
        http(f"{AZDO}/{build_id}/stages/{stage}?api-version=7.1", method="PATCH", token=azdo_token,
             body={"state": "retry", "forceRetryAllJobs": False})
        print(f"Retried stage {stage}")
    return "pending", f"Inconclusive ({causes}); retrying {len(stages)} stage(s)"[:140]


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--sha", help="commit to report the status on")
    parser.add_argument("--build-id", type=int, help="evaluate this build instead of looking it up")
    parser.add_argument("--dry-run", action="store_true", help="neither retry nor post a status")
    args = parser.parse_args()
    if not args.sha and args.build_id is None:
        parser.error("--sha or --build-id is required")

    repo = os.environ.get("GITHUB_REPOSITORY", "dotnet/android")
    gh_token = os.environ.get("GH_TOKEN", "")
    build_id = args.build_id if args.build_id is not None else find_build(repo, args.sha, gh_token)

    body = {"context": CONTEXT}
    if build_id is None:
        body.update(state="pending", description="Waiting for dotnet-android")
    else:
        state, description = gate(build_id, os.environ.get("AZDO_TOKEN", ""), args.dry_run)
        body.update(state=state, description=description,
                    target_url=f"https://dev.azure.com/dnceng-public/public/_build/results?buildId={build_id}")
    print(f"{body['state']}: {body['description']}")

    if args.sha and not args.dry_run:
        http(f"https://api.github.com/repos/{repo}/statuses/{args.sha}", method="POST", token=gh_token, body=body)


if __name__ == "__main__":
    main()
