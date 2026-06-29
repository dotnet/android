# Binlog Analysis Reference

Load this file only when the user asks to analyze .binlog artifacts from a build.

## Prerequisites

| Tool | Check | Install |
|------|-------|---------|
| `binlogtool` | `dotnet tool list -g \| grep binlogtool` | `dotnet tool install -g binlogtool` |

## Download .binlog artifacts

### List artifacts

```bash
az pipelines runs artifact list --run-id $BUILD_ID --org $ORG_URL --project $PROJECT --output json 2>&1
```

```powershell
az pipelines runs artifact list --run-id $BUILD_ID --org $ORG_URL --project $PROJECT --output json
```

Look for artifact names that contain build logs. On the `dotnet-android` (dnceng-public) pipeline the relevant ones are:
- `Build Results - macOS` / `Build Results - Windows` / `Build Results - Linux` — contain the `.binlog` files (published mainly when a build stage fails or when `XA.PublishAllLogs` is set).
- `Test Results - ...` — per-test-stage logs and artifacts. For the on-device `Package Tests` (APKs) stage these also include each device test's `build-<testName>.binlog`, `run-<testName>.binlog`, the `.trx`, and `logcat-<testName>.txt` (essential for native/JNI crash diagnosis).

If a green build has no `Build Results - *` artifact, the binlogs weren't published; re-run with `XA.PublishAllLogs` or rely on the timeline/test queries instead.

### Download

```bash
TEMP_DIR="/tmp/azdo-binlog-$BUILD_ID"
mkdir -p "$TEMP_DIR"
az pipelines runs artifact download --artifact-name "$ARTIFACT_NAME" --path "$TEMP_DIR" \
  --run-id $BUILD_ID --org $ORG_URL --project $PROJECT
```

```powershell
$tempDir = Join-Path $env:TEMP "azdo-binlog-$BUILD_ID"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
az pipelines runs artifact download --artifact-name "$ARTIFACT_NAME" --path $tempDir `
  --run-id $BUILD_ID --org $ORG_URL --project $PROJECT
```

## Analysis commands

```bash
# Broad error search
binlogtool search "$TEMP_DIR"/*.binlog "error"

# .NET Android errors
binlogtool search "$TEMP_DIR"/*.binlog "XA"

# C# compiler errors
binlogtool search "$TEMP_DIR"/*.binlog "error CS"

# NuGet errors
binlogtool search "$TEMP_DIR"/*.binlog "error NU"

# Full text log reconstruction
binlogtool reconstruct "$TEMP_DIR/file.binlog" "$TEMP_DIR/reconstructed"

# MSBuild properties
binlogtool listproperties "$TEMP_DIR/file.binlog"

# Double-write detection
binlogtool doublewrites "$TEMP_DIR/file.binlog" "$TEMP_DIR/dw"
```

## Cleanup

```bash
rm -rf "/tmp/azdo-binlog-$BUILD_ID"
```

```powershell
Remove-Item -Recurse -Force (Join-Path $env:TEMP "azdo-binlog-$BUILD_ID")
```
