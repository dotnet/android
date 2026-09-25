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
spmi_output_variable = "GDNP_1ESSECRETSCANNING_OUTPUT"
spmi_output_value = r"$(Agent.TempDirectory)\guest-readiness-spmi.sarif"
retention_variable = "XA.PublishAllLogs"
expected_windows_variables = {
    "RestoreConfigFile": r"$(Build.Repository.LocalPath)\NuGet.config",
    "GradleArgs": r'--stacktrace --no-daemon --init-script "$(Build.Repository.LocalPath)\build-tools\scripts\guest-readiness-repositories.gradle"',
    spmi_output_variable: spmi_output_value,
    retention_variable: "true",
}


def assert_windows_source_variables(variables):
    # Source typing stays strict even though the provider serializes this value unquoted.
    assert variables == expected_windows_variables, "Expected exact source string variables"


assert_windows_source_variables(windows_job["variables"])
source_bool_variables = copy.deepcopy(windows_job["variables"])
source_bool_variables[retention_variable] = True
try:
    assert_windows_source_variables(source_bool_variables)
except AssertionError:
    pass
else:
    raise AssertionError("Source boolean retention accepted instead of quoted string")
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
root_observer = {
    "task": "PowerShell@2",
    "displayName": "Observe diagnostic root SARIF metadata before clean gate",
    "condition": "always()",
    "continueOnError": True,
    "inputs": {
        "pwsh": True, "targetType": "filePath",
        "filePath": "$(Build.Repository.LocalPath)/build-tools/scripts/guest-readiness-roslyn.ps1",
        "arguments": "-Phase ObserveRoot",
    },
}
assert windows_steps.count(root_observer) == 1
assert [step for step in windows_steps if step not in (roslyn_configure, roslyn_capture, test_sources_configure, root_observer)] == old_windows["stages"][0]["jobs"][0]["steps"], "Normal Windows command sequence changed"
assert windows_steps[windows_steps.index(test_sources_configure) + 1]["template"].endswith("/run-nunit-tests.yaml")
assert windows_steps[windows_steps.index(roslyn_configure) + 1]["displayName"] == "Prepare Solution"
assert windows_steps[windows_steps.index(roslyn_capture) - 1]["parameters"]["displayName"] == "Test PackDotNet"
assert windows_steps[windows_steps.index(roslyn_capture) + 1]["template"].endswith("/upload-results.yaml")
assert windows_steps[windows_steps.index(root_observer) - 1]["template"].endswith("/upload-results.yaml")
assert windows_steps[windows_steps.index(root_observer) + 1]["template"].endswith("/fail-on-dirty-tree.yaml")
clean_path = "build-tools/automation/yaml-templates/fail-on-dirty-tree.yaml"
assert (ROOT / clean_path).read_text() == subprocess.check_output(
    ["git", "show", f"{BASELINE}:{clean_path}"], cwd=ROOT, text=True), "Clean gate changed"


def assert_root_observer_order(steps):
    observers = [i for i, step in enumerate(steps) if step.get("displayName") == root_observer["displayName"]]
    clean = [i for i, step in enumerate(steps) if step.get("displayName") == "Ensure no modified/untracked files"]
    assert len(observers) == len(clean) == 1, "Expected one observer and one clean gate"
    position = observers[0]
    for key, value in root_observer.items():
        assert steps[position].get(key) == value, f"Observer field changed: {key}"
    assert position + 1 == clean[0], "Observer must immediately precede unchanged clean gate"
    assert steps[clean[0]].get("condition") == "succeeded()", "Clean condition changed"
    assert steps[position - 1].get("task") == "PublishPipelineArtifact@1", "Build Results publication must precede observation"
    for identity in ("SdtReport", "PostAnalysis"):
        positions = [i for i, step in enumerate(steps) if re.search(rf"(?:^|\.){identity}@", step.get("task", ""))]
        assert len(positions) == 1 and positions[0] < position, f"{identity} must precede observation"
    publishers = [i for i, step in enumerate(steps) if re.search(r"(?:^|\.)PublishSecurityAnalysisLogs@", step.get("task", ""))]
    assert len(publishers) == 1 and publishers[0] > clean[0], "Normal SDL log publisher must remain after clean"


observer_fixture = [
    {"task": "provider.SdtReport@2"}, {"task": "provider.PostAnalysis@2"},
    {"task": "PublishPipelineArtifact@1"}, copy.deepcopy(root_observer),
    {"task": "PowerShell@2", "displayName": "Ensure no modified/untracked files", "condition": "succeeded()"},
    {"task": "provider.PublishSecurityAnalysisLogs@3"},
]
assert_root_observer_order(observer_fixture)
for mutation in ("observer-failure", "clean-condition", "early-observer", "early-publisher", "missing-observer"):
    invalid = copy.deepcopy(observer_fixture)
    if mutation == "observer-failure":
        invalid[3]["continueOnError"] = False
    elif mutation == "clean-condition":
        invalid[4]["condition"] = "always()"
    elif mutation == "early-observer":
        invalid.insert(0, invalid.pop(3))
    elif mutation == "early-publisher":
        invalid.insert(0, invalid.pop(5))
    else:
        invalid.pop(3)
    try:
        assert_root_observer_order(invalid)
    except AssertionError:
        pass
    else:
        raise AssertionError(f"Invalid observer graph accepted: {mutation}")
print("PASS: diagnostic observer alone continues on error immediately before unchanged clean; SDL log publication remains later.")


def assert_windows_evidence_variable_change(before, after, variable=spmi_output_variable, value=spmi_output_value):
    """Check the complete rendered objects, not a normalized scanner-policy subset."""
    unchanged = copy.deepcopy(after)
    jobs = [job for stage in unchanged["stages"] for job in stage.get("jobs", [])
            if any(step.get("displayName") == root_observer["displayName"] for step in job.get("steps", []))]
    assert len(jobs) == 1, "Expected one diagnostic Windows observer job"
    job = jobs[0]
    assert job["pool"]["os"] == "windows", "Output override must be Windows-only"
    variables = job["variables"]
    assert isinstance(variables, list), "Expected provider-expanded variable list"
    overrides = [item for item in variables
                 if item.get("name", "").replace(".", "_").upper() == variable.replace(".", "_").upper()]
    exact_variable = overrides == [{"name": variable, "value": value}]
    # Actual 1ES serialization of this source string is `value: true`; no other field is normalized.
    provider_retention_true = (
        variable == retention_variable and value == "true" and len(overrides) == 1
        and set(overrides[0]) == {"name", "value"}
        and overrides[0]["name"] == retention_variable and overrides[0]["value"] is True
    )
    assert exact_variable or provider_retention_true, f"Expected exact variable or supported retention true rendering: {variable}"
    assert [item for item in variables if item.get("name", "").replace(".", "_").upper() == spmi_output_variable] == [
        {"name": spmi_output_variable, "value": spmi_output_value}
    ], "Reviewed SPMI output must remain exact and unique"
    scanners = [step for step in job["steps"]
                if re.search(r"(?:^|\.)1ESSecretScanning@", step.get("task", ""))]
    assert len(scanners) == 1, "Shared fixed user-copy destination requires exactly one Windows scanner"
    assert not any(key.casefold() == "output" for key in scanners[0].get("inputs", {})), "Unexpected explicit task Output"
    assert not any(key.replace(".", "_").upper() == spmi_output_variable
                   for key in scanners[0].get("env", {})), "Unexpected task environment override"
    if variable == retention_variable:
        assert_root_observer_order(job["steps"])
        consumers = [step for step in job["steps"] if retention_variable in step.get("condition", "")]
        assert len(consumers) == 15, "Expected all 15 existing Windows retention conditions"
    variables.remove(overrides[0])
    assert unchanged == before, "Only one diagnostic Windows job variable may change in the entire provider graph"


spmi_before = {"stages": [{"jobs": [{
    "pool": {"os": "windows"}, "variables": [],
    "steps": [{"task": "1ESSecretScanning@1", "inputs": {"Target": "unchanged"}},
              copy.deepcopy(root_observer)],
}]}]}
spmi_after = copy.deepcopy(spmi_before)
spmi_after["stages"][0]["jobs"][0]["variables"].append({"name": spmi_output_variable, "value": spmi_output_value})
assert_windows_evidence_variable_change(spmi_before, spmi_after)
for mutation in ("directory", "duplicate-variable", "duplicate-scanner", "target", "condition", "task-output", "task-env", "other-host", "other-job"):
    invalid = copy.deepcopy(spmi_after)
    job = invalid["stages"][0]["jobs"][0]
    if mutation == "directory":
        job["variables"][0]["value"] = "$(Build.SourcesDirectory)\\"
    elif mutation == "duplicate-variable":
        job["variables"].append({"name": spmi_output_variable.lower().replace("_", "."), "value": spmi_output_value})
    elif mutation == "duplicate-scanner":
        job["steps"].append(copy.deepcopy(job["steps"][0]))
    elif mutation == "target":
        job["steps"][0]["inputs"]["Target"] = "different"
    elif mutation == "condition":
        job["steps"][0]["condition"] = "always()"
    elif mutation == "task-output":
        job["steps"][0]["inputs"]["Output"] = spmi_output_value
    elif mutation == "task-env":
        job["steps"][0]["env"] = {spmi_output_variable: "different"}
    elif mutation == "other-host":
        job["pool"]["os"] = "linux"
    else:
        invalid["stages"][0]["jobs"].append({"variables": [{"name": spmi_output_variable, "value": spmi_output_value}]})
    try:
        assert_windows_evidence_variable_change(spmi_before, invalid)
    except AssertionError:
        pass
    else:
        raise AssertionError(f"Invalid SPMI output-only graph accepted: {mutation}")
print("PASS: modeled SPMI output change is one Windows job variable; duplicate scanners/aliases and any other provider changes are rejected.")

retention_condition = "or(ne(variables['Agent.JobStatus'], 'Succeeded'), eq(variables['XA.PublishAllLogs'], 'true'))"
retention_before = copy.deepcopy(spmi_after)
retention_job = retention_before["stages"][0]["jobs"][0]
retention_job["steps"] = [{"task": "1ESSecretScanning@1", "condition": retention_condition}]
retention_job["steps"] += [{"task": "provider.Unchanged", "condition": retention_condition} for _ in range(14)]
retention_job["steps"] += copy.deepcopy(observer_fixture)
retention_after = copy.deepcopy(retention_before)
retention_after["stages"][0]["jobs"][0]["variables"].append({"name": retention_variable, "value": "true"})
assert_windows_evidence_variable_change(retention_before, retention_after, retention_variable, "true")
provider_retention_after = copy.deepcopy(retention_after)
provider_retention_after["stages"][0]["jobs"][0]["variables"][-1]["value"] = True
assert_windows_evidence_variable_change(retention_before, provider_retention_after, retention_variable, "true")
for mutation in ("bool-false", "false", "number", "alias", "duplicate", "extra-field", "spmi-output", "condition", "active-flag", "other-host", "other-job"):
    invalid = copy.deepcopy(retention_after)
    job = invalid["stages"][0]["jobs"][0]
    if mutation in ("bool-false", "false", "number"):
        job["variables"][-1]["value"] = {"bool-false": False, "false": "false", "number": 1}[mutation]
    elif mutation == "alias":
        job["variables"][-1]["name"] = "XA_PUBLISHALLLOGS"
    elif mutation == "duplicate":
        job["variables"].append({"name": "xa_publishalllogs", "value": "true"})
    elif mutation == "extra-field":
        job["variables"][-1]["extra"] = True
    elif mutation == "spmi-output":
        job["variables"][0]["value"] = "different"
    elif mutation == "condition":
        job["steps"][0]["condition"] = "always()"
    elif mutation == "active-flag":
        job["variables"].append({"name": "ONEES_HASACTIVESDLTASK", "value": "True"})
    elif mutation == "other-host":
        job["pool"]["os"] = "linux"
    else:
        invalid["stages"][0]["jobs"].append({"variables": [{"name": retention_variable, "value": True}]})
    try:
        assert_windows_evidence_variable_change(retention_before, invalid, retention_variable, "true")
    except AssertionError:
        pass
    else:
        raise AssertionError(f"Invalid retention-only graph accepted: {mutation}")

# Model only the existing inner retention predicate, never the full Azure scheduler.
for status, expected_off in (("Succeeded", False), ("SucceededWithIssues", True), ("Failed", True), ("Canceled", True)):
    for setting, expected in (("", expected_off), ("false", expected_off), ("true", True)):
        assert (status != "Succeeded" or setting == "true") == expected
print("PASS: source boolean rejected; modeled provider retention accepts only string true or exact boolean True; aliases, 15 guards, SPMI output, observer and clean controls preserved.")
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
linux_template = "build-tools/automation/yaml-templates/stage-linux-tests.yaml"
linux_stage = next(item for item in stages if item.get("template") == f"/{linux_template}@self")
assert linux_stage["parameters"] == {"guestReadiness": "${{ parameters.guestReadiness }}"}
linux_source = yaml.safe_load((ROOT / linux_template).read_text())
linux_baseline = yaml.safe_load(subprocess.check_output(["git", "show", f"{BASELINE}:{linux_template}"], cwd=ROOT))
assert remove_mode_parameters(expand(linux_source, False)) == linux_baseline
linux_on = expand(linux_source, True)
linux_configure = {
    "task": "PowerShell@2", "displayName": "Configure diagnostic test acquisition",
    "inputs": {"pwsh": True, "targetType": "filePath",
               "filePath": "$(System.DefaultWorkingDirectory)/build-tools/scripts/guest-readiness-test-sources.ps1",
               "arguments": "-AllowLinux"},
}
for before, after in zip(linux_baseline["stages"][0]["jobs"], linux_on["stages"][0]["jobs"]):
    expected = copy.deepcopy(before)
    expected["variables"] = {"RestoreConfigFile": "$(System.DefaultWorkingDirectory)/NuGet.config"}
    expected["steps"][0]["parameters"] = {"apkDiffNuGetConfigFile": "$(System.DefaultWorkingDirectory)/NuGet.config"}
    expected["steps"].insert(2, linux_configure)
    assert expected == after, "Only the explicit Linux diagnostic acquisition setup may change"
assert len(linux_on["stages"][0]["jobs"]) == 2

installer_path = "build-tools/automation/yaml-templates/install-dotnet-tool.yaml"
installer = yaml.safe_load((ROOT / installer_path).read_text())
installer_baseline = yaml.safe_load(subprocess.check_output(["git", "show", f"{BASELINE}:{installer_path}"], cwd=ROOT))
assert installer["parameters"].pop("nugetConfigFile") == ""
tool_inputs = installer["steps"][1]["inputs"]
default_args = tool_inputs.pop("${{ if eq(parameters.nugetConfigFile, '') }}")["arguments"]
configured_args = tool_inputs.pop("${{ else }}")["arguments"]
tool_inputs["arguments"] = default_args
assert installer == installer_baseline, "Empty installer config must preserve every ordinary field"
assert configured_args == default_args.replace('--add-source "https://api.nuget.org/v3/index.json"',
                                               '--configfile "${{ parameters.nugetConfigFile }}"')
assert configured_args.replace("${{ parameters.nugetConfigFile }}", "/source with spaces/NuGet.config").endswith(
    '--configfile "/source with spaces/NuGet.config"')
setup_path = "build-tools/automation/yaml-templates/setup-test-environment.yaml"
setup = yaml.safe_load((ROOT / setup_path).read_text())
setup_baseline = yaml.safe_load(subprocess.check_output(["git", "show", f"{BASELINE}:{setup_path}"], cwd=ROOT))
assert setup["parameters"].pop("apkDiffNuGetConfigFile") == ""
apk_call = next(s for s in setup["steps"] if "${{ if eq(parameters.installApkDiff, true) }}" in s)[
    "${{ if eq(parameters.installApkDiff, true) }}"][0]
assert apk_call["parameters"].pop("nugetConfigFile") == "${{ parameters.apkDiffNuGetConfigFile }}"
assert setup == setup_baseline, "Only apkdiff config forwarding may change shared setup; slicer unchanged"
maui = next(s for s in stages if s.get("stage") == "maui_tests")
maui_setup = next(s for s in maui["jobs"][0]["steps"] if s.get("template") == f"/{setup_path}@self")
assert maui_setup["parameters"]["apkDiffNuGetConfigFile"] == "$(Build.SourcesDirectory)/android/NuGet.config"
print("PASS: explicit two-job Linux setup, MAUI Android-root apkdiff config, ordinary installer/default/Mac graph invariance.")
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


def assert_linux_setup_only_change(before, after):
    """Undo only the approved setup delta, then compare every provider field."""
    unchanged = copy.deepcopy(after)
    old_jobs = {j["job"]: j for s in before["stages"] for j in s.get("jobs", []) if "job" in j}
    new_jobs = {j["job"]: j for s in unchanged["stages"] for j in s.get("jobs", []) if "job" in j}
    windows_configure = next(s for s in old_jobs["win_build_test"]["steps"]
                             if s.get("displayName") == test_sources_configure["displayName"])
    expected_configure = copy.deepcopy(windows_configure)
    expected_configure["inputs"] = linux_configure["inputs"]
    for name in ("linux_tests_smoke_1", "linux_tests_smoke_2", "maui_tests_integration"):
        old, new = old_jobs[name], new_jobs[name]
        is_linux = name.startswith("linux_tests_")
        config = ("$(System.DefaultWorkingDirectory)/NuGet.config" if is_linux
                  else "$(Build.SourcesDirectory)/android/NuGet.config")
        assert new["pool"]["os"] == ("linux" if is_linux else "windows")
        if is_linux:
            variables = [v for v in new["variables"] if v.get("name", "").upper() == "RESTORECONFIGFILE"]
            assert variables == [{"name": "RestoreConfigFile", "value": config}], "Expected one exact Linux root config variable"
            new["variables"].remove(variables[0])
            positions = [i for i, s in enumerate(new["steps"])
                         if s.get("displayName") == test_sources_configure["displayName"]]
            assert len(positions) == 1
            position = positions[0]
            assert new["steps"][position] == expected_configure
            assert new["steps"][position + 1]["displayName"].startswith("run Xamarin.Android.Build.Tests - Linux ")
            assert new["steps"][position - 1]["task"] == "DownloadPipelineArtifact@2"
            new["steps"].pop(position)
        old_installers = [s for s in old["steps"] if s.get("displayName", "").startswith("install apkdiff ")]
        new_installers = [s for s in new["steps"] if s.get("displayName", "").startswith("install apkdiff ")]
        assert len(old_installers) == len(new_installers) == 1
        old_args = old_installers[0]["inputs"]["arguments"]
        old_suffix = '--add-source "https://api.nuget.org/v3/index.json"'
        assert old_args.endswith(old_suffix)
        assert new_installers[0]["inputs"]["arguments"] == old_args.removesuffix(old_suffix) + f'--configfile "{config}"'
        new_installers[0]["inputs"]["arguments"] = old_args
    assert unchanged == before, "Only two Linux setups and three apkdiff config arguments may change"


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--expanded-preview", type=Path)
parser.add_argument("--baseline-preview", type=Path)
parser.add_argument("--require-root-observer", action="store_true")
parser.add_argument("--require-spmi-output", action="store_true")
parser.add_argument("--require-windows-retention", action="store_true")
parser.add_argument("--require-linux-test-setup", action="store_true")
args = parser.parse_args()
if args.baseline_preview and not args.expanded_preview:
    parser.error("--baseline-preview requires --expanded-preview")
if args.require_root_observer and not args.expanded_preview:
    parser.error("--require-root-observer requires --expanded-preview")
if args.require_spmi_output and not (args.expanded_preview and args.baseline_preview):
    parser.error("--require-spmi-output requires --expanded-preview and --baseline-preview")
if args.require_windows_retention and (not (args.expanded_preview and args.baseline_preview) or args.require_spmi_output):
    parser.error("--require-windows-retention requires both previews and cannot combine with the older --require-spmi-output delta")
if args.require_linux_test_setup and (not (args.expanded_preview and args.baseline_preview)
                                    or args.require_spmi_output or args.require_windows_retention):
    parser.error("--require-linux-test-setup requires both previews and cannot combine with older variable-only deltas")
if args.expanded_preview:
    preview = yaml.safe_load(args.expanded_preview.read_text(encoding="utf-8-sig"))
    sign_jobs = [job for stage in preview["stages"] for job in stage.get("jobs", [])
                 if any(step.get("displayName") == "Guest readiness signing Input evidence"
                        for step in job.get("steps", []))]
    assert len(sign_jobs) == 1, "Exactly one diagnostic signing job required"
    steps = sign_jobs[0]["steps"]
    assert_signing_order(steps)
    if args.require_root_observer:
        observer_jobs = [job for stage in preview["stages"] for job in stage.get("jobs", [])
                         if any(step.get("displayName") == root_observer["displayName"] for step in job.get("steps", []))]
        assert len(observer_jobs) == 1, "Exactly one diagnostic root observer job required"
        assert_root_observer_order(observer_jobs[0]["steps"])
        print("PASS: supplied expanded graph places metadata observation after SDL analysis/Build Results and before unchanged clean.")
    if args.baseline_preview:
        before = yaml.safe_load(args.baseline_preview.read_text(encoding="utf-8-sig"))
        if args.require_spmi_output:
            assert_windows_evidence_variable_change(before, preview)
            print("PASS: entire supplied provider graph differs only by the fixed diagnostic Windows SPMI user-copy variable.")
        if args.require_windows_retention:
            assert_windows_evidence_variable_change(before, preview, retention_variable, "true")
            print("PASS: entire supplied provider graph differs only by diagnostic Windows retention (source string or observed provider true scalar); all existing guards and tasks are identical.")
        if args.require_linux_test_setup:
            assert_linux_setup_only_change(before, preview)
            for mutation in ("alias", "mac", "audit", "condition", "version", "mixed-config"):
                invalid = copy.deepcopy(preview)
                job = next(j for s in invalid["stages"] for j in s.get("jobs", []) if j.get("job") == "linux_tests_smoke_1")
                if mutation == "alias":
                    next(v for v in job["variables"] if v.get("name") == "RestoreConfigFile")["name"] = "RESTORECONFIGFILE"
                elif mutation == "mac":
                    job["pool"]["os"] = "macOS"
                elif mutation == "audit":
                    job["variables"].append({"name": "NuGetAudit", "value": "false"})
                elif mutation == "condition":
                    job["steps"][0]["condition"] = "always()"
                else:
                    task = next(s for s in job["steps"] if s.get("displayName", "").startswith("install apkdiff "))
                    task["inputs"]["arguments"] += ' --version 0.0.18' if mutation == "version" else ' --add-source "https://api.nuget.org/v3/index.json"'
                try:
                    assert_linux_setup_only_change(before, invalid)
                except AssertionError:
                    pass
                else:
                    raise AssertionError(f"Invalid setup delta accepted: {mutation}")
            print("PASS: entire provider graph differs only by two Linux setups and three apkdiff config arguments; invalid scope/policy/version/aliases rejected.")
        old_jobs = [job for stage in before["stages"] for job in stage.get("jobs", [])
                    if job.get("job") == sign_jobs[0]["job"]]
        assert len(old_jobs) == 1
        assert_protected_signing_tasks(old_jobs[0]["steps"], steps)
    print("PASS: supplied service-expanded preview defers scans until after normal signing; all guest uploads remain always-on.")
