Xamarin.Installer.Build.Tasks
=================================================

Xamarin.Installer.Build.Tasks contains the build tasks and targets that support the 
`InstallAndroidDependencies` target that ships with the .NET for Android SDK.


### Testing

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

### OpenJDK signing-key maintenance

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
