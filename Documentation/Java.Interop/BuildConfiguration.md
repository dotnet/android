# Build Configuration

The standalone Java.Interop tooling build uses `dotnet` and MSBuild properties.
See the [README](README.md#building) for preparation and build commands.
Android runtime integration is built as part of the enclosing dotnet/android
[SDK build](../README.md#building-from-source).

## MSBuild Properties

Projects use the repository's shared
[`Directory.Build.props`](../../Directory.Build.props) and
[`Configuration.props`](../../Configuration.props).
MSBuild properties may be placed in the repository-root
`Configuration.Override.props` or passed with `-p:Name=Value`.

The repository defaults to C# preview and `MemorySafetyRulesVersion=2` in
`Directory.Build.props`, including test projects. Use the compiler provisioned
by `make prepare`; older language modes do not support these rules.
The isolated workload-dependency and Arcade internal-tools builds opt in
separately because they do not import the root props. Android project templates
also carry the settings explicitly so generated projects retain them outside
the repository.

Overridable MSBuild properties include:

* `$(Configuration)`: Build configuration, normally `Debug` or `Release`.
* `$(JavaSdkDirectory)`, `$(JavaCPath)`, and `$(JarPath)`: JDK directory and paths
    to `javac` and `jar`, supplied by the shared `Configuration.props`.
    `make prepare` provisions the configured Microsoft OpenJDK. For standalone
    tooling builds, pass `-p:JavaSdkDirectory=/path/to/jdk` to use an installed JDK.
    These are used for Java tooling and test fixtures, not a desktop JNI test host.
* `$(MicrosoftAndroidSdkOutDir)`: Output directory for tools such as
    [`class-parse`](../../src/class-parse) and [`generator`](../../src/generator),
    within the Android SDK pack's `tools/` directory.
* `$(BuildOutputDirectory)` and `$(TestOutputDirectory)`: Libraries and tooling
    tests use `bin/$(Configuration)/net10.0/` and
    `bin/Test$(Configuration)/net10.0/`, respectively.

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

Tooling tests remain under `tests/` and run with
`dotnet test` on the individual test `.csproj` files. They do not require a
locally built Android SDK; Java/Gradle fixtures still require the configured JDK.

Core JNI interop sources and Java fixtures live in
[`tests/Mono.Android-Tests/Java.Interop-Tests/`](../../tests/Mono.Android-Tests/Java.Interop-Tests/)
and run on Android through
[`Mono.Android.NET-Tests.csproj`](../../tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj).
These tests require a locally built Android SDK and a device or emulator.
There is no standalone core host-JVM harness or `make run-tests` workflow.
