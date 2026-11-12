Xamarin.Installer.Build.Tasks
=================================================

Xamarin.Installer.Build.Tasks contains the build tasks and targets that support the 
`InstallAndroidDependencies` target that ships with the .NET for Android SDK.


### XML installer prototype

On this branch, `InstallAndroidDependencies` installs both the JDK and Android SDK
through MSBuild XML, without loading the installer task assembly. The C# sources
and their tests remain as a baseline for comparison; they are not a fallback.
Dependency discovery still uses the existing `GetAndroidDependencies` target.

The XML uses standard MSBuild tasks for downloads, SHA-256 checks, and ZIP
extraction. Unix hosts also require `tar`, `chmod`, and `mv`. JDK signature
verification requires `gpg` on `PATH`, using an isolated keyring and the same
decoded-public-key SHA-256 pin as the C# implementation. No user keyring is trusted.

Run the standalone fixtures and build the installer from the repository root:

```sh
python3 tests/Xamarin.Installer.Build.Tasks-Tests/test_xml_installer.py -v
dotnet build src/Xamarin.Installer.Build.Tasks/Xamarin.Installer.Build.Tasks.csproj -v:minimal -nr:false
```

The fixtures import the actual targets without any repository task assemblies.
They serve mock downloads locally, generate signed JDK archives, and cover package
mapping, installed revisions, repeat runs, license consent, CLI cache repair,
hash/signature rejection, timeouts, and paths containing spaces.
Signature fixtures require GnuPG; the fixture suite currently skips Windows.

For a real-download smoke test independent of a full SDK build, save this as
`installer-smoke.proj` in the repository root:

```xml
<Project>
  <Import Project="$(InstallerTargets)" />
  <Target Name="GetAndroidDependencies">
    <ItemGroup>
      <AndroidDependency Include="platform-tools" />
      <JavaDependency Include="jdk" Version="21.0.8" />
    </ItemGroup>
  </Target>
</Project>
```

Then run on macOS/Linux, accepting the Android SDK licenses only if appropriate:

```sh
dotnet msbuild installer-smoke.proj -t:InstallAndroidDependencies \
  -p:InstallerTargets="$PWD/src/Xamarin.Installer.Build.Tasks/Xamarin.Installer.Common.targets" \
  -p:AndroidSdkDirectory="$PWD/bin/installer-prototype/real-sdk" \
  -p:JavaSdkDirectory="$PWD/bin/installer-prototype/real-jdk" \
  -p:_AndroidCliDirectory="$PWD/bin/installer-prototype/real-cli" \
  -p:AcceptAndroidSDKLicenses=true -v:normal -nr:false
```

Validated on macOS arm64: Microsoft JDK 21.0.8 downloaded, signature-verified,
extracted, and executed; Google's pinned Android CLI downloaded and hash-checked;
platform-tools 37.0.1 installed and `adb version` executed. A second identical
invocation performed no downloads or installs. All 15 fixtures and the installer
project build passed.

This is not yet a production-equivalent replacement. GnuPG is an additional
prerequisite, diagnostics read the copied English `Properties/Resources.resx`
rather than satellite assemblies, and the timeout applies per external command,
not across the whole operation or to `DownloadFile`. Windows/Linux execution,
full workload packaging, and concurrent cache publishing are unvalidated. Windows
Program Files protection and strict JDK-version parsing still need parity work.
SDK/JDK repeat-run decisions inspect installed versions rather than timestamps;
the CLI cache is hash-checked before incremental download decisions.

### C# baseline tests

`OpenJdkSignatureTests` in `Xamarin.Android.Build.Tests` covers signature
verification and task diagnostics. Build the local SDK and test assembly first,
then run the focused tests from the repository root:

```text
dotnet-local.cmd test bin\TestDebug\net10.0\Xamarin.Android.Build.Tests.dll --filter FullyQualifiedName~OpenJdkSignatureTests
```

The explicit integration tests download a Microsoft JDK archive, verify its
signature, reject changed archive contents, and install the JDK through the
production dependency task:

```text
dotnet-local.cmd test bin\TestDebug\net10.0\Xamarin.Android.Build.Tests.dll --filter "FullyQualifiedName~OpenJdkSignatureTests.PublishedMicrosoftArchive|FullyQualifiedName~OpenJdkSignatureTests.InstallMicrosoftJdk"
```

### C# baseline OpenJDK signing-key maintenance

For this prototype, the URL and pin are also present in
`Xamarin.Installer.Common.targets`; keep both implementations synchronized.

Before a downloaded JDK archive is extracted, the task downloads its `.sig` file
and the public key linked by [Microsoft's OpenJDK download documentation][1].
By default, `OpenJdkSignatureVerifier` accepts that key only if the SHA-256 hash
of its decoded OpenPGP packets matches `PublicKeySha256`. The repository stores
the hash, not the public key. No keys from the archive or user key stores are
trusted.

The private `_AndroidOpenJdkPublicKeyUrl` property is blank by default. A blank
value uses the published URL fixed in the task. An override changes only the
download location; the downloaded key must still match the configured trusted hash.

The private `_AndroidOpenJdkPublicKeySha256` property is also blank by default,
which selects the built-in hash. An explicit value replaces the trusted key hash
and must contain exactly 64 hexadecimal characters; letter case does not matter.
Changing this value changes which signing key is trusted. Obtain it from a
verified source, not automatically from an unexpected key download.

To update the key, confirm the replacement key and its URL with the Microsoft
OpenJDK publisher, decode the ASCII armor, and compute SHA-256 over the complete
decoded key ring. Review changes to `PublicKeyUrl` and `PublicKeySha256` together
and run the explicit published-archive test. Do not automatically replace the
trusted hash with the hash of an unexpected download.

The format reader supports only version 4 binary-document RSA signatures with
SHA-256, SHA-384, or SHA-512. .NET performs RSA PKCS#1 signature verification.
Unsupported signature formats, missing files, and key-hash mismatches stop
installation.

### Testing the full target

The following steps can be used to test the `InstallAndroidDependencies` target directly
_without_ building dotnet/android, though it does require a dotnet/android checkout and some setup.

* Clone or navigate to your `dotnet/android` checkout, and install the sandboxed .NET preview:
    ```
    bash ./eng/install-dotnet.sh                                    # macOS / Linux
    pwsh ./eng/install-dotnet.ps1                                   # Windows
    dotnet build src/workloads/workloads.csproj
    ```

* Download the `nuget-unsigned` build artifact from the [latest build][0] from the branch you want to test, and move it to the `bin/BuildDebug` folder.
    ```
    mv ~/Downloads/nuget-unsigned bin/BuildDebug/nuget-unsigned/
    ```

* Set up a sandboxed .NET for Android workload install:
    ```
    dotnet build -t:ExtractWorkloadPacks build-tools/create-packs/Microsoft.Android.Sdk.proj
    ````

Alternatively, follow build instructions in the README to build from scratch.
After setting up this sandbox environment, you can drag over your custom Xamarin.Installer.Build.Tasks outputs.

* Build your custom branch of Xamarin.Installer.Build.Tasks
    ```
    dotnet build Xamarin.Installer.Build.Tasks.csproj
    ```

* Copy the relevant `Xamarin.Installer` outputs to the sandboxed .NET preview set up earlier:
    ```
    cp /path/to/android-sdk-installer/Xamarin.Installer.Build.Tasks/bin/Debug/*.dll bin/Debug/dotnet/packs/Microsoft.Android.Sdk.Darwin/34.99.0-preview.7.346/tools
    ```

* Create and build a test project that will run against your sandboxed .NET preview:
    ```
    bin/Debug/dotnet/dotnet new android -o InstallDepsTest
    bin/Debug/dotnet/dotnet build --configfile NuGet.config -v:n -tl:off -t:InstallAndroidDependencies InstallDeps -p:AndroidSdkDirectory=/path/to/empty/sdk -p:AcceptAndroidSDKLicenses=true
    ```


[0]: https://devdiv.visualstudio.com/DevDiv/_build?definitionId=11410&_a=summary
[1]: https://learn.microsoft.com/java/openjdk/download#public-key-to-verify-signature-files
