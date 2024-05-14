# Unit Tests

Unit test infrastructure for .NET for Android.

# Tools

Most of our unit test infrastructure uses NUnit.
Many of the BCL tests for Classic Xamarin.Android rely on an xUnit test runner.

 1. [NUnit](https://github.com/nunit/nunit)
 2. [xUnit](https://github.com/xunit/xunit)

# Project Test Types

There are five types of unit tests within this repo:

 1. [MSBuild Integration Tests](#msbuild-integration-tests): integration tests
    which exercise an entire MSBuild "pipeline".  Most tests are
    MSBuild Integration Tests.

 2. [MSBuild Task Unit Tests](#msbuild-task-tests): unit tests which test a
    single MSBuild task in isolation, *without* involving the MSBuild engine,
    `.targets` files, or anything else.

 3. [Device Integration Tests](#devive-integration-tests): A superset of
    MSBuild Integration Tests, these exercise an entire MSBuild "pipeline"
    *and also* install and run the resulting app on-device.

 4. [On-Device Unit Tests](#device-unit-tests): A set of NUnit unit tests which
    run on-device.

 5. [Other Tests](#other-tests): Tests which don't easily fit into the previous
    types.

# Test Count

| Test                  | Count |
| :------------------:  | ----: |
| MSBuild Tests         |   436 |
| MSBuild Device Tests  |   105 |
| On Device Tests       |   177 |


# Running Tests

Running tests in an IDE is not currently supported.

After [building the repo](../building), the
[MSBuild Integration Tests](#msbuild-integration-tests),
[MSBuild Task Unit Tests](#msbuild-task-tests), and
[Device Integration Tests](#devive-integration-tests) tests can be run
by using the [`dotnet-local.sh`](../../dotnet-local.sh) and
[`dotnet-local.cmd`](../../dotnet-local.cmd) scripts in the top directory of
the checkout.  The `dotnet-local*` scripts are wrappers around a custom
`dotnet` installation which the build downloads and installs into
`bin/Debug/dotnet` or `bin/Release/dotnet` (depending on your configuration).

## Running on macOS & Linux

On macOS and Windows we can use the `dotnet-local.sh` script to run the tests.

To run ALL the [MSBuild Integration Tests](#msbuild-integration-tests) *and*
all the [MSBuild Task Unit Tests](#msbuild-task-tests), run:

```sh
./dotnet-local.sh test bin/TestDebug/net7.0/Xamarin.Android.Build.Tests.dll --filter=Category!=DotNetIgnore
```

To run ALL the supported [Device Integration Tests](#devive-integration-tests), run:

```sh
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/net7.0/MSBuildDeviceIntegration.dll --filter=Category!=DotNetIgnore
```

If no Android device is attached, then the emulator will be created.
The `ADB_TARGET` environment variable can be used to explicitly specify which
Android device should be used when running Device Integration Tests.

NOTE: Not all tests work under .NET for Android yet. So we need to filter
them on the `DotNetIgnore` category.

To run a specific test you can use the `Name=Value` argument for `--filter`,

```sh
./dotnet-local.sh test bin/TestDebug/net7.0/Xamarin.Android.Build.Tests.dll --filter=Name=BuildBasicApplication
```

If the test has arguments then the `=` will not match the name. Instead use the `~`,
this does a "Contains" check for the Name.

```sh
./dotnet-local.sh test bin/TestDebug/net7.0/Xamarin.Android.Build.Tests.dll --filter=Name~BuildBasicApplication
```

To list all the available tests use the `-lt` argument

```sh
./dotnet-local.sh test bin/TestDebug/net7.0/Xamarin.Android.Build.Tests.dll -lt
```

## Running on Windows

On Windows we can use the `dotnet-local.cmd` script to run the tests.

To run ALL the [MSBuild Integration Tests](#msbuild-integration-tests) *and*
all the [MSBuild Task Unit Tests](#msbuild-task-tests), run:

```cmd
dotnet-local.cmd test bin\TestDebug\net7.0\Xamarin.Android.Build.Tests.dll --filter=Category!=DotNetIgnore
```

To run ALL the supported [Device Integration Tests](#devive-integration-tests), runs:

```cmd
dotnet-local.cmd test bin\TestDebug\MSBuildDeviceIntegration\net7.0\MSBuildDeviceIntegration.dll --filter=Category!=DotNetIgnore
```

If no Android device is attached, then the emulator will be created.
The `ADB_TARGET` environment variable can be used to explicitly specify which
Android device should be used when running Device Integration Tests.

NOTE: Not all tests work under .NET for Android yet. So we need to filter
them on the `DotNetIgnore` category.

To run a specific test you can use the `Name=Value` argument for the `--filter`,

```cmd
dotnet-local.cmd test bin\TestDebug\net7.0\Xamarin.Android.Build.Tests.dll --filter=Name=BuildBasicApplication
```

If the test has arguments then the `=` will not match the name. Instead use the `~`,
this does a "Contains" check for the Name.

```sh
./dotnet-local.cmd test bin\TestDebug\net7.0\Xamarin.Android.Build.Tests.dll --filter=Name~BuildBasicApplication
```

To list all the available tests use the `-lt` argument

```cmd
dotnet-local.cmd test bin\TestDebug\net7.0\Xamarin.Android.Build.Tests.dll -lt
```

# Writing Tests

How you write tests depends upon the type of test you're writing.

<a name="msbuild-integration-tests"></a>

## MSBuild Integration Tests

MSBuild Integration Tests exercise an entire MSBuild "pipeline", and are
located in
[`src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests).
These types of test *do not* run on a Device. These tests check that apps can
build and produce the correct files in the final `apk`. It is also where we add
tests for specific user reported issues, for example build errors around non
ASCII characters etc.

Any new test `class` should derive from
[`BaseTest`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Utilities/BaseTest.cs).
These base classes provide additional helper methods to create and run the unit
tests. They also contain methods to run things like `adb` commands and to auto
cleanup the unit tests. They will also capture additional things like
screenshots if a test fails.

Writing a test uses [`Xamarin.ProjectTools`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/),
which exposes a way to programmatically generate `.csproj` files as well as
other application based source code. This saves us from having to have 1000's
of `csproj` files all over the repo.

At its core you create an
[`XamarinAndroidProject`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Android/XamarinAndroidProject.cs)
instance. This can be `XamarinAndroidApplicationProject` or say `XamarinFormsApplicationProject`.

```csharp
var project = new XamarinAndroidApplicationProject ();
```

You can then add Items such as source files, images or other files. By default
it will create a simple Android App which will include one `MainActivity.cs`
and some standard resources. If you use one of the variants of the
`XamarinAndroidApplicationProject` like `XamarinFormsApplicationProject`, the
default project will contain the files needed for that variant. For example the
Xamarin.Forms one will contain XAML files for layout.

MSBuild Properties can be set via the `SetProperty()` method. This can be
done globally or for a specific Configuration. By default the project has a
`DebugConfiguration` and a `ReleaseConfiguration`.

```csharp
project.SetProperty ("MyGlobalBoolProperty", "False");
project.SetProperty (project.DebugConfiguration, "MyDebugBoolProperty", "False");
```

Once you have a project object constructed, you can make use of `ProjectBuilder`
to build the project. There are two helper methods: `CreateApkBuilder()` and
`CreateDllBuilder()` which are available in the `BaseTest` class.
These will allow you do create a builder to output an `apk` or in the base of a
`Library` project a `dll`.
You call `CreateApkBuilder()` to create the builder then pass the project to
the `Build()` method. This will build the project.
There are other methods such as `Save()` and `Install()` which can be used to
run the various underlying MSBuild targets.

*NOTE*: You should wrap your instances of a `ProjectBuilder` inside a `using`
block to ensure that the files are cleaned up correctly after the test has run.
Tests which fail will leave their files on disk to later inspection or
archiving.

```csharp
using (var builder = new CreateApkBuilder ()) {
    Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
}
```

When running under .NET, `ProjectTools` will automatically switch to using
SDK-style projects and will generate .NET based projects. When running under
`msbuild` it will generate the old style projects. This allows you do write the
same test for both types of SDK.


<a name="msbuild-task-tests"></a>

## MSBuild Task Unit Tests

MSBuild Task Unit Tests are unit tests which test a single MSBuild task in
isolation, *without* involving the MSBuild engine, `.targets` files, or
anything else.  MSBuild Task unit tests are generally in the
[`src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/)
project.

There is an implementation of the
[`IBuildEngine`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.framework.ibuildengine?view=msbuild-17-netcore)
and related interfaces in the
[`MockBuildEngine`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Utilities/MockBuildEngine.cs)
class. You can use this to mock the MSBuild runtime and test tasks directly.
You might need to create an instance of the `MockBuildEngine` per test since it
captures warnings and errors to specific collections provided to the constructor.
If you are testing if a `Task` produces a specific error it will need its own
`MockBuildEngine`, just in case the test is run in parallel.

```csharp
var engine = new MockBuildEngine (TestContext.Out);
```

Once you have a `MockBuildEngine` you can then create an instance
of your `Task` and then assign the `BuildEngine` property.

```csharp
var task = new MyTask () {
    BuildEngine = engine,
};
```

Then you can `Assert` on the `Execute()` method of the task. This will run the
task and return a `bool`.

```csharp
Assert.IsTrue (task.Execute (), "Task should succeed.");
```

NOTE: It is common practice in .NET for Android to provide a text description on
an `Assert` call. This makes it easier to track down where a particular test
is failing.

If you want to capture warnings and errors you need to provide the
`MockBuildEngine` with the appropriate arguments.

```csharp
var errors   = new List<BuildErrorEventArgs> ();
var warnings = new List<BuildWarningEventArgs> ();
var messages = new List<BuildMessageEventArgs> ();
var engine   = new MockBuildEngine (TestContext.Out, errors: errors, warnings: warnings messages: messages);
```

You can then check these collections for specific output from the `Task`.

Putting it all together

```csharp
[Test]
public void MyTaskShouldSucceedWithNoWarnings
{
    var warnings = new List<BuildWarningEventArgs> ();
    var messages = new List<BuildMessageEventArgs> ();
    var engine   = new MockBuildEngine (TestContext.Out, warnings: warnings);
    var task     = new MyTask () {
        BuildEngine = engine,
    };
    Assert.IsTrue (task.Execute (), "Task should succeed.");
    Assert.AreEqual (0, warnings.Count, $"Task should not emit any warnings, found {warnings.Count}");
}
```

Adding [`ITaskItem`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.framework.itaskitem?view=msbuild-17-netcore)
properties to the Task can be done just like setting normal properties.
This way you can test out all sorts of scenarios.

```csharp
[Test]
public void MyTaskShouldSucceedWithNoWarnings
{
    var warnings = new List<BuildWarningEventArgs> ();
    var messages = new List<BuildMessageEventArgs> ();
    var engine   = new MockBuildEngine (TestContext.Out, warnings: warnings);
    var task     = new MyTask () {
        BuildEngine = engine,
        SomeItem    = new TaskItem ("somefile.txt"),
    };
    Assert.IsTrue (task.Execute (), "Task should succeed.");
    Assert.AreEqual (0, warnings.Count, $"Task should not emit any warnings, found {warnings.Count}");
}
```

<a name="devive-integration-tests"></a>

## Device Integration Tests

Device based tests are located in
[`tests/MSBuildDeviceIntegration`](../../tests/MSBuildDeviceIntegration/MSBuildDeviceIntegration.csproj).
These work in a similar fashion to the other MSBuild related tests. The only
requirement is that they need a Device Attached.

Any new test `class` should derive from
[`DeviceTest`](../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Utilities/DeviceTest.cs).
These base classes provide additional helper methods to create and run the unit
tests. They also contain methods to run things like `adb` commands and to auto
cleanup the unit tests. They will also capture additional things like
screenshots if a test fails.

The `DeviceTest` base class provides helper methods which will allow you to run
your test application on the device. It also contains methods for capturing the
`adb logcat` output, the UI, and changing users.
You still use the various `Save()` and `Build()` methods on the `ProjectBuilder`
class to build the app, but you can also use the `Install()` method to install
the app on the device or emulator.

The `SetDefaultTargetDevice()` method on the `XamarinAndroidApplicationProject`
will set the MSBuild `AdbTarget` property from the `ADB_TARGET` environment
variable. This will ensure that the test will use the same device that the
environment wants it to use. The `ADB_TARGET` environment variable can be
useful if you are running on a system which has multiple devices attached.

In the example below the `RunProjectAndAssert()` method will call the
underlying `Run` target in MSBuild and make sure it runs successfully.
The `WaitForActivityToStart()` method is the one which monitors the
`adb logcat` output to detect when the app starts.

```csharp
[Test]
public void MyAppShouldRun ([Values (true, false)] bool isRelease)
{
    var proj = new XamarinAndroidApplicationProject () {
        IsRelease = isRelease,
    };
    proj.SetDefaultTargetDevice ();
    using (var b = CreateApkBuilder ()) {
        // Build and Install the app
        Assert.True (b.Install (proj), "Project should have installed.");
        // Run it
        RunProjectAndAssert (proj, b);
        // Wait for the app to start with a 30 second timeout
        Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
            Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
    }
}
```

If you want to check if a UI element was shown you can make use of the
`GetUI()` method. This returns an XML representation of what is one the screen
of the device at the time of the call. You can also call `ClickButton()` to
click a specific part of the screen. While the method is called `ClickButton()`
it actually sends a `tap` to the screen at a specific point.

```csharp
[Test]
public void MyAppShouldRunAndRespondToClick ()
{
    var proj = new XamarinAndroidApplicationProject ();
    proj.SetDefaultTargetDevice ();
    using (var b = CreateApkBuilder ()) {
        // Build and Install the app
        Assert.True (b.Install (proj), "Project should have installed.");
        // Run it
        RunProjectAndAssert (proj, b);
        // Wait for the app to start with a 30 second timeout
        Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
            Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
        Assert.True (ClickButton ("", "android:id/myButton", "Hello World, Click Me!"), "Button should have been clicked.");
    }
}
```

<a name="device-unit-tests"></a>

## On-Device Unit Tests

There are a category of tests which run on the device itself, these test the
runtime behaviour. These run `NUnit` tests directly on the device. Some of
these are located in the runtime itself. We build them within the repo then run
the tests on the device. They use a custom mobile version of `NUnit` called
`NUnitLite`. For the most part they are the same.

These tests are generally found in:

  * [`tests/Mono.Android-Tests`](../../tests/Mono.Android-Tests)
  * [`tests/EmbeddedDSOs/EmbeddedDSO`](../../tests/EmbeddedDSOs/EmbeddedDSO)
  * [`tests/locales/Xamarin.Android.Locale-Tests`](../../tests/locales/Xamarin.Android.Locale-Tests)

These tests are run by using the `RunTestApp` target on the appropriate project
file, which includes:

  * `tests/Mono.Android-Tests/Runtime-Microsoft.Android.Sdk/Mono.Android.NET-Tests.csproj`

For example:

```zsh
./dotnet-local.sh build -t:RunTestApp tests/Mono.Android-Tests/Runtime-Microsoft.Android.Sdk/Mono.Android.NET-Tests.csproj
```

After running the tests, a `TestResult*.xml` file will be created in the
top checkout directory containing the results of the tests.

The following is an example unit test.

```csharp
[Test]
public void ApplicationContextIsApp ()
{
    Assert.IsTrue (Application.Context is App);
    Assert.IsTrue (App.Created);
}
```

Tests in this area are usually located in a directory representing the
namespace of the API being tested.
For example the above test exists in the `Android.App` folder, since it is
testing the `Android.App.Application` class.

<a name="other-tests"></a>

## Other Tests

[`tests/CodeBehind/BuildTests/CodeBehindBuildTests.csproj`](../../tests/CodeBehind/BuildTests/CodeBehindBuildTests.csproj)
is used to test [Layout CodeBehind](../guides/LayoutCodeBehind.md).
If it builds, the test is considered successful, and is built via inclusion
in the [`Xamarin.Android-Tests.sln` project](../../Xamarin.Android-Tests.sln).
