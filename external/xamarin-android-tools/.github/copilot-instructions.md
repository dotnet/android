# Copilot Instructions for android-tools

Shared .NET libraries for Android SDK/JDK discovery and MSBuild task infrastructure, consumed by [dotnet/android](https://github.com/dotnet/android) and IDE extensions.

## Architecture

Two independent libraries (neither references the other):

- **`Xamarin.Android.Tools.AndroidSdk`** — SDK/NDK/JDK path discovery, Android version management, manifest parsing. Multi-targets `netstandard2.0`+`net10.0` (trimming/AOT on modern TFM). Entry: `AndroidSdkInfo` → `AndroidSdkWindows`/`AndroidSdkUnix` via `OS.IsWindows`.
- **`Microsoft.Android.Build.BaseTasks`** — MSBuild task bases and build utilities. `netstandard2.0` only. `RootNamespace` = `Microsoft.Android.Build.Tasks` (differs from project name).

**Patterns:**
- **Platform polymorphism**: `AndroidSdkBase` → `AndroidSdkWindows`/`AndroidSdkUnix` (Template Method). JDK: vendor classes inherit `JdkLocations` partial, aggregated by priority in `JdkInfo.GetKnownSystemJdkInfos()`. Platform files: `Jdks/JdkLocations.{Windows,MacOS}.cs`, `Sdks/AndroidSdk{Windows,Unix}.cs`.
- **Task base types**: `AndroidTask` (common `Task`-based MSBuild tasks), `AndroidToolTask` (`ToolTask`-based wrappers for external tools), and `AsyncTask` (long-running, UI-safe `Task`-based tasks). All use `UnhandledExceptionLogger` for XA error codes.
- **Incremental builds**: `Files.CopyIf*Changed()` skips unchanged writes. `ObjectPool<T>`/`MemoryStreamPool` reduces GC. `JdkInfo` uses `Lazy<T>` for expensive parsing.

## Build & Test

```sh
dotnet build Xamarin.Android.Tools.sln
dotnet test tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj
dotnet test tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj
```

Output: `bin\$(Configuration)\` (redistributables), `bin\Test$(Configuration)\` (tests). `$(DotNetTargetFrameworkVersion)` = `10.0` in `Directory.Build.props`. Versioning: `nuget.version` has `major.minor`; patch = git commit count since file changed.

## Conventions

- [Mono Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/): tabs, K&R braces, `PascalCase` public members.
- Nullable enabled in `AndroidSdk`. `NullableAttributes.cs` excluded on `net10.0+`.
- Strong-named via `product.snk`. In the AndroidSdk project, tests use `InternalsVisibleTo` with full public key (`Properties/AssemblyInfo.cs`).
- Assembly names support `$(VendorPrefix)`/`$(VendorSuffix)` for branding forks.
- `.resx` localization in multiple languages via OneLocBuild (`Localize/`). Do not hand-edit satellite `.resx`.

## Tests

NUnit 3 (`[TestFixture]`, `[Test]`, `[TestCase]`). Tests create isolated temp dirs with faux JDK/SDK structures (`.bat` on Windows, shell scripts on Unix), cleaned in teardown. `AndroidSdk-Tests`: `[OneTimeSetUp]`/`[OneTimeTearDown]`. `BaseTasks-Tests`: imports `MSBuildReferences.projitems` for MSBuild types.

## Key Files

- **SDK discovery**: `AndroidSdkInfo.cs`, `Sdks/AndroidSdk{Windows,Unix}.cs`
- **JDK discovery**: `JdkInfo.cs`, `Jdks/` directory
- **Android versions**: `AndroidVersions.cs` (hardcoded `KnownVersions` table)
- **MSBuild tasks**: `AndroidTask.cs`, `AsyncTask.cs`, `Files.cs`
- **Build config**: `Directory.Build.props`, `Directory.Build.targets`, `nuget.version`

## Adding Android API Levels

Update `KnownVersions` in `AndroidVersions.cs` — add tuple with ApiLevel, Id, CodeName, OSVersion, TargetFrameworkVersion, Stable flag.
