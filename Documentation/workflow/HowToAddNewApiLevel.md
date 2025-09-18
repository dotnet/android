# HowTo: Add a new Android API Level

## Unstable Previews

The first unstable preview generally ships in late February or early March.  At this early
stage for the APIs, we simply add literal bindings for them.  We do not spend resources on
the more manual parts like enumification that will likely change as the APIs mature.

### Review `repository2-3.xml`

<https://dl.google.com/android/repository/repository2-3.xml> is an XML description of the Android SDK,
containing API level information, in particular the API level name and URL for the artifact.

`repository2-3.xml` is a "live" document; it changes over time.

Consider this snippet:

```xml
<sdk:sdk-repository
    xmlns:sdk="http://schemas.android.com/sdk/android/repo/repository2/03"
    xmlns:common="http://schemas.android.com/repository/android/common/02"
    xmlns:sdk-common="http://schemas.android.com/sdk/android/repo/common/03"
    xmlns:generic="http://schemas.android.com/repository/android/generic/02"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <channel id="channel-0">stable</channel>
  <channel id="channel-1">beta</channel>
  <channel id="channel-2">dev</channel>
  <channel id="channel-3">canary</channel>
  <remotePackage path="platforms;android-36">
    <type-details xsi:type="sdk:platformDetailsType">
      <api-level>36</api-level>
      <extension-level>17</extension-level>
      <base-extension>true</base-extension>
      <layoutlib api="15"/>
    </type-details>
    <revision>
      <major>2</major>
    </revision>
    <display-name>Android SDK Platform 36</display-name>
    <uses-license ref="android-sdk-license"/>
    <channelRef ref="channel-0"/>
    <archives>
      <archive>
        <complete>
          <size>65878410</size>
          <checksum type="sha1">2c1a80dd4d9f7d0e6dd336ec603d9b5c55a6f576</checksum>
          <url>platform-36_r02.zip</url>
        </complete>
      </archive>
    </archives>
  </remotePackage>
</sdk:sdk-repository>
```

  * **Path**: `//remotePackage/@path` is the "path" of the package, suitable for use with `sdkmanager`. For example:

    ```sh
    sdkmanager "platforms;android-36"
    ```

    will install this particular package.  Note that many paths contain `;`, and thus must be quoted when used with `sdkmanager`.

  * **Channel**: `//remotePackage/channelRef/@ref` is which "channel" this package is in.  See `//channel` for the list of channels.
    In this case, it's in `channel-0`, which is the `stable` channel.

  * **Display Name**: `//remotePackage/display-name` is the name of the package, in this case `Android SDK Platform 36`.

  * **Filename**: `//remotePackage/archives/archive/url` is the *file name* of the package.  Prepend this value with
    `https://dl.google.com/android/repository` to construct the final url, e.g.
    <https://dl.google.com/android/repository/platform-36_r02.zip>.

  * **Codename**: `//remotePackage/type-details/codename` is the *codename* of the package.  This is optional.

Next, keep this snippet for a preview API level in mind when reviewing the following sections:

```xml
  <remotePackage path="platforms;android-CANARY">
    <type-details xsi:type="sdk:platformDetailsType">
      <api-level>36.0</api-level>
      <codename>CANARY</codename>
      <extension-level>19</extension-level>
      <base-extension>true</base-extension>
      <layoutlib api="15"/>
    </type-details>
    <revision>
      <major>3</major>
    </revision>
    <display-name>Android SDK Platform CANARY</display-name>
    <uses-license ref="android-sdk-preview-license"/>
    <channelRef ref="channel-2"/>
    <archives>
      <archive>
        <complete>
          <size>66275299</size>
          <checksum type="sha1">8bf2196d77081927fdb863059b32d802d942b330</checksum>
          <url>platform-36.0-CANARY_r03.zip</url>
        </complete>
      </archive>
    </archives>
  </remotePackage>
```

### Add New Platform to `xaprepare`

For the new API level, you need:

  * The API level value.  For previews, this is the `//remotePackage/type-details/codename` value; `CANARY`, in this case.
  * The *base file name* of the `//url` value.  `xaprepare` automatically appends a `.zip` suffix.

Then update the following files:

  - Add new `AndroidPlatform` value to
    [`/build-tools/xaprepare/xaprepare/ConfigAndData/BuildAndroidPlatforms.cs`](../../build-tools/xaprepare/xaprepare/ConfigAndData/BuildAndroidPlatforms.cs):

    ```csharp
    new AndroidPlatform (apiName: "CANARY", apiLevel: new Version (36, 1), platformID: "CANARY", include: "v16.0",   framework: "v16.1", stable: false),

    ```

    TODO: what should be done for the "mid-year" updates, as is the case for API-CANARY?

    What are `include` and `framework` used for?

  - Add new level to
    [`/build-tools/xaprepare/xaprepare/ConfigAndData/Dependencies/AndroidToolchain.cs`](../../build-tools/xaprepare/xaprepare/ConfigAndData/Dependencies/AndroidToolchain.cs):

    ```csharp
    new AndroidPlatformComponent ("platform-36.0-CANARY_r03",   apiLevel: "CANARY", pkgRevision: "3", isLatestStable: false, isPreview: true),
    ```

    *Note*: the first argument is *base filename* of the package to download; `xaprepare` will automatically append `.zip`.

At this point, you can run `Xamarin.Android.sln -t:Prepare` using your usual mechanism.
However, it might not download the new platform into your local Android SDK.

### Build Xamarin.Android

Build `Xamarin.Android.sln` using your usual mechanism. This will not use the new platform yet,
but will build the tools like `param-name-importer` and `class-parse` that will be needed
in the next steps.


### Download the new API Levels

If preparing the repo did not download the new API level, you may explicitly do so via
`xaprepare --android-sdk-platforms=all`:

```dotnetcli
./dotnet-local.sh run --project build-tools/xaprepare/xaprepare/xaprepare.csproj -- --android-sdk-platforms=all
```

### Generate `params.txt` File

Build the `params.txt` file for the desired API level.  The `-p:ParamApiLevel=VALUE` parameter is the API level to process.
Unstable API levels use the API level codename, while stable API levels use the integer value:

```dotnetcli
# unstable
./dotnet-local.sh build build-tools/create-android-api/create-android-api.csproj -t:GenerateParamsFile -p:ParamApiLevel=CANARY

# stable
./dotnet-local.sh build build-tools/create-android-api/create-android-api.csproj -t:GenerateParamsFile -p:ParamApiLevel=36
```

This will create a `api-XX.params.txt` file in `/src/Mono.Android/Profiles/` that needs to be committed.

### Generate `api.xml` File

Add new level to
[`/build-tools/api-merge/merge-configuration.xml`](../../build-tools/api-merge/merge-configuration.xml)
to create `api-CANARY.xml.class-parse`:

```diff
--- a/build-tools/api-merge/merge-configuration.xml
+++ b/build-tools/api-merge/merge-configuration.xml
@@ -25,8 +25,9 @@
     <File Path="api-34.xml.in" Level="34" />
     <File Path="api-35.xml.in" Level="35" />
     <File Path="api-36.xml.in" Level="36" />
+    <File Path="api-CANARY.xml.in" Level="36.1" />
   </Inputs>
   <Outputs>
-    <File Path="api-36.xml" LastLevel="36" />
+    <File Path="api-CANARY.xml" LastLevel="36.1" />
   </Outputs>
 </Configuration>
```

Run the following command to create a merged `api.xml`:

```dotnetcli
./dotnet-local.sh build build-tools/create-android-api/create-android-api.csproj -t:GenerateApiDescription
```
  
This will create a `api-XX.xml` file in `/src/Mono.Android/Profiles/` that needs to be committed.

### Other Infrastructure Changes

- Add level to `/build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks/CheckApiCompatibility.cs`
  to enable running ApiCompat against the new level. (ex: `{ "v11.0.99", "v11.0" }`)
- Add level to `/build-tools/api-xml-adjuster/Makefile`
  [TODO: remove? `$(API_LEVELS)` was last touched for API-34!]
- LOCAL ONLY: Update `Configuration.props` or `Configuration.Override.props` to specify building the new level:
  - `<AndroidApiLevel>31</AndroidApiLevel>`
  - `<AndroidPlatformId>S</AndroidPlatformId>`
  - `<AndroidFrameworkVersion>v11.0.99</AndroidFrameworkVersion>`

Or specify them on the command-line for one-off local builds.

### Building the New Mono.Android

- Build `Xamarin.Android.sln` with your usual mechanism, and the new `Mono.Android.dll` should be built
- Read the note at the bottom of `/src/Mono.Android/metadata` that has a few lines that must be 
  copy/pasted for new API levels
- Add required metadata fixes in `/src/Mono.Android/metadata` until `Mono.Android.csproj` builds
  - Check that new package/namespaces are properly cased

To build *just* `src/Mono.Android/Mono.Android.csproj`:

```dotnetcli
./dotnet-local.sh build src/Mono.Android/*.csproj -p:AndroidApiLevel=36.1 -p:AndroidPlatformId=CANARY -p:AndroidFrameworkVersion=v16.1 -p:IsUnstableVersion=true
```

### New AndroidManifest.xml Elements

- See `build-tools/manifest-attribute-codegen/README.md` for instructions on surfacing any new
  elements or attributes added to `AndroidManifest.xml`.

### ApiCompat

There may be ApiCompat issues that need to be examined.  Either fix the assembly with metadata or allow
acceptable "breaks":

- Add new file to `/tests/api-compatibility`, like `acceptable-breakages-v11.0.99.txt`
- Copy errors reported from ApiCompat task to acceptable breakages file

## Bindings Stabilization

When Google announces that the APIs are frozen, additional work such as enumification is needed.

There have been many, many attempts to "automate" the enumification process in the past, to varying
degrees of success.  The main problem is that no automated process is going to be perfect, so
they all rely on a human verifying and modifying the results.

However this verification process is long and tedious.  Doing it correctly requires almost as much
effort as doing the full process manually.  Thus it generally isn't done correctly and many errors
slip in, leaving our users with bad bindings that are hard to fix in the future without breaking API.

Currently we have taken the opposite approach and do the process completely manually, but we 
have invested in tooling to make the process as easy as possible.

This tooling is BindingStudio:
https://github.com/jpobst/BindingStudio

It's a Winforms app, so it only runs on Windows.  It's ugly as sin, and has very poor UX.  However,
it prompts you with the exact decisions you need to make, and handles as much dirty work as possible,
allowing enumification to be done in a few days.

### Extract constants from API

Using BindingStudio:

- Update `CURRENT_API_LEVEL` in MainForm.cs
- Choose `Tools` -> `Add API Level Constants`
  - Fill in existing `map.csv`: `xamarin-android/src/Mono.Android/map.csv`
  - Fill in new `api.xml`: ex: `xamarin-android/src/Mono.Android/obj/Debug/net6.0/android-32/mcw/api.xml`
- Choose `File` -> `Save`

This adds all the new possible constants from the API level to `map.csv`.  They will be
marked with a `?` indicating no determination has been made if they should be enumified or not.

Example:
```
?,32,android/media/Spatializer.SPATIALIZER_IMMERSIVE_LEVEL_MULTICHANNEL,1,,,,
?,32,android/media/Spatializer.SPATIALIZER_IMMERSIVE_LEVEL_NONE,0,,,,
?,32,android/media/Spatializer.SPATIALIZER_IMMERSIVE_LEVEL_OTHER,-1,,,,
```

### Creating enums

Using BindingStudio:

- Choose `File` -> `Open Constant Map`
- Choose existing `map.csv`: `xamarin-android/src/Mono.Android/map.csv`

The left tree view will be populated with every type that has possible constants that require
a decision.  Clicking a tree node will show the grid of all constants in the type.  The ones 
that need to be handled are the ones with `Action` = `None`.  (The others are shown because
sometimes the correct action is to add a new constant to an existing enum.)

Select the row(s) containing the constants you want to act on.  (Use Control and Shift to select
multiple rows.)  There are 3 possible options for constants:

1) Ignore

If the constant(s) should not be part of an enum (like `Math.PI`), click the `Ignore` toolbar 
button to leave them as constants.

2) Add to existing enum

If the constant(s) should be added to an existing enum:
- Click the `Add to existing enum` toolbar button.
- The dialog will show all other enums in this type
- Choose the existing enum to add the new constant(s) to
- After accepting the dialog, you may need to click the grid to cause it to refresh
- The constant(s) will be marked as `Enumify` with the `EnumFullType` you specified
- The enum member names may need to be tweaked by changing the `EnumMember` column

3) Create a new enum

If the constant(s) should be added to a brand new enum:
- Click the `Create Enum` toolbar button
- In the dialog, a suggested enum namespace and name will be pre-populated. This may need to be
  tweaked as needed.
  - Mark `Is Flags` if this should be a `[Flags]` enum type.
- After accepting the dialog, you may need to click the grid to cause it to refresh
- The constant(s) will be marked as `Enumify` with the `EnumFullType` you specified
- The enum member names may need to be tweaked by changing the `EnumMember` column

Once decisions have been made for all new constants in a type, use the left tree view to move
to the next type.  You should periodically save your progress with `File` -> `Save` in case
BindingStudio crashes.

The left tree view can be updated by saving and reopening the `map.csv` file.

### Extract methods that possibly need enums

Using BindingStudio:

- Update the file paths in `MainForm.FindAPILevelMethodsToolStripMenuItem_Click`
- Run BindingStudio and choose `Tools` -> `Find API Level Methods`

This will create a file of every method in the new API level that takes an `int` as a parameter
or returns an `int` as a return value.  Each method will be marked with a `?` in the file
to indicate a decision needs to be made to ignore it or map it to an enum.

Example:
```
?,32,android/media,AudioAttributes,getSpatializationBehavior,return,
?,32,android/media,AudioAttributes$Builder,setSpatializationBehavior,sb,
```

### Mapping methods

Using BindingStudio:

- Choose `File` -> `Open Constant Map`
  - Choose existing `map.csv`: `xamarin-android/src/Mono.Android/map.csv`
- Choose `File` -> `Open Method Map`
  - Choose the new `.csv` created in the previous step

The left tree will populate with every method that possibly should be enumified and
needs a decision to be made.  Clicking a method shows the Android documentation for
the method to help make the decision, as well as an area to input the decision.

Note a method may show up multiple times, once for each parameter or return type 
(Parameter Name = "return") that is an int.  Each one may require a different action.

There are 3 possible options for a method parameter/return type:

1) Unknown

You don't how to handle this method currently, so leaving it in the initial state
of "Unknown" will leave it alone until a decision can be made.

2) Ignore

The method parameter/return type should remain an `int` and not be converted to an enum.

Ex: 
```
int Add (int value1, int value2) { ... }
```

Click the "Ignore" radio button and then the "Save" button.

3) Enumify

The method parameter/return type should be changed to an enum.

Ex:
```
void AudioAttributesBuilder.SetSpatializationBehavior (int sb) { ... }
```

- Choose the "Enumify" radio option
- Use the DropDown in the middle to select the enum to use
  - When selected, the members of that enum will be shown in the box below the enum
- Alternatively, search for a enum by enum member name using the Search box in the right
  - If desired enum is found, clicking it will populate dropdown
- Click "Save"

Use `File` -> `Save` to save your work often!

### Finishing the method map

The official `methodmap.csv` uses a slightly different format than the one used for enumification.

Using BindingStudio:
- Ensure the "new api level method map" CSV file is loaded.
- Choose `Tools` -> `Export Final Method Map`
- Choose a temporary file name
- Open the temporary file, copy the contents to the bottom of the official:
  - xamarin-android/src/Mono.Android/methodmap.csv

Congrats! Enumification is complete!

---- Somewhat outdated docs below, update when we do this year's stabilization ----

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

## Bindings Finalization


### Update BuildVersionCodes API Level Value

Our enumification process stores the value of the constants in `map.csv`. The build version code
constant for the preview API level is 10000, but changes to eg: 31 when the API goes stable.

Depending on when enumification was done, the 10000 may be stored instead of 31. When the API
goes stable we must update `map.csv` to the correct value.

Search for `android/os/Build$VERSION_CODES` in `map.csv`.

