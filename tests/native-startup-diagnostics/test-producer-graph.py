"""Bounded, parsed source-owned pipeline graph checks; not an Azure service preview."""
from pathlib import Path
import xml.etree.ElementTree as ET
import yaml

root = Path(__file__).resolve().parents[2]
pipeline = yaml.safe_load(
    (root / "build-tools" / "automation" / "azure-pipelines-guest-readiness.yaml").read_text()
)
assert set(pipeline) == {"trigger", "pr", "parameters", "jobs"}
assert pipeline["trigger"] == "none" and pipeline["pr"] == "none"
params = {p["name"]: p for p in pipeline["parameters"]}
assert set(params) == {"enable", "sourceCommit", "patchSha256"}
assert params["enable"]["type"] == "boolean" and params["enable"]["default"] is False
assert len(pipeline["jobs"]) == 1
job = pipeline["jobs"][0]
assert job["condition"] == "and(succeeded(), eq('${{ parameters.enable }}', 'true'))"
steps = job["steps"]
assert len(steps) == 5
assert steps[0] == {
    "checkout": "self", "fetchDepth": 0, "submodules": "recursive", "persistCredentials": False
}
assert [s["task"] for s in steps if "task" in s] == [
    "UseDotNet@2", "NuGetAuthenticate@1", "PublishPipelineArtifact@1"
]
assert steps[3]["pwsh"].count("guest-readiness-runtime-pack.ps1") == 1
assert set(steps[3]["env"]) == {"GUEST_SOURCE_COMMIT", "GUEST_PATCH_SHA256"}
assert steps[4]["inputs"]["targetPath"].endswith(r"\bin\guest-readiness-producer")
assert not any(key in pipeline for key in ("extends", "resources", "schedules"))

# Inspect the real normal package project and its selected version target, not copied XML snippets.
pack = ET.parse(root / "build-tools" / "create-packs" / "Microsoft.Android.Runtime.proj").getroot()
assert pack.find(".//PackageId").text == "Microsoft.Android.Runtime.$(AndroidRuntime).$(AndroidApiLevel).$(AndroidRID)"
assert pack.find(".//BeforePack").text.strip().startswith("_GetDefaultPackageVersion;")
targets = ET.parse(root / "build-tools" / "create-packs" / "Directory.Build.targets").getroot()
version = targets.find("./Target[@Name='_GetDefaultPackageVersion']")
assert version.attrib["DependsOnTargets"] == "GetXAVersionInfo"
assert version.find("./PropertyGroup/PackageVersion").text == "$(AndroidPackVersionLong)"
assert version.find(".//Exec") is None
print("Parsed standalone manual pipeline and normal pack/version source graph checks passed.")
