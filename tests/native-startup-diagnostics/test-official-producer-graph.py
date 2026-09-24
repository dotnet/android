"""Source-owned conditional expansion, not an Azure/1ES service preview."""
import copy
import json
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
assert windows_job["steps"] == old_windows["stages"][0]["jobs"][0]["steps"], "Normal Windows command sequence changed"
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
assert any(x.get("condition") == "always()" for x in sign["steps"])
assert sign["steps"][0]["${{ if eq(parameters.phase, 'Output') }}"][0] == {
    "checkout": "1esPipelines", "path": "s/guest-readiness-1es", "persistCredentials": False,
    "condition": "always()",
}, "Retain the actual resolved-resource provenance checkout"
capture = next(step for step in sign["steps"] if step.get("task") == "PowerShell@2")
assert capture["${{ if eq(parameters.phase, 'Output') }}"] == {"condition": "always()"}
assert "condition" not in capture and "continueOnError" not in capture
assert capture["env"]["GUEST_SIGNING_OUTPUT_DIRECTORY"] == "$(Agent.TempDirectory)/artifact-signing/packed"
assert capture["env"]["GUEST_SIGNING_JOB_STATUS"] == "$(Agent.JobStatus)"
retained_output = sign["steps"][-1]["${{ if eq(parameters.phase, 'Output') }}"][0]
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
print("PASS: diagnostic Official envelope is independent of unchanged Test/Real signing; both hosted Mac images explicitly use macOS-15 after baseline variables, with default-off selection unchanged.")
print("PASS: all six Mac pool consumers resolve macOS-15 in diagnostic mode and original labels when off; stage/job scopes cannot silently shadow root images.")
print("PASS: diagnostic Windows job selects its actual self checkout config without changing commands; default Windows graph and canonical Git NuGet.config casing preserved.")
