# Unit Tests


## Tools

1. [NUnit](https://github.com/nunit/nunit)
2. [XUnit](https://github.com/xunit/xunit)

## Project Test Types

1. Build Tests

    There are a number of `Build` related tests which test various aspects of the `.NET Android` build system. This includes not only MSBuild Task tests but build integration tests. Since the SDK is consumed from MSBuild, almost all of the unit tests are MSBuild related.

2. Device Tests

    Because `.NET Android` has to work on devices, we have a number of
    "Integration" tests which check to make sure a final app will function as expected on an Emulator or Device. Our CI system will
    test against an Emulator. However the system will pick up the first attached device, so developers can test one physical hardware
    if needed.

## Test Count

| Test | Count |
| :---------: | ----: |
| Test | 0 |
| Test | 100 |


## Running Tests

Running tests in the IDE is currently not supported.

After building the repo you can make use of the `dotnet-local` script
to run unit tests against the locally build SDK. The `dotnet-local` script is a wrapper around the custom `dotnet` installation which the
build downloads and installs in `bin/Debug/dotnet` or `bin/Release/dotnet` (depending on your configuration).

### MacOS/Linux

To run ALL the build tests run.

`dotnet-local.sh test bin/TestDebug/net7.0/Xamarin.Android.Build.Tests.dll --filter=Category!=DotNetIgnore`

To run ALL the supported Device Integraton tests runs.

`dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/net7.0/MSBuildDeviceIntegration.dll --filter=Category!=DotNetIgnore`

NOTE: Not all tests work under .NET Android yet. So we need to filter
them on the `DotNetIgnore` category.


### Windows

On Windows we can use the same `dotnet-local` script to run the tests
just like we do on other platforms.

`dotnet-local.cmd test bin/TestDebug/net7.0/Xamarin.Android.Build.Tests.dll --filter=Category!=DotNetIgnore`

`dotnet-local.cmd test bin/TestDebug/MSBuildDeviceIntegration/net7.0/MSBuildDeviceIntegration.dll --filter=Category!=DotNetIgnore`

## Writing Tests

This section outlines how to write the unit tests for the various parts of the SDK.
Any new test `class` should derive from `BaseTest` or in the case of Device based tests, `DeviceTest`. These base classes provide additional helper methods to
create and run the unit tests. They also projects methods to run things like
`adb` commands and to auto cleanup the unit tests. They will also capture additional
things like screenshots if a test fails.

### Task Tests

Tests which run on MSBuild Tasks are located in the `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Xamarin.Android.Build.Tests.csproj` project. They should be placed in the `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Tasks/` folder along with the other tests.

There is an implementation of the `IBuildEngine*` interfaces in the `MockBuildEngine` class. You can use this to mock the MSBuild
runtime and test tasks directly. You might need to create an instance of the `MockBuildEngine` per test since it captures warnings and errors to specific collections provided to the constructor. So if you are testing if a `Task` produces a specific error it will need its own `MockBuildEngine`. Just in case the test is run in parallel.

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

Then you can `Assert` on the `Execute` method of the task. This will run the task and return a `bool`.

```csharp
Assert.IsTrue (task.Execute (), "Task should succeed.");
```

NOTE: It is common practice in .NET Android to provide a text description on an `Assert`. This makes it easier to track down
where a particular test is failing.

If you want to capture warnings and errors you need to provide the `MockBuildEngine` with the appropriate arguments.

```csharp
var errors = new List<BuildErrorEventArgs>;
var warnings = new List<BuildWarningEventArgs>;
var messages = new List<BuildMessageEventArgs>;
var engine = new MockBuildEngine (TestContext.Out, errors: errors, warnings: warnings messages:messages);
```

You can then check these collections for specific output from the `Task`.

Putting it all together

```csharp

[Test]
public void MyTaskShouldSucceedWithNoWarnings
{
    var warnings = new List<BuildWarningEventArgs>;
    var messages = new List<BuildMessageEventArgs>;
    var engine = new MockBuildEngine (TestContext.Out, warnings: warnings);
    var task = new MyTask () {
        BuildEngine = engine,
    };
    Assert.IsTrue (task.Execute (), "Task should succeed.");
    Assert.AreEqual (0, warnings.Count, $"Task should not emit any warnings, found {warnings.Count}");
}
```

### Build Tests

Tests which need to test the SDK integration are located in the `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Xamarin.Android.Build.Tests.csproj` project. These types of test do NOT run on a Device. Device tests are slow and expensive to run (time wise). Generally these tests check that apps can build and produce the correct files in the final `apk`. It is also where we add tests for specific user reported issues, for example build errors around non ASCII characters etc.

There are other build tests which test other aspects of the SDK. Examples are
`tests/CodeBehind/BuildTests/CodeBehindBuildTests.csproj`

Writing a test makes use of the `src/Xamarin.Android.Build.Tasks/Tests/Xamarin.ProjectTools/Xamarin.ProjectTools.csproj` API. This api exposes a way to programmatically generating csproj files as well as other application based source code. This saves us from having to have 1000's of csproj files all over the repo.

At its core you create an `XamarinAndroidProject` instance. This can be
`XamarinAndroidApplicationProject` or say `XamarinFormsApplicationProject`.

```csharp
var project = new XamarinAndroidApplicationProject ();
```

You can then Add Items such as source files, images or other files. By default it
will create a simple Android App which will include one `MainActivity.cs` and some
standard resources. Properties can be set via the `SetProperty` method. This can be
done globally or for a specific Configuration.

```csharp
project.SetProperty ("MyGlobalBoolProperty", "False");
project.SetProperty (project.DebugConfiguration, "MyDebugBoolProperty", "False");
```

Once you have a project object constructed, you can make use of the `ProjectBuilder`
to build the project. There are two helper methods `CreateApkBuilder` and `CreateDllBuilder` which are available. These will allow you do create a builder to
output an `apk` or in the base of a `Library` project a `dll`.
You call `CreateApkBuilder` to create the builder then pass the project to the `Build`
method. This will build the project. There are other methods such as `Save` and `Install` which can be used to run the various underlying MSBuild targets.
NOTE: You should wrap your instances of a `ProjectBuilder` inside a `using` to make sure that the files are cleaned up correctly after the test has run. Tests which fail
will leave their files on disk to later inspection or archiving.

```csharp
using (var builder = new CreateApkBuilder ()) {
    Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
}
```

When running under .NET the ProjectTools will automatically switch to using SDK style
projects and will generate .NET based projects. When running under `msbuild` it will
generate the old style projects. This allows you do write the same test for both types
of SDK.




### Device Tests

Device based tests are located in `tests/MSBuildDeviceIntegration/MSBuildDeviceIntegration.csproj`. These work in a similar fashion to the other MSBuild
related tests. The only requirement is that they need a Device Attached.

### On Device Unit Tests

There are a category of tests which run on the device itself, these tests the runtime
behaviour. These run `NUnit` and `XUnit` tests directly on the device. Some of these are located in the runtime itself. We build them within the repo then run the tests on
the device.
