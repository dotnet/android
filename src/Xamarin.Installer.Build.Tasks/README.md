Xamarin.Installer.Build.Tasks
=================================================

Xamarin.Installer.Build.Tasks contains the build tasks and targets that support the 
`InstallAndroidDependencies` target that ships with the .NET for Android SDK.


### Testing

The Xamarin.Installer.Build.Tests project contains a test project that is used to test the 
`InstallAndroidDependencies` task via a mock MSBuild environment.

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
