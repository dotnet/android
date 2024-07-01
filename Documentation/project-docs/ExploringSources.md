# .NET for Android Directory Structure

There are two project configurations, `Debug` and `Release`. The project
configuration is controlled by the `$(Configuration)` MSBuild property.
By default this is `Debug`. [`make jenkins`](../building/unix/instructions.md)
will build both `Debug` and `Release` configurations. The configuration name
is used within many of the output directories which are created.


  * `bin`: Build output.
      * `Build$(Configuration)`: Artifacts needed to *build Xamarin.Android itself*.
        Files within this directory will not be needed to build apps outside of
        the xamarin-android repo.
      * `$(Configuration)`: These are the redistributable artifacts you're looking for.
          * `bin`: Programs that are intended to be installed into `$(prefix)/bin`,
	    for Unix-style installable files.
          * `packs\Microsoft.Android.Sdk.$(HostOS)\$(AndroidPackVersion)\tools`: MSBuild project integrations.
            This is where `Xamarin.Android.CSharp.targets` lives.
          * `lib\xamarin.android\xbuild-frameworks\MonoAndroid\v1.0`:
            Xamarin.Android BCL assemblies
          * `lib\xamarin.android\xbuild-frameworks\MonoAndroid\*`: Xamarin.Android
            framework assemblies. This is where the
            [`$(TargetFrameworkVersion)`-specific assemblies live](#tfv).
      * `Test$(Configuration)`: Unit test output root.
  * `build-tools`: Tooling that is used only to build the xamarin-android repo itself.
    Output from these projects appears in `bin/Build$(Configuration)`.
      * `android-toolchain`: Maintains the Android NDK and SDK within
        `$(AndroidToolchainDirectory)`.
      * `api-merge`: Merges API descriptions; used by `src/Mono.Android`
      * `jnienv-gen`: Generator for `Android.Runtime.JNIEnv`; used by `src/Mono.Android`
      * `mono-runtimes`: Builds mono
  * `Documentation`: Project documentation
  * `external`: git submodules
      * `Java.Interop`: Core JNI interaction support
      * `mono`: Used to execute IL on Android
  * `packages`: NuGet packages; created by [`make prepare`](../building/unix/instructions.md).
  * `samples`: Sample applications.
  * `src`: Projects which are redistributable, the outputs of which will be in
    `bin/$(Configuration)`.
      * `Mono.Android`: Builds `Mono.Android.dll` for a specific
        [`$(AndroidApiLevel)` and `$(AndroidFrameworkVersion)`](../building/configuration.md).
      * `Mono.Android.Export`: Builds `Mono.Android.Export.dll`.
      * `Xamarin.Android.Build.Tasks`: MSBuild tasks for Xamarin.Android projects.
      * `Xamarin.Android.Build.Utilities`: MSBuild tasks support sources.
      * `Xamarin.Android.NUnitLite`: NUnitLite for Android sources.
      * `Xamarin.AndroidTools.Aidl`: AIDL processor, used in MSBuild tasks.
      * `Xamarin.Android.Tools.BootstrapTasks`: supplemental build tasks used by
        some build-tools. (This should be in `build-tools`; oops.)
  * `tools`: Utilities which are built into `bin/$(Configuration)`.

<a name="tfv" />

# `$(TargetFrameworkVersion)`s

There is a separate `Mono.Android.dll` *binding assembly* for each API level.

The `bin/$(Configuration)/lib/xamarin.android/xbuild-frameworks/MonoAndroid`
directory contains directories based on `$(TargetFrameworkVersion)` values,
where each `$(TargetFrameworkVersion)` value is specific to an Android API level.

This means there is no *single* `Mono.Android.dll`, there is instead a *set*
of them.

This complicates the "mental model" for the `Mono.Android` project, as
a *project* can have only one output, not many (...within reason...).
As such, building the `Mono.Android` project will only generate a single
`Mono.Android.dll`.

To control which API level is bound, set the `$(AndroidApiLevel)` and
`$(AndroidFrameworkVersion)` MSBuild properties. `$(AndroidApiLevel)` is the
Android API level, *usually* a number, while `$(AndroidFrameworkVersion)`
is the Xamarin.Android `$(TargetFrameworkVersion)`.

The default values will target Android API-27/Android 8.1.

For example, to generate `Mono.Android.dll` for API-19 (Android 4.4):

    cd src/Mono.Android
    msbuild /p:AndroidApiLevel=19 /p:AndroidFrameworkVersion=v4.4
    # creates bin\Debug\lib\xamarin.android\xbuild-frameworks\MonoAndroid\v4.4\Mono.Android.dll
