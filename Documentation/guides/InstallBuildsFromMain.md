# Installing builds from the `main` branch

Each commit from xamarin-android/main (that builds successfully) is
pushed to the [dotnet7 feed][0]. We can install these builds using
`dotnet workload` commands.

## Install the .NET SDK

Begin by installing a nightly build of the .NET SDK from the
[dotnet/installer repo][1]. See the `Microsoft.Dotnet.Sdk.Internal`
version found in [`Version.Details.xml`][2], if you want to get an
exact matching build. If you pick at least the same preview/release
number, it should be *close enough* to work.

You can guess what the URL is, if you need a specific version like
`7.0.100-preview.6.22277.6`, for example:

* Windows x64: https://ci.dot.net/public/Sdk/7.0.100-preview.6.22277.6/dotnet-sdk-7.0.100-preview.6.22277.6-win-x64.exe
* Windows arm64: https://ci.dot.net/public/Sdk/7.0.100-preview.6.22277.6/dotnet-sdk-7.0.100-preview.6.22277.6-win-arm64.exe
* macOS x64: https://ci.dot.net/public/Sdk/7.0.100-preview.6.22277.6/dotnet-sdk-7.0.100-preview.6.22277.6-osx-x64.pkg
* macOS arm64: https://ci.dot.net/public/Sdk/7.0.100-preview.6.22277.6/dotnet-sdk-7.0.100-preview.6.22277.6-osx-arm64.pkg

*See [WindowsOnArm64.md][3] for further details about Windows on Arm64.*

## Install the `android` Workload

On Windows, in a terminal running as Administrator run:

```dotnetcli
> dotnet workload install android --source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet7/nuget/v3/index.json --source https://api.nuget.org/v3/index.json
...
Successfully installed workload(s) android.
```

On macOS, you can prepend `sudo` to the same command.

## Install Android API 32

If you get the error:

```
error XA5207: Could not find android.jar for API level 32.
This means the Android SDK platform for API level 32 is not installed.
Either install it in the Android SDK Manager (Tools > Android > Android SDK Manager...),
or change your .NET for Android project to target an API version that is installed.
```

On Windows, if your Android SDK is located in `C:\Program Files
(x86)\Android\android-sdk`, you will need a terminal running as
administrator to run:

```dotnetcli
> dotnet build -t:InstallAndroidDependencies -p:AndroidManifestType=GoogleV2 -p:AcceptAndroidSDKLicenses=true
```

*On macOS, you might not even need `sudo` for this command.*

Alternatively, you can open the `Android SDK Manager` as described,
click the gear icon in the bottom right, and select **Repository** >
**Full List**. Check **Android API 32** > **Android SDK Platform 32**
and click `Apply Changes`.

## Using Visual Studio

If you want to use Visual Studio on Windows, make sure to install at
least Visual Studio 2022 17.3 (currently in preview) and the `.NET
Multi-platform App UI development` workload.

The .NET 7 SDK has some changes to optional workloads that requires
some manual changes.

If you look in `C:\Program Files\dotnet\sdk-manifests`, and have
folders such as:

```
6.0.300
7.0.100
7.0.100-preview.6
```

You will need to copy any subfolders in `7.0.100-preview.6` to the
`7.0.100` directory. This will be resolved as soon as the .NET 7
workload resolver is updated in Visual Studio.

[0]: https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet7
[1]: https://github.com/dotnet/installer#table
[2]: ../../eng/Version.Details.xml
[3]: WindowsOnArm64.md
