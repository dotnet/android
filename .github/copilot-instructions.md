# Instructions for AIs

**.NET for Android** (formerly Xamarin.Android) - Open-source Android development bindings for .NET languages. `main` branch targets **.NET 12**.

## Architecture
- `src/Mono.Android/` - Android SDK bindings in C#
- `src/Xamarin.Android.Build.Tasks/` - MSBuild tasks for Android apps  
- `src/native/` - Native runtime (MonoVM/CoreCLR/NativeAOT)
- `src/Java.Interop/`, `src/Java.Interop.Tools.*/` - JNI bindings and Java-to-.NET interop tooling
- `src/Microsoft.Android.Build.BaseTasks/` - Shared MSBuild task infrastructure: `AndroidTask`, `AsyncTask`, NRT extensions, and common file/process utilities
- `src/Xamarin.Android.Tools.AndroidSdk/` - Shared Android SDK/JDK discovery, `AdbRunner`, `EmulatorRunner`, and SDK info utilities
- `tests/` - NUnit and MSTest fixtures, integration tests, device tests

**Build System:** MSBuild + .NET Arcade SDK + CMake (native)

## Essential Commands
- **Build the local SDK:** `make prepare && make leeroy` (not `make all`) or `build.cmd`; see the `local-build` skill
- **Test with local build:** `dotnet-local.sh`/`dotnet-local.cmd` (prefers `bin/Release` when present)
- **Run tests:** see the `tests` skill for standalone, full-build, and device commands.

Shared Android tooling targets `netstandard2.0` and/or modern .NET; check APIs against all target frameworks. Prefer `ANDROID_HOME` for new SDK environment handling; retain `ANDROID_SDK_ROOT` only for compatibility.

## Working Principles

- **Check for existing work first:** search PRs and branches (`gh pr list --repo dotnet/android --state all --search "<keywords>"`, `git log origin/main --grep "<keywords>"`) and prior sessions; extend rather than redo, including before fanning out.
- **Reuse before inventing:** look for an existing MSBuild, SDK, `global.json`, or Arcade mechanism, in-repo helper, or CI template first.
- **Keep it simple:** smallest change; prefer deleting over layering; simplifications end with less code; no fallbacks, options, or abstractions without a caller.
- **Look up dependencies, don't guess URLs:** for .NET source matching our pins, search the VMR at the `Sha` in `eng/Version.Details.xml` (`gh search code --repo dotnet/dotnet "<symbol>"`, `gh api "repos/dotnet/dotnet/contents/<path>?ref=<sha>"`); for package versions, `dotnet package search <id> --exact-match --prerelease --source <feed from NuGet.config>`; for MS Learn, `microsoft_docs_search`.
- **Ask before adding** workflows, scripts, `.editorconfig`/`Directory.*` files, tests for build scripts, or build-time-generated files.
- **Measure performance claims** before and after with the same configuration; drop changes without a measured win.
- **Validate proportionally:** use the smallest sufficient validation (table in the `tests` skill).

## Critical Rules

**Never use `git commit --amend`:** Always create new commits. The user will squash or fixup as needed.

**PR descriptions and commit messages:** PRs are squash-merged; follow the `pull-request` skill.

Reference official Android documentation where helpful:
* [Android Developer Guide](https://developer.android.com/develop)
* [Android API Reference](https://developer.android.com/reference)
* [Android `aapt2` Documentation](https://developer.android.com/tools/aapt2)

**Only modify the main English `*.resx` files** (e.g., `Resources.resx`)

**Never modify non-English localization files:** `*.lcl` files in `Localize/loc/` or non-English `*.resx` files are auto-generated.

**Use Microsoft docs:** Search MS Learn before making .NET, Windows, or Microsoft features, APIs, or integrations. Use the `microsoft_docs_search` tool.

**MSBuild Tasks:** ALL NEW MSBuild tasks must support multithreaded execution and declare `[MSBuildMultiThreadableTask]` directly on each concrete task class. The attribute is not inherited; implementing `IMultiThreadableTask` alone does not enable in-process MT execution.

- Use `MultithreadedAndroidTask` for new managed tasks, or `MultithreadedAndroidToolTask` for tasks that run an external tool. Do not derive new tasks from `AndroidTask`, `AsyncTask`, or `AndroidToolTask`, whose constructors capture the process working directory.
- Use the engine-supplied `TaskEnvironment` for project-relative paths, environment access, and child processes. Keep constructors free of process-state dependencies; initialize `TaskEnvironment` to `Fallback` when implementing the interface directly so direct instantiation also works.
- Before adding wrappers or workarounds, check the public APIs in the pinned dependency's reference assembly. XML documentation can include internal methods.
- `TaskEnvironment.GetAbsolutePath (path)` roots a path but does not collapse `.` or `..`. When canonicalization is needed, call `.GetCanonicalForm ()` on the returned `AbsolutePath`; use `.Value` for string operations. Do not construct `FileInfo` or `DirectoryInfo` just to normalize paths.
- Review helpers, exception paths, and static state for thread safety. Do not mutate shared process state or use process isolation as a substitute for MT support. For asynchronous tasks, preserve the `Yield()`/`Reacquire()` contract with `try`/`finally`.
- Use `XA####` error codes and test in isolation. Validate ordinary and strict `-m -mt -nr:false -warnaserror:MSB4286,MSB4287,MSB4288` builds. See [MSBuild best practices](../Documentation/guides/MSBuildBestPractices.md#multithreaded-tasks).

**Internal build `<UsingTask/>` elements:** For `xa-prep-tasks` and `BootstrapTasks` (internal build-time tasks, not shipped to customers), always use `TaskFactory="TaskHostFactory"` and `Runtime="NET"` attributes on `<UsingTask/>` elements. This runs the task in a separate process to avoid Windows file locking issues and ensures the task runs on .NET (even when MSBuild.exe in Visual Studio uses .NET Framework). Example:

```xml
<UsingTask AssemblyFile="$(BootstrapTasksAssembly)" TaskName="Xamarin.Android.Tools.BootstrapTasks.MyTask" TaskFactory="TaskHostFactory" Runtime="NET" />
<UsingTask AssemblyFile="$(PrepTasksAssembly)" TaskName="Xamarin.Android.BuildTools.PrepTasks.MyTask" TaskFactory="TaskHostFactory" Runtime="NET" />
```

**Shipped `<UsingTask/>` elements:** Omit `TaskFactory="TaskHostFactory"` and `Runtime="NET"` on `netstandard2.0` shipped task assemblies (`Xamarin.Android.Build.Tasks.dll`, `$(_XamarinAndroidBuildTasksAssembly)`, `Xamarin.Android.Build.Debugging.Tasks.dll`, `Xamarin.Installer.Build.Tasks.dll`) so they do not explicitly force a task host on either .NET Framework or .NET MSBuild. For modern-.NET shipped tasks in `$(_MicrosoftAndroidBuildTasksAssembly)` (`Microsoft.Android.Build.Tasks.dll`), declare the conditioned pair `Runtime="NET" TaskFactory="TaskHostFactory" Condition=" '$(MSBuildRuntimeType)' == 'Full' "` and `Runtime="NET" Condition=" '$(MSBuildRuntimeType)' == 'Core' "` so `.NET` MSBuild does not force a task host while .NET Framework MSBuild (e.g. `MSBuild.exe` in Visual Studio) uses the .NET task host; never add an unconditioned `TaskFactory="TaskHostFactory"` to shipped tasks.

**API Bindings:** Use `[Register]` attributes, follow `Android.*` namespace patterns.

**Native Code:** Use CMake, handle multiple ABIs (arm64-v8a, armeabi-v7a, x86_64, x86).

## Unsafe C# and Memory Safety

Follow [Unsafe code best practices](https://learn.microsoft.com/dotnet/standard/unsafe-code/best-practices) for affected unsafe code; do not scan the repository unless asked. For caller contracts and safety documentation, see [Improving C# memory safety](https://devblogs.microsoft.com/dotnet/improving-csharp-memory-safety/).

- Prefer `unsafe (expression)` for single value-producing operations, including getters, ref returns and `using` acquisition. Keep managed comparisons/hash/formatting outside; retain blocks for pinning, void calls and multiple statements.
- Under the new rules, caller-unsafe members still need body contexts. Keep `// SAFETY:` proofs adjacent; use multiline `/// <safety>` for caller obligations.
- Respect producer/consumer language modes, including generated code. Inline expressions need pinned-compiler preview; never enable preview just for syntax. Preserve behavior and lifetimes.
- **JNI peer lifetimes (`GC.KeepAlive`):** Copying `.Handle`/`.PeerReference` or using `JValue`/`JniArgumentValue` does not root the peer, and on CoreCLR/NativeAOT `JavaMarshalGCBridge` deletes/replaces grefs of managed-unreachable peers even if Java still references them. Keep every borrowed owner alive past JNI/P/Invoke calls; see the [JNI Peer Lifetimes checklist](skills/code-review/references/interop-rules.md#jni-peer-lifetimes-gckeepalive-review-checklist).

## Nullable Reference Types

Enable NRT for new code. Don't convert existing code to NRT unless asked. When enabling:

- Add `#nullable enable` as the first line of new files unless the project enables `Nullable`. For new code in existing files, enable it locally without changing the surrounding nullable context.
- Never use `!` (null-forgiving), in tests too: make `[SetUp]` fields nullable, or copy to a local after `Assert.IsNotNull`.
- Check `null` at entry points: `ArgumentNullException.ThrowIfNull` on .NET 10+, `throw new ArgumentNullException (nameof (x))` on `netstandard2.0`.
- MSBuild tasks: `[Required]` properties are non-nullable with a default (`= ""`, `= []`); other and `[Output]` properties are nullable.
- On `netstandard2.0`, use the `str.IsNullOrEmpty ()`/`str.IsNullOrWhiteSpace ()` extensions instead of the static `string` methods.

## Formatting

C# code uses tabs (not spaces) and Mono style (`.editorconfig`):
- **NEVER** use `!` (null-forgiving operator) in C# code. Always refactor to avoid it, e.g. by having helper methods return non-null types or by checking for null explicitly.
- Preserve existing formatting and comments
- Space before `(` and `[`: `Foo ()`, `array [0]`
- Use `""` not `string.Empty`, `[]` not `Array.Empty<T>()`
- Prefer C# raw string literals (`"""`) for multi-line strings instead of `@""` with escaped quotes
- Minimal diffs - don't leave random empty lines
- Do NOT use `#region` or `#endregion`
- Put opening braces on the same line, including `} catch (...) {`.

## Testing

### Validate locally before pushing

Build and test code changes locally before pushing or opening a PR; CI is not the first validation. Building the full SDK (`make prepare && make leeroy`, or `build.cmd`) is normal work: don't ask to skip it because of presumed build time. Follow the `tests` skill, rebuild the local SDK when affected, and test affected runtimes, ABIs, and build properties on-device for runtime, JNI, and native changes. Don't push known regressions. If blocked, report the exact command, failure, and what remains unvalidated. Report the commands and outcomes in the handoff. Documentation-only changes need no build.

### Adding tests

Choose tests for unique protection of meaningful behavior or regressions, not test count. Reuse existing coverage; avoid duplicates, trivial implementation assertions, tests that only check removed code, and nondeterministic GC-timing tests. A refactor can need characterization tests when behavior is otherwise unprotected.

**Modifying project files in tests:** Update the in-memory `Xamarin.ProjectTools` project, call `proj.Touch ("MainActivity.cs")`, then `builder.Save (proj, doNotCleanupOnUpdate: true, saveProject: false)`; don't write project sources directly with `File.WriteAllText()`.

## Error Patterns
- **MSBuild Errors:** `XA####` (errors), `XA####` (warnings), `APT####` (Android tools)
- **Error messages:** Must come from `Properties.Resources` (e.g., `Properties.Resources.XA0143`) for localization support. Add new messages to the English `Resources.resx` file.
- **New warning/error codes:** Use `Log.LogCodedWarning("XA####", ...)` / `Log.LogCodedError("XA####", ...)` (not uncoded `LogWarning(...)`) and add/update docs in `Documentation/docs-mobile/messages/xa####.md`, `Documentation/docs-mobile/messages/index.md`, and `Documentation/docs-mobile/TOC.yml`.
- **Error code lifecycle:** When removing functionality that used an `XA####` code, either repurpose the code or remove it from `Resources.resx` and `Resources.Designer.cs`. Don't leave orphaned codes.
- **Logging in `AsyncTask`:** Use the thread-safe helpers (`LogCodedError()`, `LogMessage()`, `LogCodedWarning()`, `LogDebugMessage()`) instead of `Log.*`. The `Log` property is marked `[Obsolete]` on `AsyncTask` because calling `Log.LogMessage` directly from a background thread can hang Visual Studio.

## CI / Build Investigation

**dotnet/android PR validation runs on the public Azure DevOps `dotnet-android` pipeline on `dnceng-public`, not GitHub Actions.** When a user asks about CI status, CI failures, why a PR is blocked, or build errors:

1. **ALWAYS invoke the `ci-status` skill first.** The pipeline surfaces as ~39 `dotnet-android (...)` GitHub checks, but the skill adds build progress, ETA, per-stage failures, and failed-test names that `gh pr checks` alone doesn't give you.
2. The skill auto-detects the current PR from the git branch when no PR number is given.
3. For deep .binlog analysis, use the `azdo-build-investigator` skill.
4. Only after the skill confirms no Azure DevOps failures should you report CI as passing.

## Investigation & Debugging Practices

When diagnosing runtime, build, or test failures, follow these practices. They exist because the .NET ↔ JNI ↔ C++ ↔ generated-native stack is loosely coupled and static reasoning alone is unreliable.

- **Reproduce CI failures locally — do not iterate through CI.** Use the `tests` skill for the same commands CI runs (including `Mono.Android.NET-Tests` on device).

- **Diagnose stale outputs before cleanup.** Inspect the binlog and resolved SDK paths; clean only affected outputs you own. See the `local-build` skill.

- **Verify uncertain runtime paths** with existing logs or targeted instrumentation; rebuild only when needed.

- **Decompile the produced `.dll` before blaming runtime.** Use `ilspycmd` or `ildasm` to inspect the actual generated IL/metadata. A missing attribute or misnamed type in generator output cascades into opaque runtime failures.

- **Silent `am instrument`:** Check `adb logcat -d | grep -E 'FATAL|tombstone|signal'` for a native crash before assuming a hang.

- **Confirm the runtime that ran.** `PublishAot=true` in Debug still uses CoreCLR unless `Optimize=true` or publishing; check `_AndroidRuntime` before claiming NativeAOT coverage.

## Troubleshooting
- **Build:** See the `local-build` skill for SDK/NDK paths, stalls, SourceLink warnings, and disk space
- **MSBuild:** Test in isolation, validate inputs
- **Device:** Use update directories for rapid Debug iteration; see the `android-devices` skill
- **Performance:** See `../Documentation/guides/profiling.md` and `../Documentation/guides/tracing.md`
