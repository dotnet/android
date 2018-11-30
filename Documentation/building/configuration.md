# Build Configuration

The Xamarin.Android build is heavily dependent on MSBuild, with the *intention*
that it should (eventually?) be possible to build the project simply by
checking out the repo, loading `Xamarin.Android.sln` into an IDE, and Building
the solution. (This isn't currently possible, and may never be, but it's
the *vision*.)

However, some properties may need to be altered in order to suit your
requirements, such as the location of a cache directory to store
the Android SDK and NDK.

To modify the build process, copy
[`Configuration.Override.props.in`](../../Configuration.Override.props.in)
to `Configuration.Override.props`, and edit the file as appropriate.
`Configuration.Override.props` is `<Import/>`ed by `Configuration.props`
and will override any default values specified in `Configuration.props`.

Overridable MSBuild properties include:

  * `$(AutoProvision)`: Automatically install required dependencies, if possible.
    Only supported on macOS and certain Linux distros.

  * `$(AutoProvisionUsesSudo)`: Use `sudo` when installing dependencies.

  * `$(AndroidApiLevel)`: The Android API level to bind in `src/Mono.Android`.
    This is an integer value, e.g. `15` for
    [API-15 (Android 4.0.3)](http://developer.android.com/about/versions/android-4.0.3.html).

  * `$(AndroidFirstFrameworkVersion)`: The first `$(TargetFrameworkVersion)`
    which will be built by `make jenkins` and included in the installer.
    Currently `v4.4`.
    This controls what is included in `build-tools/create-vsix` packages.

  * `$(AndroidFrameworkVersion)`: The Xamarin.Android `$(TargetFrameworkVersion)`
    version which corresponds to `$(AndroidApiLevel)`. This is *usually* the
    Android version number with a leading `v`, e.g. `v4.0.3` for API-15.

  * `$(AndroidLatestStableApiLevel)`: The highest/latest Android API level that
    has a stable API. The `src/Xamarin.Android.Build.Tasks` build uses this
    value to reference files built within `src/Mono.Android`.

    This should be consistent with `$(AndroidLatestStableFrameworkVersion)` and
    `$(AndroidLatestStablePlatformId)`.

    This should only be updated when a new API level is declared stable.

  * `$(AndroidLatestStableFrameworkVersion)`: The highest/latest Xamarin.Android
    `$(TargetFrameworkVersion)` value which has a stable API.
    The `src/Xamarin.Android.Build.Tasks` build uses this value to reference
    files built within `src/Mono.Android`.

    This should be consistent with `$(AndroidLatestStableApiLevel)` and
    `$(AndroidLatestStablePlatformId)`.

    This should only be updated when a new API level is declared stable.

  * `$(AndroidLatestStablePlatformId)`: The highest/latest Android platform ID
    which has a stable API.
    The `src/Xamarin.Android.Build.Tasks` build uses this value to reference
    files built within `src/Mono.Android`.

    This should be consistent with `$(AndroidLatestStableApiLevel)` and
    `$(AndroidLatestStableFrameworkVersion)`.

    This should only be updated when a new API level is declared stable.

  * `$(AndroidPlatformId)`: The "Platform ID" for the `android.jar` to use when
    building `src/Mono.Android`. This is usually the same value as
    `$(AndroidApiLevel)`, but may differ with Android Preview releases.

  * `$(AndroidSupportedHostJitAbis)`: The Android ABIs for which to build a
    host JIT *and* Xamarin.Android base class libraries (`mscorlib.dll`/etc.).
    The "host JIT" is used e.g. with the Xamarin Studio Designer, to render
    Xamarin.Android apps on the developer's machine.
    There can also be support for cross-compiling mono for a different
    host, e.g. to build Windows `libmonosgen-2.0.dll` from OS X.
    Supported host values include:

      * `Darwin`
      * `Linux`
      * `mxe-Win64`: Cross-compile Windows 64-bit binaries from Unix.

    The default value is `$(HostOS)`, where `$(HostOS)` is based on probing
    various environment variables and filesystem locations.
    On OS X, the default would be `Darwin`.

  * `$(AndroidSupportedTargetAotAbis)`: The Android ABIs for which to build the
    Mono AOT compilers. The AOT compilers are required in order to set the
    [`$(AotAssemblies)`][aot-assemblies] app configuration property to True.

    [aot-assemblies]: https://developer.xamarin.com/guides/android/under_the_hood/build_process/#AotAssemblies

  * `$(AndroidSupportedTargetJitAbis)`: The Android ABIs for which to build the
    the Mono JIT for inclusion within apps. This is a `:`-separated list of
    ABIs to build. Supported values are:

      * `armeabi-v7a`
      * `arm64-v8a`
      * `x86`
      * `x86_64`

  * `$(AndroidToolchainCacheDirectory)`: The directory to cache the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-archives`.

  * `$(AndroidToolchainDirectory)`: The directory to install the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-toolchain`.

  * `$(HostCc)`, `$(HostCxx)`: The C and C++ compilers to use to generate
    host-native binaries.

  * `$(IgnoreMaxMonoVersion)`: Skip the enforcement of the `$(MonoRequiredMaximumVersion)`
    property. This is so that developers can run against the latest
    and greatest. But the build system can enforce the min and max 
    versions. The default is `true`, however on Jenkins we use:

         /p:IgnoreMaxMonoVersion=False

  * `$(JavaInteropSourceDirectory)`: The Java.Interop source directory to
    build and reference projects from. By default, this is
    `external/Java.Interop` directory, maintained by `git submodule update`.

  * `$(MakeConcurrency)`: **make**(1) parameters to use intended to influence
    the number of CPU cores used when **make**(1) executes. By default this uses
    `-jCOUNT`, where `COUNT` is obtained from `sysctl hw.ncpu`.

  * `$(MonoRequiredMinimumVersion)`: The minimum *system* mono version that is
    supported in order to allow a build to continue. Policy is to require a
    system mono which corresponds vaguely to the [`external/mono`](external)
    version. This is not strictly required; older mono versions *may* work, they
    just are not tested, and thus not guaranteed or supported.

  * `$(MonoRequiredMaximumVersion)`: The max *system* mono version that is
    required. This is so that we can ensure a stable build environment by
    making sure we dont install unstable versions.

  * `$(MonoSgenBridgeVersion)`: The Mono SGEN Bridge version to support.
    Valid values include:

      * `4`: Mono 4.6 support.
      * `5`: Mono 4.8 and above support. This is the default.

  * `$(XAIncludeProprietaryBits)`: Defaults to `False`. When enabled, this flag
    signals `proprietary.csproj` to copy proprietary assemblies from your system's
    Xamarin.Android install to the local build output. This enables proprietary
    features such as debugging and fast deployment. Since a "normal" OSS build would
    not include proprietary files, this flag also emits a warning when enabled.
