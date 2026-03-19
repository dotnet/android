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

## Running Scripts

This repo uses `dotnet run file.cs` (.NET 10+ feature) to execute standalone C# scripts with top-level statements — no `.csproj` needed. Example:

```sh
dotnet run .github/skills/android-tools-reviewer/scripts/submit_review.cs -- arg1 arg2
```

## Android Environment Variables

Per the [official Android docs](https://developer.android.com/tools/variables#envar):

- **`ANDROID_HOME`** — the canonical variable for the Android SDK root path. Use this everywhere.
- **`ANDROID_SDK_ROOT`** — **deprecated**. Do not introduce new usages. Existing code may still read it for backward compatibility but always prefer `ANDROID_HOME`.
- **`ANDROID_USER_HOME`** — user-level config/AVD storage (defaults to `~/.android`).
- **`ANDROID_EMULATOR_HOME`** — emulator config (defaults to `$ANDROID_USER_HOME`).
- **`ANDROID_AVD_HOME`** — AVD data (defaults to `$ANDROID_USER_HOME/avd`).

When setting environment variables for SDK tools (e.g. `sdkmanager`, `avdmanager`), set `ANDROID_HOME`. The `EnvironmentVariableNames` class in this repo defines the constants.

## Conventions

- **One type per file**: each public class, struct, enum, or interface must be in its own `.cs` file named after the type (e.g. `JdkVersionInfo` → `JdkVersionInfo.cs`). Do not combine multiple top-level types in a single file.
- **Minimal public API**: prefer `internal` for new methods/classes unless they are consumed by external projects (dotnet/android, IDE extensions). Use `InternalsVisibleTo` for test access.
- **Strongly-typed APIs over strings**: when an API parameter has a finite set of valid forms (e.g. `"tcp:5000"`), use a record/enum pair (e.g. `AdbPortSpec(AdbProtocol.Tcp, 5000)`) instead of raw strings. Callers get compile-time safety, IntelliSense, and pattern matching.
- **Avoid convenience overloads**: don't add `string`, `int`, and strongly-typed overloads for the same method. Pick the strongly-typed signature and let callers construct the type. Fewer overloads = smaller API surface, fewer RS0027 suppressions, and less maintenance.
- **Include stdout in error diagnostics**: when a method captures stdout (e.g. `ListReversePortsAsync`), pass it to `ProcessUtils.ThrowIfFailed(exitCode, command, stderr, stdout)` so failure messages include all output, not just stderr.
- **Update PublicAPI files**: when adding or removing `public` API surface, update the `PublicAPI.Unshipped.txt` files under `src/Xamarin.Android.Tools.AndroidSdk/PublicAPI/net10.0/` and `src/Xamarin.Android.Tools.AndroidSdk/PublicAPI/netstandard2.0/`. New types and members go in the unshipped file. Build with `--no-incremental` and verify zero `RS0016` (missing) or `RS0017` (removed) warnings. See `PublicAPI.Shipped.txt` for the expected entry format.
- **Use `ProcessUtils`**: never use `System.Diagnostics.Process` directly. Use the existing helpers such as `ProcessUtils.CreateProcessStartInfo()`, `ProcessUtils.StartProcess()`, and `ProcessUtils.ExecuteToolAsync()` for launching external tools. This ensures consistent logging, timeout handling, and cancellation.
- **Process arguments**: use `ProcessUtils.CreateProcessStartInfo()` and pass arguments as separate strings instead of building a single arguments string yourself. `ProcessUtils` will use `ProcessStartInfo.ArgumentList` when available and fall back to `Arguments` on `netstandard2.0`.
- **Use `FileUtil`**: file operations like extraction, downloads, checksum verification, and path checks belong in `FileUtil.cs`. Don't duplicate file helpers in domain classes.
- **Concise XML docs**: omit `<summary>` tags for self-explanatory methods. Only add doc comments when the behavior is non-obvious. Avoid restating the method name.
- **`netstandard2.0` awareness**: many modern .NET APIs are unavailable or have fewer overloads on `netstandard2.0`. When unsure about API availability, search mslearn to check documentation for the target framework.
- **Format your code**: always match the existing file indentation (tabs, not spaces — see `.editorconfig`). Only format code you add or modify; never reformat existing lines.
- **No whitespace-only diffs**: before committing, run `git diff --stat` and verify only files with intentional code changes appear. If a file shows as fully rewritten (all lines removed and re-added) you have introduced line-ending or trailing-whitespace changes — revert that file with `git checkout -- <file>` and re-apply only your code change. Never commit whitespace-only or line-ending-only changes.
- **File-scoped namespaces**: all new files should use file-scoped namespaces (`namespace Foo;` instead of `namespace Foo { ... }`).
- **Static `HttpClient`**: `HttpClient` instances must be `static` to avoid socket exhaustion. See [HttpClient guidelines](https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines#recommended-use). Do not create per-instance `HttpClient` fields or dispose them in `IDisposable`.
- [Mono Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/): tabs, K&R braces, `PascalCase` public members.
- **No null-forgiving operator (`!`)**: do not use the null-forgiving operator after null checks. Instead, use C# property patterns (e.g. `if (value is { Length: > 0 } v)`) which give the compiler proper non-null flow analysis on all target frameworks including `netstandard2.0`.
- **Prefer switch expressions**: use C# switch expressions over switch statements for simple value mappings (e.g. `return state switch { "x" => A, _ => B };`). Use switch statements only when the body has side effects or complex logic.
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
