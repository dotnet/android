# Build Configuration

The standalone Java.Interop tooling build uses `dotnet` and MSBuild properties.
See the [README](../README.md#building) for preparation and build commands.
Android runtime integration is built as part of the enclosing dotnet/android
[SDK build](../../../Documentation/README.md#building-from-source).

## MSBuild Properties

MSBuild properties may be placed into `external/Java.Interop/Configuration.Override.props`,
which can be copied from
[`Configuration.Override.props.in`](../Configuration.Override.props.in).
The `Configuration.Override.props` file is `<Import/>`ed by
[`Directory.Build.props`](../Directory.Build.props); there is no need to
`<Import/>` it within other project files.

Overridable MSBuild properties include:

* `$(Configuration)`: Build configuration, normally `Debug` or `Release`.
* `$(JdksRoot)`: JDK location used during preparation. Preparation also checks
    `JAVA_HOME` and installed JDKs, and writes the selected Java tool paths to
    `bin/Build$(Configuration)/JdkInfo.props`.
* `$(JavaSdkDirectory)`, `$(JavaCPath)`, and `$(JarPath)`: JDK directory and paths
    to `javac` and `jar`, normally loaded from the generated `JdkInfo.props`.
    These are used for Java tooling and test fixtures, not a desktop JNI test host.
* `$(UtilityOutputFullPathCoreApps)`: Full output directory for utilities such as
    [`class-parse`](../tools/class-parse) and [`generator`](../tools/generator).
    The enclosing repository sets this to the Android SDK pack's `tools`
    directory in [`Java.Interop.override.props`](../../Java.Interop.override.props).
    Without that override, tools use `bin/$(Configuration)-net10.0/` under
    `external/Java.Interop/`, as defined by
    [`TargetFrameworkDependentValues.props`](../TargetFrameworkDependentValues.props).

## Native registration

`JniAddNativeMethodRegistrationAttribute` is obsolete and has no effect. It
remains available for compatibility, and using it produces a compiler warning
rather than an error. The unused
`JniRuntime.CreationOptions.JniAddNativeMethodRegistrationAttributePresent`
property has been removed. Android native registration uses the generated
trimmable typemap instead.

## Runtime exception support

`JniEnvironment.Exceptions.Throw(Exception)` delegates to the active
`JniRuntime.RaisePendingException()` implementation. Custom runtimes must override
that method to translate managed exceptions that are not `JavaException` instances;
the base implementation only throws an existing Java throwable into JNI.

.NET for Android's CoreCLR and NativeAOT runtimes share the Android exception
implementation, using the generated `Android.Runtime.JavaProxyThrowable` peer.
This preserves the original managed exception when it returns through JNI or
appears as a Java throwable's cause. Java.Interop no longer supplies a separate
proxy implementation or `java-interop.jar`.

## Tests

Tooling tests remain under `external/Java.Interop/tests/` and run with
`dotnet test` on the individual test `.csproj` files. They do not require a
locally built Android SDK; Java/Gradle fixtures still require the configured JDK.

Core JNI interop sources and Java fixtures live in
[`tests/Mono.Android-Tests/Java.Interop-Tests/`](../../../tests/Mono.Android-Tests/Java.Interop-Tests/)
and run on Android through
[`Mono.Android.NET-Tests.csproj`](../../../tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj).
These tests require a locally built Android SDK and a device or emulator.
There is no standalone core host-JVM harness or `make run-tests` workflow.
