# Instructions for AIs

**.NET for Android** (formerly Xamarin.Android) - Open-source Android development bindings for .NET languages. `main` branch targets **.NET 11**.

## Architecture
- `src/Mono.Android/` - Android SDK bindings in C#
- `src/Xamarin.Android.Build.Tasks/` - MSBuild tasks for Android apps  
- `src/native/` - Native runtime (MonoVM/CoreCLR/NativeAOT)
- `external/Java.Interop/` - JNI bindings and Java-to-.NET interop
- `external/xamarin-android-tools/` - Shared SDK tooling: `AndroidTask`/`AsyncTask` base classes, `AdbRunner`, `EmulatorRunner`, NRT extensions (`IsNullOrEmpty()`, `IsNullOrWhiteSpace()`), `CreateTaskLogger`, and SDK info utilities
- `tests/` - NUnit tests, integration tests, device tests

**Build System:** MSBuild + .NET Arcade SDK + CMake (native)

## Essential Commands
- **Build:** `./build.sh` or `build.cmd`
- **Test with local build:** `dotnet-local.sh`/`dotnet-local.cmd` 
- **Run tests:** `dotnet-local.cmd test bin/TestDebug/net9.0/Xamarin.Android.Build.Tests.dll --filter Name~TestName`
- **Device tests:** `dotnet-local.cmd test bin/TestDebug/MSBuildDeviceIntegration/net9.0/MSBuildDeviceIntegration.dll`

## Critical Rules

**Never use `git commit --amend`:** Always create new commits. The user will squash or fixup as needed.

Reference official Android documentation where helpful:
* [Android Developer Guide](https://developer.android.com/develop)
* [Android API Reference](https://developer.android.com/reference)
* [Android `aapt2` Documentation](https://developer.android.com/tools/aapt2)

**Only modify the main English `*.resx` files** (e.g., `Resources.resx`)

**Never modify non-English localization files:** `*.lcl` files in `Localize/loc/` or non-English `*.resx` files are auto-generated.

**Use Microsoft docs:** Search MS Learn before making .NET, Windows, or Microsoft features, APIs, or integrations. Use the `microsoft_docs_search` tool.

**MSBuild Tasks:** Extend `AndroidTask` base class, use `XA####` error codes, test in isolation. Use `AsyncTask` for tasks that need `async`/`await` — it handles `Yield()`, `try`/`finally`, and `Reacquire()` automatically.

**Internal build `<UsingTask/>` elements:** For `xa-prep-tasks` and `BootstrapTasks` (internal build-time tasks, not shipped to customers), always use `TaskFactory="TaskHostFactory"` and `Runtime="NET"` attributes on `<UsingTask/>` elements. This runs the task in a separate process to avoid Windows file locking issues and ensures the task runs on .NET (even when MSBuild.exe in Visual Studio uses .NET Framework). Example:

```xml
<UsingTask AssemblyFile="$(BootstrapTasksAssembly)" TaskName="Xamarin.Android.Tools.BootstrapTasks.MyTask" TaskFactory="TaskHostFactory" Runtime="NET" />
<UsingTask AssemblyFile="$(PrepTasksAssembly)" TaskName="Xamarin.Android.BuildTools.PrepTasks.MyTask" TaskFactory="TaskHostFactory" Runtime="NET" />
```

**Do NOT** use `TaskFactory="TaskHostFactory"` or `Runtime="NET"` on `<UsingTask/>` elements shipped in the product (e.g., in `Xamarin.Android.Common.targets` or `Microsoft.Android.Sdk/*.targets`), as it could negatively impact customer builds.

**API Bindings:** Use `[Register]` attributes, follow `Android.*` namespace patterns.

**Native Code:** Use CMake, handle multiple ABIs (arm64-v8a, armeabi-v7a, x86_64, x86).

## Nullable Reference Types

When opting C# code into nullable reference types:

* Only make the following changes when asked to do so.

* Add `#nullable enable` at the top of the file without any preceding blank lines.

* Don't *ever* use `!` (null-forgiving operator) to handle `null`! Always check for null explicitly and throw appropriate exceptions.

* **In test code**, avoid `!` too. Common workarounds:
  - `[SetUp]`-initialized fields: declare as nullable (`MockBuildEngine? engine;`) instead of `MockBuildEngine engine = null!;`
  - After `Assert.IsNotNull`: extract into a local variable (`var opts = task.Options; Assert.IsNotNull (opts); opts.Foo...`) instead of using `task.Options!.Foo`

* Declare variables non-nullable, and check for `null` at entry points.

* Use `throw new ArgumentNullException (nameof (parameter))` in `netstandard2.0` projects.

* Use `ArgumentNullException.ThrowIfNull (parameter)` in Android projects that will be .NET 10+.

* `[Required]` properties in MSBuild task classes should always be non-nullable with a default value.

* Non-`[Required]` properties should be nullable and have null-checks in C# code using them.

* For MSBuild task properties like:

```csharp
public string NonRequiredProperty { get; set; }
public ITaskItem [] NonRequiredItemGroup { get; set; }

[Output]
public string OutputProperty { get; set; }
[Output]
public ITaskItem [] OutputItemGroup { get; set; }

[Required]
public string RequiredProperty { get; set; }
[Required]
public ITaskItem [] RequiredItemGroup { get; set; }
```

Fix them such as:

```csharp
public string? NonRequiredProperty { get; set; }
public ITaskItem []? NonRequiredItemGroup { get; set; }

[Output]
public string? OutputProperty { get; set; }
[Output]
public ITaskItem []? OutputItemGroup { get; set; }

[Required]
public string RequiredProperty { get; set; } = "";
[Required]
public ITaskItem [] RequiredItemGroup { get; set; } = [];
```

If you see a `string.IsNullOrEmpty()` check:

```csharp
if (!string.IsNullOrEmpty (NonRequiredProperty)) {
    // Code here
}
```

Convert this to use the extension method:

```csharp
if (!NonRequiredProperty.IsNullOrEmpty ()) {
    // Code here
}
```

If you see a `string.IsNullOrWhiteSpace()` check:

```csharp
if (!string.IsNullOrWhiteSpace (UncompressedFileExtensions)) {
    foreach (var ext in UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
        // Code here
    }
}
```

Convert this to use the extension method:

```csharp
if (!UncompressedFileExtensions.IsNullOrWhiteSpace ()) {
    foreach (var ext in UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
        // Code here
    }
}
```

## Formatting

C# code uses tabs (not spaces) and Mono style (`.editorconfig`):
- **NEVER** use `!` (null-forgiving operator) in C# code. Always refactor to avoid it, e.g. by having helper methods return non-null types or by checking for null explicitly.
- Preserve existing formatting and comments
- Space before `(` and `[`: `Foo ()`, `array [0]`
- Use `""` not `string.Empty`, `[]` not `Array.Empty<T>()`
- Prefer C# raw string literals (`"""`) for multi-line strings instead of `@""` with escaped quotes
- Minimal diffs - don't leave random empty lines
- Do NOT use `#region` or `#endregion`

```csharp
Foo ();
Bar (1, 2, "test");
myarray [0] = 1;

if (someValue) {
    // Code here
}

try {
    // Code here
} catch (Exception e) {
    // Code here
}
```

## Testing

**Modifying project files in tests:** Never use `File.WriteAllText()` directly to update project source files. Instead, use the `Xamarin.ProjectTools` infrastructure:

```csharp
// 1. Update the in-memory content
proj.MainActivity = proj.MainActivity.Replace ("old text", "new text");
// 2. Bump the timestamp so UpdateProjectFiles knows it changed
proj.Touch ("MainActivity.cs");
// 3. Write to disk (doNotCleanupOnUpdate preserves other files, saveProject: false skips .csproj regeneration)
builder.Save (proj, doNotCleanupOnUpdate: true, saveProject: false);
```

This pattern ensures proper encoding, timestamps, and file attributes are handled correctly. The `Touch` + `Save` pattern is used throughout the test suite for incremental builds and file modifications.

## Error Patterns
- **MSBuild Errors:** `XA####` (errors), `XA####` (warnings), `APT####` (Android tools)
- **Error messages:** Must come from `Properties.Resources` (e.g., `Properties.Resources.XA0143`) for localization support. Add new messages to the English `Resources.resx` file.
- **Error code lifecycle:** When removing functionality that used an `XA####` code, either repurpose the code or remove it from `Resources.resx` and `Resources.Designer.cs`. Don't leave orphaned codes.
- **Logging in `AsyncTask`:** Use the thread-safe helpers (`LogCodedError()`, `LogMessage()`, `LogCodedWarning()`, `LogDebugMessage()`) instead of `Log.*`. The `Log` property is marked `[Obsolete]` on `AsyncTask` because calling `Log.LogMessage` directly from a background thread can hang Visual Studio.

## CI / Build Investigation

**dotnet/android's primary CI runs on Azure DevOps (internal), not GitHub Actions.** When a user asks about CI status, CI failures, why a PR is blocked, or build errors:

1. **ALWAYS invoke the `ci-status` skill first** — do NOT rely on `gh pr checks` alone. GitHub checks may all show ✅ while the internal Azure DevOps build is failing.
2. The skill auto-detects the current PR from the git branch when no PR number is given.
3. For deep .binlog analysis, use the `azdo-build-investigator` skill.
4. Only after the skill confirms no Azure DevOps failures should you report CI as passing.

## Troubleshooting
- **Build:** Clean `bin/`+`obj/`, check Android SDK/NDK, `make clean`
- **MSBuild:** Test in isolation, validate inputs
- **Device:** Use update directories for rapid Debug iteration
- **Performance:** See `../Documentation/guides/profiling.md` and `../Documentation/guides/tracing.md`
