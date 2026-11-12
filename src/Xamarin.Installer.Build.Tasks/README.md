Xamarin.Installer.Build.Tasks
=================================================

Contains the `InstallAndroidDependencies` target that ships with the .NET for
Android SDK. It installs the Microsoft OpenJDK and Android SDK packages required
by a project.

The target is implemented in MSBuild XML in `Xamarin.Installer.Common.targets`.
`VerifyOpenJdkSignature` is the only custom task; it verifies the downloaded JDK
archive before extraction.

### Testing

Build the local SDK, then run:

```text
dotnet-local.cmd test bin\TestDebug\net11.0\Xamarin.Android.Build.Tests.dll --filter FullyQualifiedName~OpenJdkSignatureTests
```

To try the target directly, create a project with the local SDK and run:

```text
dotnet-local.cmd build -t:InstallAndroidDependencies -p:AndroidSdkDirectory=/path/to/sdk -p:AcceptAndroidSDKLicenses=true
```
