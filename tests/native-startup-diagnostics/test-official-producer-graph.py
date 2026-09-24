"""Source-owned conditional expansion, not an Azure/1ES service preview."""
import copy
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

import yaml

ROOT = Path(__file__).resolve().parents[2]


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
                     set(v) == {"guestReadiness", "guestReadinessAttempt"})}
    return node


paths = ["build-tools/automation/azure-pipelines.yaml"] + [
    f"build-tools/automation/yaml-templates/{name}.yaml"
    for name in ("build-macos", "commercial-build", "build-linux")]
for path in paths:
    old = yaml.safe_load(subprocess.check_output(
        ["git", "show", f"a98605cfb228ee44dd63a4cd222b3609fbae38b6:{path}"], cwd=ROOT))
    new = yaml.safe_load((ROOT / path).read_text())
    assert remove_mode_parameters(expand(new, False)) == old, f"Default graph changed: {path}"

root = yaml.safe_load((ROOT / paths[0]).read_text())
parameters = {x["name"]: x for x in root["parameters"]}
assert parameters["guestReadiness"]["default"] is False
diagnostic = expand(root, True)
text = json.dumps(diagnostic)
for forbidden in ("nuget-msi-convert", "push_signed_nugets", "PushToMaestro", "darc", "SymbolUploader"):
    assert forbidden not in text, forbidden
stages = diagnostic["extends"]["parameters"]["stages"]
prepare = next(x for x in stages if x.get("stage") == "dotnet_prepare_release")
jobs = prepare["jobs"]
signers = [x for x in jobs if x.get("template") == "sign-artifacts/jobs/v4.yml@yaml-templates"]
assert len(signers) == 2
assert prepare["dependsOn"] == ["mac_build", "linux_build"]
oldroot = yaml.safe_load(subprocess.check_output(["git", "show", f"a98605cf:{paths[0]}"], cwd=ROOT))
oldprepare = next(x for x in oldroot["extends"]["parameters"]["stages"] if x.get("stage") == "dotnet_prepare_release")
assert prepare["condition"] == oldprepare["condition"]
for before, after in zip([x for x in oldprepare["jobs"] if x.get("template") == "sign-artifacts/jobs/v4.yml@yaml-templates"], signers):
    oldparams, newparams = before["parameters"], copy.deepcopy(after["parameters"])
    for added in ("checkoutType", "checkoutPath", "preSignSteps", "postSignSteps"):
        newparams.pop(added, None)
    assert oldparams == newparams, "Existing signing policy changed"
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
