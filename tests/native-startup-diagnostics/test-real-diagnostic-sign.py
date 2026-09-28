"""Source-level boundaries; Azure provider expansion and real signatures need separate checks."""
from pathlib import Path
import subprocess

import yaml

ROOT = Path(__file__).resolve().parents[2]
PIPELINE = ROOT / "build-tools/automation/azure-pipelines.yaml"
STAGE = ROOT / "build-tools/automation/yaml-templates/stage-guest-readiness-real-sign.yaml"
SIGNER = ROOT / "build-tools/scripts/guest-readiness-real-sign.ps1"


def test_artifact_only_route():
    doc = yaml.safe_load(PIPELINE.read_text())
    parameters = {item["name"]: item for item in doc["parameters"]}
    assert parameters["realDiagnosticSign"] == {
        "name": "realDiagnosticSign", "type": "boolean", "default": False
    }
    root_stages = doc["extends"]["parameters"]["stages"]
    assert len(root_stages) == 2
    assert list(root_stages[0]) == ["${{ if eq(parameters.realDiagnosticSign, false) }}"]
    assert list(root_stages[1]) == ["${{ if eq(parameters.realDiagnosticSign, true) }}"]
    assert root_stages[1]["${{ if eq(parameters.realDiagnosticSign, true) }}"] == [{
        "template": "/build-tools/automation/yaml-templates/stage-guest-readiness-real-sign.yaml@self"
    }]
    assert "${{ if or(eq(parameters.guestReadiness, true), eq(parameters.realDiagnosticSign, true)) }}" in doc["extends"]
    assert "Skip1ESComplianceTasks" in str(doc["extends"]["parameters"]["sdl"])
    stage = yaml.safe_load(STAGE.read_text())["stages"][0]
    assert len(stage["jobs"]) == 1
    job = stage["jobs"][0]
    for predicate in ("'Manual'", "'System.DefinitionId'", "'11410'", "'Build.Repository.Name'",
                      "'refs/heads/release/'", "'refs/heads/main'"):
        assert predicate in job["condition"]
    assert job["templateContext"]["mb"]["signing"] == {
        "enabled": True, "signType": "Real", "signWithProd": True
    }
    steps = job["steps"]
    downloads = [step for step in steps if step.get("task") == "DownloadPipelineArtifact@2"]
    assert len(downloads) == 1
    assert {key: downloads[0]["inputs"][key] for key in (
        "buildType", "project", "definition", "buildVersionToDownload",
        "pipelineId", "artifactName"
    )} == {
        "buildType": "specific", "project": "DevDiv", "definition": "11410",
        "buildVersionToDownload": "specific", "pipelineId": "15457266",
        "artifactName": "guest-readiness-Darwin-Pack",
    }
    assert downloads[0]["inputs"]["allowFailedBuilds"] is True
    templates = [step for step in steps if "template" in step]
    assert len(templates) == 1
    assert templates[0]["template"] == "sign-artifacts/steps/v4.yml@yaml-templates"
    assert templates[0]["parameters"]["signType"] == "Real"
    assert templates[0]["parameters"]["handleUnmappedFiles"] == "fail"
    assert len([step for step in steps if step.get("task") == "1ES.PublishPipelineArtifact@1"]) == 2
    assert all(step["condition"] == "always()" for step in steps
               if step.get("task") == "1ES.PublishPipelineArtifact@1" or
               step.get("displayName") == "Retain unadmitted signed package receipts")
    assert all(step.get("task", "").split("@")[0] in (
        "", "DownloadPipelineArtifact", "PowerShell", "1ES.PublishPipelineArtifact"
    ) for step in steps)
    assert [step["inputs"]["arguments"].split()[1:3]
            for step in steps if step.get("task") == "PowerShell@2"] == [
                ["Input", "-SourceDirectory"], ["Output", "-SourceDirectory"]
            ]
    input_arguments = next(step["inputs"]["arguments"] for step in steps
                           if step.get("displayName") == "Validate frozen source and stage exactly two unsigned packs")
    assert '-WorkingDirectory "$(Agent.TempDirectory)\\android-diagnostic-real-working"' in input_arguments
    assert templates[0]["parameters"]["workingDirectory"] == r"$(Agent.TempDirectory)\android-diagnostic-real-working"
    assert "New-Item -ItemType Directory -Path $WorkingDirectory -ErrorAction Stop" in SIGNER.read_text()
    script = SIGNER.read_text()
    for hash_ in ("b6f89ce67d01e8f99c14718e3d16138e1dae91e1edc5f066984b26bfc798d816",
                  "28a2edcb90afb766bc1599dc36165c3822662315eebb92095a85569d34fc50d2",
                  "19fa9b5addc2c2d99a6e09492a60b3b2f01e799b",
                  "76ca628b0f2f914dd9a2fa37b54bffe2778f7f81"):
        assert hash_ in script
    assert "resources.repositories['yaml-templates'].version" in str(doc["variables"])
    assert "resources.repositories['1esPipelines'].version" in str(doc["variables"])
    path = "build-tools/automation/azure-pipelines-guest-readiness.yaml"
    assert (ROOT / path).read_text().replace("\r\n", "\n") == subprocess.check_output(
        ["git", "show", f"HEAD:{path}"], cwd=ROOT).decode().replace("\r\n", "\n")


if __name__ == "__main__":
    test_artifact_only_route()
    print("Frozen artifact-only signing graph passed.")
