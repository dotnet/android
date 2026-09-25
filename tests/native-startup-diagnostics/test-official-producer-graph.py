"""Source-owned conditional expansion, not an Azure/1ES service preview."""
import copy
import argparse
from collections import Counter
import json
import re
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

import yaml

ROOT = Path(__file__).resolve().parents[2]
BASELINE = "d549e1dc4e2a083b08b4f24cb5495e81b99d79b5"
# Fresh publication clones do not contain the original private local implementation history.
subprocess.run(["git", "merge-base", "--is-ancestor", BASELINE, "HEAD"], cwd=ROOT, check=True)


def expand(node, enabled):
    """Evaluate only our mode conditions, preserving unrelated Azure expressions."""
    if isinstance(node, list):
        result = []
        previous = None
        for item in node:
            if isinstance(item, dict) and len(item) == 1:
                key = next(iter(item))
                if key.startswith("${{ if eq(parameters.guestReadiness, "):
                    previous = enabled == ("true" in key)
                    if previous:
                        result.extend(expand(item[key], enabled))
                    continue
                if key == "${{ else }}" and previous is not None:
                    if not previous:
                        result.extend(expand(item[key], enabled))
                    previous = None
                    continue
            previous = None
            result.append(expand(item, enabled))
        return result
    if isinstance(node, dict):
        result = {}
        for key, value in node.items():
            if key.startswith("${{ if eq(parameters.guestReadiness, "):
                if enabled == ("true" in key):
                    result.update(expand(value, enabled))
            else:
                result[key] = expand(value, enabled)
        return result
    return node


def remove_mode_parameters(node):
    if isinstance(node, list):
        return [remove_mode_parameters(x) for x in node
                if not isinstance(x, dict) or x.get("name") not in ("guestReadiness", "guestReadinessAttempt")]
    if isinstance(node, dict):
        return {k: remove_mode_parameters(v) for k, v in node.items()
                if k not in ("guestReadiness", "guestReadinessAttempt") and
                not (k == "parameters" and isinstance(v, dict) and
                     set(v) in ({"guestReadiness"}, {"guestReadiness", "guestReadinessAttempt"}))}
    return node


paths = ["build-tools/automation/azure-pipelines.yaml"] + [
    f"build-tools/automation/yaml-templates/{name}.yaml"
    for name in ("build-macos", "commercial-build", "build-linux")]
for path in paths:
    old = yaml.safe_load(subprocess.check_output(
        ["git", "show", f"{BASELINE}:{path}"], cwd=ROOT))
    new = yaml.safe_load((ROOT / path).read_text())
    assert remove_mode_parameters(expand(new, False)) == old, f"Default graph changed: {path}"

windows_path = "build-tools/automation/yaml-templates/build-windows.yaml"
windows = yaml.safe_load((ROOT / windows_path).read_text())
old_windows = yaml.safe_load(subprocess.check_output(["git", "show", f"{BASELINE}:{windows_path}"], cwd=ROOT))
assert remove_mode_parameters(expand(windows, False)) == old_windows, "Default Windows graph changed"
diagnostic_windows = expand(windows, True)
windows_job = diagnostic_windows["stages"][0]["jobs"][0]
assert windows_job["variables"] == {
    "RestoreConfigFile": r"$(Build.Repository.LocalPath)\NuGet.config",
    "GradleArgs": r'--stacktrace --no-daemon --init-script "$(Build.Repository.LocalPath)\build-tools\scripts\guest-readiness-repositories.gradle"',
}
roslyn_configure = {
    "task": "PowerShell@2",
    "displayName": "Configure unique diagnostic Roslyn outputs",
    "inputs": {
        "pwsh": True, "targetType": "filePath",
        "filePath": "$(Build.Repository.LocalPath)/build-tools/scripts/guest-readiness-roslyn.ps1",
        "arguments": "-Phase Configure",
    },
}
roslyn_capture = {
    "task": "PowerShell@2",
    "displayName": "Retain diagnostic Roslyn outputs and unexplained root SARIF",
    "condition": "always()",
    "inputs": {
        "pwsh": True, "targetType": "filePath",
        "filePath": "$(Build.Repository.LocalPath)/build-tools/scripts/guest-readiness-roslyn.ps1",
        "arguments": '-Phase Capture -Destination "$(Build.StagingDirectory)/Build$(XA.Build.Configuration)/guest-readiness-roslyn"',
    },
}
windows_steps = windows_job["steps"]
assert windows_steps.count(roslyn_configure) == windows_steps.count(roslyn_capture) == 1
test_sources_configure = {
    "task": "PowerShell@2",
    "displayName": "Configure diagnostic test acquisition",
    "inputs": {
        "pwsh": True, "targetType": "filePath",
        "filePath": "$(Build.Repository.LocalPath)/build-tools/scripts/guest-readiness-test-sources.ps1",
    },
}
assert windows_steps.count(test_sources_configure) == 1
assert [step for step in windows_steps if step not in (roslyn_configure, roslyn_capture, test_sources_configure)] == old_windows["stages"][0]["jobs"][0]["steps"], "Normal Windows command sequence changed"
assert windows_steps[windows_steps.index(test_sources_configure) + 1]["template"].endswith("/run-nunit-tests.yaml")
assert windows_steps[windows_steps.index(roslyn_configure) + 1]["displayName"] == "Prepare Solution"
assert windows_steps[windows_steps.index(roslyn_capture) - 1]["parameters"]["displayName"] == "Test PackDotNet"
assert windows_steps[windows_steps.index(roslyn_capture) + 1]["template"].endswith("/upload-results.yaml")
tracked_root_files = subprocess.check_output(
    ["git", "ls-tree", "--name-only", BASELINE], cwd=ROOT, text=True).splitlines()
assert [name for name in tracked_root_files if name.casefold() == "nuget.config"] == ["NuGet.config"]
official_source = (ROOT / "build-tools/scripts/guest-readiness-official.ps1").read_text()
assert "$restoreConfig = Join-Path $root 'NuGet.config'" in official_source

root = yaml.safe_load((ROOT / paths[0]).read_text())
parameters = {x["name"]: x for x in root["parameters"]}
assert parameters["guestReadiness"]["default"] is False
diagnostic = expand(root, True)
assert diagnostic["extends"]["template"] == "azure-pipelines/MicroBuild.1ES.Official.yml@1esPipelines"
assert not any(key.startswith("${{") for key in diagnostic["extends"]), "Diagnostic envelope must not depend on the Real-signing predicate"
variable_template = "/build-tools/automation/yaml-templates/variables.yaml@self"
variable_index = next(i for i, item in enumerate(diagnostic["variables"]) if item.get("template") == variable_template)
host_overrides = {item["name"]: item["value"] for item in diagnostic["variables"][variable_index + 1:]
                  if item.get("name") in ("HostedMacImage", "HostedMacImageWithEmulator")}
assert host_overrides == {"HostedMacImage": "macOS-15", "HostedMacImageWithEmulator": "macOS-15"}
assert not any(item.get("name") in host_overrides for item in expand(root, False)["variables"])
mac = expand(yaml.safe_load((ROOT / paths[1]).read_text()), True)
assert mac["stages"][0]["jobs"][0]["pool"]["${{ else }}"] == {
    "name": "Azure Pipelines", "vmImage": "$(HostedMacImage)",
}, "Keep the demonstrated normal hosted pool schema"
# No stage/job import may silently shadow the root host selection.
mac_consumers = []
for name in ("build-macos", "stage-package-tests", "stage-msbuild-tests",
             "run-msbuild-tests", "stage-msbuild-emulator-tests"):
    pending = [yaml.safe_load((ROOT / f"build-tools/automation/yaml-templates/{name}.yaml").read_text())]
    while pending:
        node = pending.pop()
        if isinstance(node, dict):
            scoped_variables = node.get("variables", {})
            assert isinstance(scoped_variables, dict), f"Review scoped variable imports in {name}"
            assert not {key.casefold() for key in scoped_variables} & {
                key.casefold() for key in host_overrides
            }, f"Stage/job host override shadows root in {name}"
            image = node.get("vmImage", node.get("image"))
            if image in ("$(HostedMacImage)", "$(HostedMacImageWithEmulator)"):
                mac_consumers.append((name, image[2:-1]))
            pending.extend(node.values())
        elif isinstance(node, list):
            pending.extend(node)
assert len(mac_consumers) == 6, "Audit every Mac build/package/MSBuild/emulator pool consumer"
baseline_variables = yaml.safe_load((ROOT / variable_template.removeprefix("/").removesuffix("@self")).read_text())["variables"]
for enabled in (False, True):
    effective_variables = {item["name"]: item["value"] for item in baseline_variables if "name" in item}
    for item in expand(root, enabled)["variables"]:
        if "name" in item:
            effective_variables[item["name"]] = item["value"]
    for name, variable in mac_consumers:
        expected = "macOS-15" if enabled else {
            "HostedMacImage": "macOS-14-arm64", "HostedMacImageWithEmulator": "macOS-14"
        }[variable]
        assert effective_variables[variable] == expected, f"Wrong effective {name} image"
text = json.dumps(diagnostic)
for forbidden in ("nuget-msi-convert", "push_signed_nugets", "PushToMaestro", "darc", "SymbolUploader", "breakglass"):
    assert forbidden not in text, forbidden
stages = diagnostic["extends"]["parameters"]["stages"]
windows_stage = next(item for item in stages if item.get("template") == f"/{windows_path}@self")
assert windows_stage["parameters"] == {"guestReadiness": "${{ parameters.guestReadiness }}"}
prepare = next(x for x in stages if x.get("stage") == "dotnet_prepare_release")
jobs = prepare["jobs"]
signers = [x for x in jobs if x.get("template") == "sign-artifacts/jobs/v4.yml@yaml-templates"]
assert len(signers) == 2
assert prepare["dependsOn"] == ["mac_build", "linux_build"]
oldroot = yaml.safe_load(subprocess.check_output(["git", "show", f"{BASELINE}:{paths[0]}"], cwd=ROOT))
expected_sdl = copy.deepcopy(oldroot["extends"]["parameters"]["sdl"])
expected_sdl["sourceRepositoriesToScan"]["include"].append({"repository": "1esPipelines"})
assert diagnostic["extends"]["parameters"]["sdl"] == expected_sdl, "Diagnostic checkout must add scan coverage without suppressing or changing existing SDL"
assert expand(root, False)["extends"]["parameters"]["sdl"] == oldroot["extends"]["parameters"]["sdl"]
assert any(resource["repository"] == "1esPipelines" and resource["name"] == "1ESPipelineTemplates/MicroBuildTemplate"
           for resource in root["resources"]["repositories"])
oldprepare = next(x for x in oldroot["extends"]["parameters"]["stages"] if x.get("stage") == "dotnet_prepare_release")
assert prepare["condition"] == oldprepare["condition"]
for before, after in zip([x for x in oldprepare["jobs"] if x.get("template") == "sign-artifacts/jobs/v4.yml@yaml-templates"], signers):
    oldparams, newparams = before["parameters"], copy.deepcopy(after["parameters"])
    for added in ("checkoutType", "checkoutPath", "preSignSteps", "postSignSteps"):
        newparams.pop(added, None)
    assert oldparams == newparams, "Existing signing policy changed"
    signing_condition = "${{ if and(startsWith(variables['Build.SourceBranch'], 'refs/heads/release/'), ne(variables['Build.Reason'], 'PullRequest'), eq(variables['Build.DefinitionName'], 'Xamarin.Android')) }}"
    assert newparams[signing_condition] == {"signType": "Real"}
    assert newparams["${{ else }}"] == {"signType": "Test"}
assert "security" in text.lower()
for path in paths[2:]:
    expanded = json.dumps(expand(yaml.safe_load((ROOT / path).read_text()), True))
    assert all(f'"phase": "{p}"' in expanded for p in ("Validate", "Build", "Pack"))

targets = ET.parse(ROOT / "build-tools/create-packs/Directory.Build.targets").getroot()
assert any(x.attrib["Project"].endswith("GuestReadinessPackProperties.targets") for x in targets.findall("Import"))
for target in ("CreateAllPacks", "_CreatePreviewPacks", "_CreateDefaultRefPack"):
    for command in targets.findall(f"./Target[@Name='{target}']/Exec"):
        assert "@(_GlobalProperties, ' ')" in command.attrib["Command"]
sdk = ET.parse(ROOT / "build-tools/create-packs/Microsoft.Android.Sdk.proj").getroot()
assert any("SignList.xml" in x.attrib.get("Include", "") for x in sdk.iter())
sign = yaml.safe_load((ROOT / "build-tools/automation/yaml-templates/guest-readiness-sign.yaml").read_text())
assert sign["steps"][0]["${{ if eq(parameters.phase, 'Output') }}"][0] == {
    "checkout": "1esPipelines", "path": "s/guest-readiness-1es", "persistCredentials": False,
    "condition": "always()",
}, "Retain the actual resolved-resource provenance checkout"
capture = next(step for step in sign["steps"] if step.get("task") == "PowerShell@2")
assert capture["${{ if eq(parameters.phase, 'Output') }}"] == {"condition": "always()"}
assert "condition" not in capture and "continueOnError" not in capture
assert capture["env"]["GUEST_SIGNING_OUTPUT_DIRECTORY"] == "$(Agent.TempDirectory)/artifact-signing/packed"
assert capture["env"]["GUEST_SIGNING_JOB_STATUS"] == "$(Agent.JobStatus)"
output_uploads = sign["steps"][-1]["${{ if eq(parameters.phase, 'Output') }}"]
assert len(output_uploads) == 3
assert not any(step.get("task") == "1ES.PublishPipelineArtifact@1" for step in sign["steps"]), "No pre-sign upload may trigger scans before their targets exist"
assert output_uploads[0] == {
    "task": "1ES.PublishPipelineArtifact@1", "condition": "always()",
    "inputs": {"targetPath": "$(Build.ArtifactStagingDirectory)/guest-readiness-sign-Input",
               "artifactName": "guest-readiness-sign-Input"},
}
assert output_uploads[1]["inputs"] == {
    "targetPath": capture["env"]["GUEST_SIGN_RECEIPT_DIRECTORY"],
    "artifactName": "guest-readiness-sign-Output",
}
assert all(step["condition"] == "always()" for step in output_uploads)
mac_signer = signers[0]["parameters"]
assert mac_signer["preSignSteps"][0]["parameters"]["phase"] == "Input"
assert mac_signer["postSignSteps"][0]["parameters"]["phase"] == "Output"
retained_output = output_uploads[2]
assert retained_output["condition"] == "always()"
assert retained_output["inputs"]["targetPath"] == capture["env"]["GUEST_RETAINED_OUTPUT_DIRECTORY"]
assert retained_output["inputs"]["targetPath"] != capture["env"]["GUEST_SIGNING_OUTPUT_DIRECTORY"]
assert retained_output["inputs"]["artifactName"] == "guest-readiness-signed-output"
build = yaml.safe_load((ROOT / "build-tools/automation/yaml-templates/guest-readiness-build.yaml").read_text())
publisher = yaml.safe_load((ROOT / "build-tools/automation/yaml-templates/publish-artifact.yaml").read_text())
supported_task = publisher["steps"][1]["${{ if eq(parameters.use1ESTemplate, true) }}"][0]["task"]
assert supported_task == "1ES.PublishPipelineArtifact@1"
phase_condition = "${{ if ne(parameters.phase, 'Validate') }}"
for phase in ("Validate", "Build", "Pack"):
    steps = []
    for step in build["steps"]:
        if phase_condition in step:
            if phase != "Validate":
                steps.extend(step[phase_condition])
        else:
            steps.append(step)
    assert all(step.get("task") != "PublishPipelineArtifact@1" for step in steps)
    uploads = [step for step in steps if step.get("task") == supported_task]
    assert len(uploads) == (0 if phase == "Validate" else 1)
    if uploads:
        assert uploads[0] == {
            "task": supported_task,
            "condition": "always()",
            "inputs": {
                "targetPath": "${{ parameters.xaSourcePath }}/bin/guest-readiness-official",
                "artifactName": "guest-readiness-$(Agent.OS)-${{ parameters.phase }}",
            },
        }
        for agent_os in ("Darwin", "Linux"):
            name = uploads[0]["inputs"]["artifactName"].replace("$(Agent.OS)", agent_os).replace("${{ parameters.phase }}", phase)
            assert name == f"guest-readiness-{agent_os}-{phase}"
download = sign["steps"][1]["${{ if eq(parameters.phase, 'Input') }}"][0]
assert download["inputs"]["artifactName"] == "guest-readiness-Darwin-Pack"
out = ROOT / "bin/guest-readiness-official-tests"
out.mkdir(parents=True, exist_ok=True)
(out / "diagnostic-source-graph.json").write_text(json.dumps(diagnostic, indent=2) + "\n")
print("PASS: default graph equality in four templates; diagnostic promotion omission, preserved sign/security/full-pack graph.")
print("PASS: existing 1ES publisher, Validate/Build/Pack retention matrix, always-on failure receipts and exact Darwin/Linux artifact names/sign input.")
print("PASS: diagnostic-only 1esPipelines SDL inclusion; all existing SDL coverage and resolved-resource provenance checkout preserved.")
print("PASS: Output checkout/capture/publication all run on failure; source is normal packed output, with separate retained-output directory and prior job status.")
print("PASS: Input is captured before signing; post-sign publication preserves BinSkim/signing targets while Input-bound AntiMalware follows the snapshot.")
print("PASS: diagnostic Official envelope is independent of unchanged Test/Real signing; both hosted Mac images explicitly use macOS-15 after baseline variables, with default-off selection unchanged.")
print("PASS: all six Mac pool consumers resolve macOS-15 in diagnostic mode and original labels when off; stage/job scopes cannot silently shadow root images.")
print("PASS: diagnostic Windows job selects its actual self checkout config without changing commands; default Windows graph and canonical Git NuGet.config casing preserved.")

def assert_signing_order(steps):
    """Share the actual-preview assertions with deterministic provider-shaped cases."""
    positions = {}
    for name in ("Guest readiness signing Input evidence", "Sign Package Contents",
                 "Verify NuGet Packages", "Copy Signed Output", "Guest readiness signing Output evidence"):
        matches = [i for i, step in enumerate(steps) if step.get("displayName") == name]
        assert len(matches) == 1, f"Missing or repeated normal signing step: {name}"
        positions[name] = matches[0]
    assert list(positions.values()) == sorted(positions.values()), "Normal signing/capture order changed"
    scans = [i for i, step in enumerate(steps) if re.search(r"(?:^|\.)BinSkim@[0-9]+$", step.get("task", ""))]
    assert len(scans) == 5, f"Expected five BinSkim tasks, found {len(scans)}"
    assert min(scans) > positions["Copy Signed Output"], "Binary scans precede normal signed-output production"
    for artifact in ("guest-readiness-sign-Input", "guest-readiness-sign-Output", "guest-readiness-signed-output"):
        uploads = [(i, step) for i, step in enumerate(steps)
                   if step.get("task") == "PublishPipelineArtifact@1"
                   and step.get("inputs", {}).get("artifactName") == artifact]
        assert len(uploads) == 1 and uploads[0][0] > positions["Guest readiness signing Output evidence"], artifact
        assert uploads[0][1]["condition"] == "always()", artifact


qualified_binskim = "securedevelopmentteam.vss-secure-development-tools.build-task-binskim.BinSkim@4"
ordered_fixture = [{"displayName": name} for name in (
    "Guest readiness signing Input evidence", "Sign Package Contents",
    "Verify NuGet Packages", "Copy Signed Output", "Guest readiness signing Output evidence")]
ordered_fixture += [{"task": qualified_binskim} for _ in range(5)]
ordered_fixture += [{"task": "PublishPipelineArtifact@1", "condition": "always()",
                     "inputs": {"artifactName": name}} for name in (
                         "guest-readiness-sign-Input", "guest-readiness-sign-Output", "guest-readiness-signed-output")]
assert_signing_order(ordered_fixture)
short_fixture = copy.deepcopy(ordered_fixture)
for step in short_fixture:
    if step.get("task") == qualified_binskim:
        step["task"] = "BinSkim@4"
assert_signing_order(short_fixture)
early_fixture = copy.deepcopy(ordered_fixture)
early_fixture.insert(1, early_fixture.pop(5))
for fixture, expected_error in (
    (early_fixture, "Binary scans precede normal signed-output production"),
    ([step for step in ordered_fixture if step.get("task") != qualified_binskim],
     "Expected five BinSkim tasks, found 0"),
):
    try:
        assert_signing_order(fixture)
    except AssertionError as error:
        assert str(error) == expected_error
    else:
        raise AssertionError("Invalid provider-shaped fixture was accepted")
print("PASS: modeled provider-shaped ordering accepts five qualified/short scanners after signing; early and missing scanners fail distinct assertions.")


def assert_protected_signing_tasks(before, after):
    """Permit only the single artifact-bound AntiMalware Input snapshot relocation."""
    protected = []
    for steps in (before, after):
        protected.append(Counter(json.dumps(step, sort_keys=True) for step in steps
                                 if "Guardian:" in step.get("displayName", "")
                                 or step.get("displayName") in ("Sign Package Contents", "Sign NuGet Packages",
                                                               "Verify NuGet Packages", "Copy Signed Output")
                                 or step.get("task", "").startswith("MicroBuildSigningPlugin@")))
    if protected[0] == protected[1]:
        return
    assert [sum(tasks.values()) for tasks in protected] == [49, 49], "Protected task counts changed"
    removed = list((protected[0] - protected[1]).elements())
    added = list((protected[1] - protected[0]).elements())
    assert len(removed) == len(added) == 1, "More than the single Input alias changed"
    old_step, new_step = json.loads(removed[0]), json.loads(added[0])
    assert old_step["task"] == "securedevelopmentteam.vss-secure-development-tools.build-task-antimalware.AntiMalware@4"
    paths = ("$(Build.ArtifactStagingDirectory)/guest-readiness-sign",
             "$(Build.ArtifactStagingDirectory)/guest-readiness-sign-Input")
    for steps, scanner, path in zip((before, after), (old_step, new_step), paths):
        publishers = [step for step in steps if step.get("task") == "PublishPipelineArtifact@1"
                      and step.get("inputs", {}).get("artifactName") == "guest-readiness-sign-Input"]
        assert len(publishers) == 1, "Input publisher must be unique"
        assert publishers[0]["inputs"]["targetPath"] == path, "Input publisher alias differs"
        assert scanner["inputs"]["FileDirPath"] == path, "AntiMalware target differs from exact Input alias"
    old_step["inputs"]["FileDirPath"] = paths[1]
    assert old_step == new_step, "Other AntiMalware policy or task fields changed"


alias_before = [{"task": "securedevelopmentteam.vss-secure-development-tools.build-task-antimalware.AntiMalware@4",
                 "displayName": "Guardian: AntiMalware Scanner (Binary)",
                 "inputs": {"FileDirPath": "$(Build.ArtifactStagingDirectory)/guest-readiness-sign"}}]
alias_before += [{"displayName": f"Guardian: unchanged fixture {i}"} for i in range(48)]
alias_before += [{"task": "PublishPipelineArtifact@1",
                  "inputs": {"artifactName": "guest-readiness-sign-Input",
                             "targetPath": "$(Build.ArtifactStagingDirectory)/guest-readiness-sign"}}]
alias_after = copy.deepcopy(alias_before)
alias_after[0]["inputs"]["FileDirPath"] += "-Input"
alias_after[-1]["inputs"]["targetPath"] += "-Input"
assert_protected_signing_tasks(alias_before, alias_after)
for invalid_path in ("$(Build.ArtifactStagingDirectory)",  # broader
                     "$(Build.ArtifactStagingDirectory)/guest-readiness-sign-Input/subdir",  # narrower
                     "$(Build.ArtifactStagingDirectory)/unrelated"):
    invalid = copy.deepcopy(alias_after)
    invalid[0]["inputs"]["FileDirPath"] = invalid[-1]["inputs"]["targetPath"] = invalid_path
    try:
        assert_protected_signing_tasks(alias_before, invalid)
    except AssertionError:
        pass
    else:
        raise AssertionError("Arbitrary/broader/narrower scanner alias was accepted")
for mutation in ("policy", "count", "publisher"):
    invalid = copy.deepcopy(alias_after)
    if mutation == "policy":
        invalid[0]["continueOnError"] = True
    elif mutation == "count":
        invalid.insert(0, copy.deepcopy(invalid[0]))
    else:
        invalid[-1]["inputs"]["targetPath"] = "different"
    try:
        assert_protected_signing_tasks(alias_before, invalid)
    except AssertionError:
        pass
    else:
        raise AssertionError(f"Invalid alias {mutation} was accepted")
print("PASS: modeled AntiMalware alias is bound to the unique Input publisher; other 48 tasks and every non-path field stay exact.")

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--expanded-preview", type=Path)
parser.add_argument("--baseline-preview", type=Path)
args = parser.parse_args()
if args.baseline_preview and not args.expanded_preview:
    parser.error("--baseline-preview requires --expanded-preview")
if args.expanded_preview:
    preview = yaml.safe_load(args.expanded_preview.read_text(encoding="utf-8-sig"))
    sign_jobs = [job for stage in preview["stages"] for job in stage.get("jobs", [])
                 if any(step.get("displayName") == "Guest readiness signing Input evidence"
                        for step in job.get("steps", []))]
    assert len(sign_jobs) == 1, "Exactly one diagnostic signing job required"
    steps = sign_jobs[0]["steps"]
    assert_signing_order(steps)
    if args.baseline_preview:
        before = yaml.safe_load(args.baseline_preview.read_text(encoding="utf-8-sig"))
        old_jobs = [job for stage in before["stages"] for job in stage.get("jobs", [])
                    if job.get("job") == sign_jobs[0]["job"]]
        assert len(old_jobs) == 1
        assert_protected_signing_tasks(old_jobs[0]["steps"], steps)
    print("PASS: supplied service-expanded preview defers scans until after normal signing; all guest uploads remain always-on.")
