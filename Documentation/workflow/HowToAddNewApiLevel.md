# HowTo: Add a new Android API Level

## Developer Preview

The first developer preview generally ships in late February or early March.  At this early
stage for the APIs, we simply add literal bindings for them.  We do not spend resources on
the more manual parts like enumification that will likely change as the APIs mature.

### Add New Platform to `xaprepare`

- Add new level to `/build-tools/xaprepare/xaprepare/ConfigAndData/BuildAndroidPlatforms.cs`:
  - `new AndroidPlatform (apiName: "S", apiLevel: 31, platformID: "S", include: "v11.0.99", framework: "v11.0.99", stable: false),`
- Add new level to `/build-tools/xaprepare/xaprepare/ConfigAndData/Dependencies/AndroidToolchain.cs`:
  - `new AndroidPlatformComponent ("platform-S_r01", apiLevel: "S", pkgRevision: "1"),`
  
At this point, you can run `Xamarin.Android.sln /t:Prepare` using your usual mechanism, and
the new platform will be downloaded to your local Android SDK.

### Generate `params.txt` File

- In `/external/Java.Interop/tools/param-name-importer`:
  - Add new level to `generate.sh` and run
  - *or* run manually: `param-name-importer.exe -source-stub-zip C:/Users/USERNAME/android-toolchain/sdk/platforms/android-S/android-stubs-src.jar -output-text api-S.params.txt -output-xml api-S.params.xml -verbose -framework-only`
- Copy the produced `api-X.params.txt` file to `/src/Mono.Android/Profiles/`

### Other Infrastructure Changes

- Add level to `/build-tools/api-merge/merge-configuration.xml` to create `api-S.xml.class-parse`
- Add level to `/build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks/CheckApiCompatibility.cs`
  to enable running ApiCompat against the new level. (ex: `{ "v11.0.99", "v11.0" }`)
- Add level to `/build-tools/api-xml-adjuster/Makefile`
- LOCAL ONLY: Update `Configuration.props` or `Configuration.Override.props` to specify building the new level:
  - `<AndroidApiLevel>31</AndroidApiLevel>`
  - `<AndroidPlatformId>S</AndroidPlatformId>`
  - `<AndroidFrameworkVersion>v11.0.99</AndroidFrameworkVersion>`

### Building the New Mono.Android

- Build `Xamarin.Android.sln` with your usual mechanism, and the new `Mono.Android.dll` should be built
- Read the note at the bottom of `/src/Mono.Android/metadata` that has a few lines that must be 
  copy/pasted for new API levels
- Add required metadata fixes in `/src/Mono.Android/metadata` until `Mono.Android.csproj` builds
  
### ApiCompat

There may be ApiCompat issues that need to be examined.  Either fix the assembly with metadata or allow
acceptable "breaks":

- Add new file to `/tests/api-compatibility`, like `acceptable-breakages-v11.0.99.txt`
- Copy errors reported from ApiCompat task to acceptable breakages file

## Bindings Stabilization

When Google announces that the APIs are frozen, additional work such as enumification is needed.

---- Somewhat outdated docs below, update when we do this year's stabilization ----

5) enumification

See `build-tools/enumification-helpers/README`. Usually it takes many days to complete...

Enumification work can be delayed and only the final API has to be enumified.

6) new AndroidManifest.xml elements and attributes

`build-tools/manifest-attribute-codegen/manifest-attribute-codegen.cs` can be compiled to a tool that collects all Manifest elements and attributes with the API level since when each of them became available. New members are supposed to be added to the existing `(FooBar)Attribute.cs` and `(FooBar)Attribute.Partial.cs` in `src/Mono.Android` and `src/Xamarin.Android.Build.Tasks` respectively.

Note that there are documented and undocumented XML nodes, and we don't have to deal with undocumented ones.

Android P introduced no documented XML artifact.

7) Update Android Tooling Versions

These sre located in [Xamarin.Android.Common.props.in](../../src/Xamarin.Android.Build.Tasks/Xamarin.Android.Common.props.in). The following MSBuild properties need to be updated to ensure 
the latest tool versions are being used.

`AndroidSdkBuildToolsVersion`
`AndroidSdkPlatformToolsVersion`
`AndroidSdkToolsVersion`

The major version should match the new API level. For Android P this will be 28.x.x . If a version which exactly matches the API Level is not available then the latest version should be used.


