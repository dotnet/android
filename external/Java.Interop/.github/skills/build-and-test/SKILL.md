---
name: build-and-test
description: Build and test the Java.Interop repository. Use when asked to build, compile, run tests, verify changes, or check for regressions. Handles submodule initialization, build preparation, compilation, test execution, and result summarization. Also use after making code changes to validate they compile and pass tests.
---

# Build and Test

Build and test the Java.Interop .NET/JNI interop repository.

## Prerequisites

- .NET SDK (9+)
- Java Development Kit (JDK)
- Platform-specific JVM libraries

## Workflow

1. Initialize submodules (if needed)
2. Prepare the build
3. Build the solution
4. Run tests
5. Summarize results

## Step 1: Initialize Submodules

Run only if `external/xamarin-android-tools` is empty or missing:

```bash
git submodule update --init --recursive
```

## Step 2: Prepare + Build

```bash
dotnet build -t:Prepare
dotnet build Java.Interop.sln
```

If the user only wants to build (not test), stop here and report success/failure.

## Step 3: Run Tests

Run all tests:

```bash
dotnet test Java.Interop.sln
```

Run a specific test project (when the user specifies one or when iterating on a focused area):

```bash
dotnet test tests/<TestProject>/<TestProject>.csproj
```

Common test projects:
- `Java.Interop-Tests` — core JNI binding tests (largest suite)
- `Java.Interop.Export-Tests` — export attribute tests
- `Java.Interop.Dynamic-Tests` — dynamic invocation tests
- `Java.Base-Tests` — Java.Base binding tests
- `generator-Tests` — C# binding generator tests
- `Java.Interop-PerformanceTests` — performance benchmarks
- `Java.Interop.Tools.JavaCallableWrappers-Tests` — JCW generation tests

## Step 4: Summarize Results

Parse the `dotnet test` output. Extract lines matching `Passed!` or `Failed!` patterns.

Present a summary table:

| Test Assembly | Passed | Failed | Skipped |
|---|---|---|---|
| Assembly-Name | N | N | N |

**Total: X passed, Y failed, Z skipped.**

If any tests failed, show the failure details and relevant error messages.

## Handling Failures

**Build failures**: Show the full error output. Common issues:
- Missing submodules → run `git submodule update --init --recursive`
- Missing JDK → check `$JAVA_HOME` is set
- Missing JVM → check `$JdkJvmPath` in `Configuration.Override.props`

**Test failures**: Show the failing test names and assertion messages. If a specific test fails, suggest re-running just that test project for faster iteration.
