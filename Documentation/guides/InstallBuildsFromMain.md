# Installing builds from the `main` branch

Each commit from xamarin-android/main (that builds successfully) is
pushed to the [dotnet10 feed][0]. We can install these builds using
`dotnet workload` commands.

## Install the .NET SDK

Begin by installing a nightly build of the .NET SDK from the
[dotnet/installer repo][1]. See the `Microsoft.Dotnet.Sdk.Internal`
version found in [`Version.Details.xml`][2], if you want to get an
exact matching build. If you pick at least the same preview/release
number, it should be *close enough* to work.

You can guess what the URL is, if you need a specific version like
`10.0.100-rc.1.25427.104`, for example:

* Windows x64: https://ci.dot.net/public/Sdk/10.0.100-rc.1.25427.104/dotnet-sdk-10.0.100-rc.1.25427.104-win-x64.exe
* Windows arm64: https://ci.dot.net/public/Sdk/10.0.100-rc.1.25427.104/dotnet-sdk-10.0.100-rc.1.25427.104-win-arm64.exe
* macOS x64: https://ci.dot.net/public/Sdk/10.0.100-rc.1.25427.104/dotnet-sdk-10.0.100-rc.1.25427.104-osx-x64.pkg
* macOS arm64: https://ci.dot.net/public/Sdk/10.0.100-rc.1.25427.104/dotnet-sdk-10.0.100-rc.1.25427.104-osx-arm64.pkg

*See [WindowsOnArm64.md][3] for further details about Windows on Arm64.*

## Install the `android` Workload

On Windows, in a terminal running as Administrator run:

```dotnetcli
> dotnet workload install android --source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json --source https://api.nuget.org/v3/index.json
...
Successfully installed workload(s) android.
```

On macOS, you can prepend `sudo` to the same command.

## Install Android API 36

If you get the error:

```
error XA5207: Could not find android.jar for API level 36.
This means the Android SDK platform for API level 36 is not installed; it was expected to be in `C:\Program Files (x86)\Android\android-sdk\platforms\android-36\android.jar`.
See https://aka.ms/xa5207 for more details.
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
**Full List**. Check **Android API 36** > **Android SDK Platform 36**
and click `Apply Changes`.

[0]: https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet10
[1]: https://github.com/dotnet/installer#table
[2]: ../../eng/Version.Details.xml
[3]: WindowsOnArm64.md
