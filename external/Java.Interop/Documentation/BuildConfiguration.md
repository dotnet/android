# Build Configuration

The Java.Interop build can be configured by specifying MSBuild properties to control
behavior or by overriding **make**(1) variables on the command line.

## MSBuild Properties

MSbuild properties may be placed into the file `Configuration.Override.props`,
which can be copied from
[`Configuration.Override.props.in`](Configuration.Override.props.in).
The `Configuration.Override.props` file is `<Import/>`ed by
[`Directory.Build.props`](Directory.Build.props); there is no need to
`<Import/>` it within other project files.

Overridable MSBuild properties include:

* `$(CecilSourceDirectory)`: If the empty string, Cecil will be obtained from
    NuGet packages.  Otherwise, `$(UtilityOutputFullPath)Xamarin.Android.Cecil.dll`
    will be used to reference Cecil.
* `$(JdkJvmPath)`: Full path name to the JVM native library to use for
    tests which require a desktop JVM. By default this is probed from the
    configured JDK.
* `$(JavaCPath)`: Path to the `javac` command-line tool, by default set to `javac`.
* `$(JarPath)`: Path to the `jar` command-line tool, by default set to `jar`.
  * It may be desirable to override these on Windows, depending on your `PATH`.
* `$(UtilityOutputFullPath)`: Directory to place various utilities such as
    [`class-parse`](tools/class-parse) and [`generator`](tools/generator).
    This value should be a full path.
    By default this is `$(MSBuildThisFileDirectory)bin/$(Configuration)`.

## Native source validation

`src/java-interop/java-interop.csproj` builds the `java-interop-validation`
static library with CMake to validate the shared dynamic-loading, utility, and
core native sources on supported Windows, macOS, and Linux hosts. It requires
a host C/C++ toolchain and CMake, but no Mono runtime pack or Mono headers.
From the enclosing dotnet/android repository, run:

```sh
dotnet build external/Java.Interop/src/java-interop/java-interop.csproj
```

The archive is written to the project's configuration/framework-specific
intermediate directory (`obj/Debug/net10.0` by default). An unchanged build skips
native compilation; `dotnet clean` removes the validation archive.

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

## **make**(1) variables

The following **make**(1) variables may be specified:

* `$(CONFIGURATION)`: The product configuration to build, and corresponds
    to the `$(Configuration)` MSBuild property when running `$(MSBUILD)`.
    Valid values are `Debug` and `Release`. Default value is `Debug`.
* `$(RUNTIME)`: The managed runtime to use to execute utilities, tests.
    Default value is `mono64` if present in `$PATH`, otherwise `mono`.
* `$(TESTS)`: Which unit tests to execute. Useful in conjunction with the
    `make run-tests` target:

        make run-tests TESTS=bin/Debug/generator-Tests.dll

    Core JNI interop sources and Java fixtures live in
    `tests/Mono.Android-Tests/Java.Interop-Tests/` and run on Android through
    `tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj`
    in the enclosing dotnet/android repository.

* `$(V)`: If set to a non-empty string, adds `/v:diag` to `$(MSBUILD_FLAGS)`
    invocations.
* `$(MSBUILD)`: The MSBuild build tool to execute for builds.
    Default value is `xbuild`.
