# APK Size Regression Checks

## Overview

This skill describes how to handle APK size regression check failures in CI. When your changes affect APK sizes (either increase or decrease), the `BuildReleaseArm64` test will fail and you need to update the baseline reference files.

## When This Applies

- CI fails with an `apkdiff regression test failed` error
- The test `BuildReleaseArm64` in `BuildTest2.cs` reports a regression
- Your changes intentionally affect binary sizes (e.g., adding/removing code, changing compiler options, updating dependencies)

## Reference Files

### MSBuild Test References
Located in `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Resources/Base/`:
- `BuildReleaseArm64SimpleDotNet.CoreCLR.apkdesc`
- `BuildReleaseArm64SimpleDotNet.MonoVM.apkdesc`
- `BuildReleaseArm64SimpleDotNet.NativeAOT.apkdesc`
- `BuildReleaseArm64XFormsDotNet.CoreCLR.apkdesc`
- `BuildReleaseArm64XFormsDotNet.MonoVM.apkdesc`
- `BuildReleaseArm64XFormsDotNet.NativeAOT.apkdesc`
- `BuildReleaseArm64SimpleLegacy.apkdesc`
- `BuildReleaseArm64XFormsLegacy.apkdesc`

### APK Instrumentation Test References
Located in `tests/apk-sizes-reference/`:
- `com.companyname.vsandroidapp-Signed-Release.apkdesc`
- `Xamarin.Forms_Performance_Integration-Signed-Release.apkdesc`
- `Xamarin.Forms_Performance_Integration-Signed-Release-Aot.apkdesc`
- `Xamarin.Forms_Performance_Integration-Signed-Release-Bundle.apkdesc`
- `Xamarin.Forms_Performance_Integration-Signed-Release-Profiled-Aot.apkdesc`

## How to Update Baselines

### From CI Artifacts (Preferred)
1. Navigate to the failed CI build
2. Download the test results archive from the build artifacts
3. Extract the new `.apkdesc` files from the archive
4. Replace the corresponding files in the reference directories listed above
5. Commit the updated reference files

### Local Update (Alternative)
Run the update script on a machine with full Android SDK setup:
```bash
# On macOS/Linux
./build-tools/scripts/UpdateApkSizeReference.sh

# On Windows
.\build-tools\scripts\UpdateApkSizeReference.ps1
```

**Note:** Local updates require:
- A Release configuration build of Xamarin.Android
- Full Android SDK and build environment
- ARM64 target support

## Important Notes

1. **Size decreases are also flagged** - The test flags both increases and decreases to keep baselines current
2. **Use Release builds** - Debug builds produce larger APKs with different optimizations
3. **Verify changes are intentional** - Before updating baselines, check the `apkdiff` output to confirm the size changes are expected
4. **Thresholds** - The test uses percentage-based thresholds (3% for APK, 5% for individual files) defined in `BuildTest2.cs`

## File Format

The `.apkdesc` files are JSON containing entry names and sizes:
```json
{
  "Comment": null,
  "Entries": {
    "AndroidManifest.xml": { "Size": 3036 },
    "classes.dex": { "Size": 397520 },
    "lib/arm64-v8a/libmonodroid.so": { "Size": 1375784 }
  },
  "PackageSize": 12345678
}
```

## Related Files

- Test implementation: `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/BuildTest2.cs`
- APK diff tool: https://www.nuget.org/packages/apkdiff/
- Threshold configuration: `build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks/ApkDiffCheckRegression.cs`
