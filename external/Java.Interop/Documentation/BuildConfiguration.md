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
* `$(JdkJvmPath)`: Full path name to the JVM native library to link
    [`java-interop`](src/java-interop) against. By default this is
    probed for from numerous locations within
    [`build-tools/scripts/jdk.mk`](build-tools/scripts/jdk.mk).
* `$(JavaCPath)`: Path to the `javac` command-line tool, by default set to `javac`.
* `$(JarPath)`: Path to the `jar` command-line tool, by default set to `jar`.
  * It may be desirable to override these on Windows, depending on your `PATH`.
* `$(UtilityOutputFullPath)`: Directory to place various utilities such as
    [`class-parse`](tools/class-parse), [`generator`](tools/generator),
    and [`logcat-parse`](tools/logcat-parse). This value should be a full path.
    By default this is `$(MSBuildThisFileDirectory)bin/$(Configuration)`.

## **make**(1) variables

The following **make**(1) variables may be specified:

* `$(CONFIGURATION)`: The product configuration to build, and corresponds
    to the `$(Configuration)` MSBuild property when running `$(MSBUILD)`.
    Valid values are `Debug` and `Release`. Default value is `Debug`.
* `$(RUNTIME)`: The managed runtime to use to execute utilities, tests.
    Default value is `mono64` if present in `$PATH`, otherwise `mono`.
* `$(TESTS)`: Which unit tests to execute. Useful in conjunction with the
    `make run-tests` target:

        make run-tests TESTS=bin/Debug/Java.Interop.Dynamic-Tests.dll

* `$(V)`: If set to a non-empty string, adds `/v:diag` to `$(MSBUILD_FLAGS)`
    invocations.
* `$(MSBUILD)`: The MSBuild build tool to execute for builds.
    Default value is `xbuild`.


