#!/usr/bin/env python3
"""Enriched failure analysis for one dnceng-public `dotnet-android` build:
  1. cross-config matrix per failed test (failed/passed/retried configs) + stack/asserts
  2. crashed / incomplete lanes (started-but-not-finished culprit lives in logcat)
  3. branch cross-reference (PR changes that name a failing test's class/namespace/assembly)

Needs `az login`. Usage: ci_failures.py --build-id N [--pr N] [--repo dotnet/android]
"""
import json, subprocess, sys, argparse, re
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor

ORG = "https://dev.azure.com/dnceng-public"
PROJECT = "public"
RES = "499b84ac-1321-427f-aa17-267ca6975798"


def az_json(url):
    p = subprocess.run(["az", "rest", "--method", "get", "--resource", RES,
                        "--url", url, "-o", "json"], capture_output=True, text=True)
    if p.returncode != 0:
        sys.stderr.write(f"az error {url}\n{p.stderr[:300]}\n")
        return None
    try:
        return json.loads(p.stdout)
    except json.JSONDecodeError:
        return None


def run_results(rid):
    data = az_json(f"{ORG}/{PROJECT}/_apis/test/Runs/{rid}/results?api-version=7.1&$top=5000")
    out = {}
    for row in (data or {}).get("value", []):
        n = row.get("automatedTestName")
        if n:
            out[n] = (row.get("outcome"), row.get("errorMessage"), row.get("stackTrace"))
    return rid, out


def fetch_all(rids, workers=6):
    if not rids:
        return {}
    with ThreadPoolExecutor(max_workers=workers) as ex:
        return dict(ex.map(run_results, rids))


def base_of(name):
    """Strip flavor/OS/index suffix so sibling configs share one base.
    'Mono.Android.NET_Tests-NativeAOT' -> 'Mono.Android.NET_Tests';
    'Xamarin.Android.Build.Tests - macOS-7' -> 'Xamarin.Android.Build.Tests'."""
    b = re.sub(r' - (macOS|Windows|Linux)(-\d+)?$', '', name)
    b = re.sub(r'-[A-Za-z0-9]+$', '', b)
    return b


# ---------------- section 1: cross-config matrix ----------------
def section_matrix(bid, failed, runs, run_by_id):
    fail_runs, storage = defaultdict(set), {}
    for f in failed:
        fail_runs[f["automatedTestName"]].add(f["runId"])
        storage[f["automatedTestName"]] = f.get("automatedTestStorage")

    def first_base(rids):
        for r in rids:
            if r in run_by_id:
                return base_of(run_by_id[r]["name"])
        return ""
    fam = {n: first_base(rids) for n, rids in fail_runs.items()}
    cand = defaultdict(list)
    for fk in set(fam.values()):
        for r in runs:
            if base_of(r["name"]) == fk:
                cand[fk].append(r)
    cache = fetch_all(list({r["id"] for fk in fam.values() for r in cand[fk]}))

    print(f"## Failed-test cross-config matrix — {len(fail_runs)} distinct test(s)\n")
    for n in sorted(fail_runs):
        fk = fam[n]
        cfg = defaultdict(list)
        for r in cand[fk]:
            row = cache.get(r["id"], {}).get(n)
            if row:
                cfg[r["name"]].append((r.get("completedDate") or "", row[0]))
        short, ns = n.rsplit(".", 1)[-1], n.rsplit(".", 1)[0]
        print(f"### `{short}`  ({ns})")
        print(f"- assembly `{storage.get(n)}` · family `{fk}`")
        fl, pa, ot = [], [], []
        for name in sorted(cfg):
            outs = [o for _, o in sorted(cfg[name])]
            label = name[len(fk):].lstrip(" -") or name
            disp = "->".join(outs) + " (retry)" if len(set(outs)) > 1 else outs[0]
            (fl if "Failed" in outs else pa if set(outs) == {"Passed"} else ot).append(
                f"`{label}`" + ("" if disp == "Passed" else f" ({disp})"))
        print(f"- FAILED in: {', '.join(fl) or '-'}")
        print(f"- passed in: {', '.join(pa) or '-'}")
        if ot:
            print(f"- other: {', '.join(ot)}")
        for rid in fail_runs[n]:
            row = cache.get(rid, {}).get(n)
            if row and row[1]:
                print(f"- assert/error: {row[1].strip().splitlines()[0][:300]}")
                if row[2]:
                    print("  ```")
                    for ln in row[2].strip().splitlines()[:6]:
                        print("  " + ln[:200])
                    print("  ```")
                break
        print()


# ---------------- section 2: crashed / incomplete lanes ----------------
def section_crashes(bid, runs, timeline):
    recs = timeline.get("records", [])
    published = {r["name"]: r for r in runs}
    crashed = []
    # incomplete test runs (runner died mid-run)
    for r in runs:
        inc = r.get("incompleteTests") or 0
        if inc > 0:
            crashed.append((r["name"], f"{inc} test(s) did not complete - runner died mid-run"))
    # "run <flavor>" tasks that did not cleanly succeed AND published no (complete) results = crash/zero-tests
    for rec in recs:
        if rec.get("type") == "Task" and (rec.get("name") or "").startswith("run ") \
                and rec.get("result") in ("failed", "succeededWithIssues", "canceled"):
            flavor = rec["name"][4:].strip()
            run = published.get(flavor)
            if run is None or (run.get("incompleteTests") or 0) > 0:
                crashed.append((flavor, f"`run` task {rec['result']} but no complete test run published - app likely crashed ('Zero tests ran' / native crash)"))
    # job-level timeouts (hang)
    for rec in recs:
        if rec.get("type") == "Job" and rec.get("result") == "canceled":
            msg = " ".join(i.get("message", "") for i in (rec.get("issues") or []))
            m = re.search(r"maximum time of (\d+) minutes", msg)
            if m:
                crashed.append((rec["name"], f"timed out at {m.group(1)}-min cap - likely a hung test; last started test in logcat is the suspect"))
    if not crashed:
        return
    print("## Crashed / incomplete lanes  (!)\n")
    print("These went red with **no usable failed-test list** - the culprit (a test that **started but never "
          "finished**, or a native crash) is only in the device **logcat**, not the test API:\n")
    seen = set()
    for name, why in crashed:
        if (name, why) in seen:
            continue
        seen.add((name, why))
        print(f"- **{name}** - {why}")
    print()
    print("To name the culprit, list this build's artifacts and download the matching `Test Results - ...` lane "
          "(large: 100MB-2GB - prefer a `Debug` lane), then scan its logcat (see references/azdo-queries.md):\n")
    print("```bash")
    print(f'az pipelines runs artifact list --run-id {bid} --org {ORG} --project {PROJECT} \\')
    print(r"  --query '[?starts_with(name, `Test Results`)].name' -o tsv")
    print(f'az pipelines runs artifact download --run-id {bid} --org {ORG} --project {PROJECT} \\')
    print('  --artifact-name "<paste matching Test Results - ... name>" --path /tmp/cilogs')
    print(r"grep -nE 'Running |\[PASS\]|\[FAIL\]|SIGSEGV|SIGABRT|tombstone|FATAL|art::|JNI DETECTED|Process .*died' \\")
    print('  /tmp/cilogs/**/logcat-*.txt | tail -60   # last test that STARTED with no PASS/FAIL = crasher')
    print("```\n")


# ---------------- section 3: branch cross-reference ----------------
def section_xref(failed, repo, pr):
    names = sorted({f["automatedTestName"] for f in failed})
    if not names:
        return
    p = subprocess.run(["gh", "pr", "diff", str(pr), "--repo", repo, "--name-only"],
                       capture_output=True, text=True)
    if p.returncode != 0:
        sys.stderr.write(f"gh diff failed: {p.stderr[:200]}\n")
        return
    files = [f for f in p.stdout.splitlines() if f.strip()]
    stems = {f.rsplit("/", 1)[-1].rsplit(".", 1)[0]: f for f in files}
    print("## Branch cross-reference\n")
    print(f"PR #{pr} changes {len(files)} file(s). Name overlaps with failing tests (judge if causal):\n")
    any_hit = False
    for n in names:
        parts = n.split(".")
        cls = parts[-2] if len(parts) >= 2 else ""
        method, ns = parts[-1], ".".join(parts[:-2])
        hits = set()
        for stem, path in stems.items():
            if stem and (stem == cls or stem == method or stem in ns.split(".") or (cls and cls in path)):
                hits.add(path)
        if hits:
            any_hit = True
            print(f"- `{cls}.{method}` <- {', '.join('`'+h+'`' for h in sorted(hits)[:5])}")
    if not any_hit:
        print("- No direct file-name overlap. Check whether changed runtime/build code affects the failing assembly.")
    print()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--build-id", required=True)
    ap.add_argument("--pr")
    ap.add_argument("--repo", default="dotnet/android")
    args = ap.parse_args()
    bid = args.build_id

    failed = (az_json(f"{ORG}/{PROJECT}/_apis/test/ResultsByBuild?buildId={bid}&outcomes=Failed&api-version=7.1-preview") or {}).get("value", [])
    runs = (az_json(f"{ORG}/{PROJECT}/_apis/test/runs?buildUri=vstfs:///Build/Build/{bid}&api-version=7.1&includeRunDetails=true") or {}).get("value", [])
    timeline = az_json(f"{ORG}/{PROJECT}/_apis/build/builds/{bid}/timeline?api-version=7.1") or {}
    run_by_id = {r["id"]: r for r in runs}

    print(f"# Failure analysis - build {bid}\n")
    if failed:
        section_matrix(bid, failed, runs, run_by_id)
    else:
        print("_No failed tests in the test API (build may still be red via crash/timeout below)._\n")
    section_crashes(bid, runs, timeline)
    if args.pr:
        section_xref(failed, args.repo, args.pr)


if __name__ == "__main__":
    main()
