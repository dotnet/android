# .NET for Android Review Rules

Distilled from past code reviews by senior maintainers of dotnet/android, the
conventions in [copilot-instructions.md](../../../.github/copilot-instructions.md),
and [MSBuildBestPractices.md](../../../Documentation/guides/MSBuildBestPractices.md).

---

## 1. MSBuild Task Conventions (C#)

All build tasks ship to customers and run inside MSBuild. Getting them wrong
causes broken builds for every .NET Android developer.

| Check | What to look for |
|-------|-----------------|
| **Inherit from `AndroidTask`** | Every MSBuild task must extend `AndroidTask` (from `Microsoft.Android.Build.BaseTasks`). It must implement `TaskPrefix` (a short string for error codes) and `RunTask()`. Do not inherit directly from `Microsoft.Build.Utilities.Task`. Unhandled exceptions are automatically converted to proper error codes. |
| **Use `AsyncTask` for background work** | Tasks that need `async`/`await` should extend `AsyncTask` and override `RunTaskAsync()`. It handles `Yield()`, `try`/`finally`, and `Reacquire()` automatically. Use `AsyncTask.Log*` helpers for logging from the background thread — calling `Log.LogMessage` directly can cause IDE hangs. Use full paths on background threads (`Environment.CurrentDirectory` may differ if the task is on another MSBuild node). Leverage the `WhenAll` extension for parallel work over `ITaskItem[]`. |
| **Return `!Log.HasLoggedErrors`** | `RunTask()` must return `!Log.HasLoggedErrors`. Do not return `true`/`false` directly — it skips the centralized error-tracking mechanism. |
| **Use `Log.LogCoded*` methods** | Errors and warnings must use `Log.LogCodedError("XA####", …)` or `Log.LogCodedWarning("XA####", …)` — never bare `Log.LogError` without a code. Error messages should come from `Properties.Resources`. |
| **XA error codes** | Error codes follow `XA####` (4+ digits). New codes must not collide with existing ones. Check `Properties/Resources.cs.resx` for used codes. |
| **`[Required]` properties** | `[Required]` properties must be non-nullable with a default: `public string Foo { get; set; } = "";` or `public ITaskItem[] Bar { get; set; } = [];`. Non-`[Required]` and `[Output]` properties must be nullable (`string?`, `ITaskItem[]?`). |
| **`UsingTask` for internal tasks** | `<UsingTask/>` elements for `xa-prep-tasks` and `BootstrapTasks` (internal, not shipped) must use `TaskFactory="TaskHostFactory"` and `Runtime="NET"`. Do NOT add these attributes to shipped task definitions in `Xamarin.Android.Common.targets` or `Microsoft.Android.Sdk/*.targets`. |
| **Caching with `RegisterTaskObject`** | Use `BuildEngine4.RegisterTaskObject()` (via the `RegisterTaskObjectAssemblyLocal()` extension method) instead of `static` variables for sharing data between tasks or across builds. Use `as` for casts to avoid `InvalidCastException`. Cache keys should include context that invalidates properly (device target, file path, version). Cache primitive/small values only. |

---

## 2. MSBuild Targets & XML

Targets define the build pipeline. Mistakes here break incremental builds,
cause performance regressions, or silently delete files.

| Check | What to look for |
|-------|-----------------|
| **Underscore prefix for private names** | Internal targets, properties, and item groups must be prefixed with `_` (e.g., `_CompileJava`, `$(_JarFile)`, `@(_JavaFiles)`). MSBuild has no visibility — the underscore signals "we might rename this." Public-facing properties should be prefixed with `Android` (e.g., `$(AndroidEnableProguard)`). |
| **Incremental builds (`Inputs`/`Outputs`)** | Every target that *writes files* must have `Inputs` and `Outputs` so MSBuild can skip it when nothing changed. Targets that only read files, set properties, or populate item groups do NOT need them. |
| **Stamp files** | When outputs aren't known ahead of time, use a stamp file in `$(_AndroidStampDirectory)` named after the target (e.g., `$(_AndroidStampDirectory)_ResolveLibraryProjectImports.stamp`). Create it with `<Touch Files="..." AlwaysCreate="True" />`. |
| **`FileWrites` for intermediate files** | Intermediate files must be added to `@(FileWrites)` so `IncrementalClean` doesn't delete them. Use an `<ItemGroup>` block inside the target (it evaluates even when the target is skipped). Do NOT use `<Output TaskParameter="TouchedFiles" ItemName="FileWrites" />` — it won't run when the target is skipped, so `IncrementalClean` will delete the stamp and break incrementality. Stamp files in `$(_AndroidStampDirectory)` are already handled by `_AddFilesToFileWrites`. |
| **Don't duplicate item group transforms** | If a target uses the same transform (e.g., `@(Files->'$(Dir)%(Filename)%(Extension)')`) more than once, compute it into a local item group first and reuse it. Duplicated transforms allocate the same array twice. |
| **Use `->Count()` for empty checks** | Prefer `'@(Items->Count())' != '0'` over `'@(Items)' != ''`. The latter does a string join of all items, producing enormous log messages. `->Count()` returns `0` even for non-existent item groups. |
| **Avoid `BeforeTargets`/`AfterTargets`** | Prefer `$(XDependsOn)` properties (e.g., `$(BuildDependsOn)`) to order targets. `AfterTargets` runs even if the predecessor *failed*, causing confusing cascading errors. Use `BeforeTargets`/`AfterTargets` only when no `DependsOn` property exists, and consider checking `$(MSBuildLastTaskResult)`. |
| **XML indentation** | MSBuild/XML files use 2 spaces for indentation (per `.editorconfig`), not tabs. |

---

## 3. Nullable Reference Types

| Check | What to look for |
|-------|-----------------|
| **`#nullable enable`** | New files should have `#nullable enable` at the top with no preceding blank lines. |
| **Never use `!` (null-forgiving operator)** | The `!` operator is banned. If the value can be null, add a proper null check. If it can't be null, make the type non-nullable. AI-generated code frequently sprinkles `!` to silence warnings — this turns compile-time safety into runtime `NullReferenceException`s. |
| **Use `IsNullOrEmpty()` extension** | Use the `NullableExtensions` instance methods (`str.IsNullOrEmpty()`, `str.IsNullOrWhiteSpace()`) instead of the static `string.IsNullOrEmpty(str)` / `string.IsNullOrWhiteSpace(str)` — they integrate with `[NotNullWhen]` for NRT flow analysis. |
| **`ArgumentNullException.ThrowIfNull`** | Android-targeted code (.NET 10+) should use `ArgumentNullException.ThrowIfNull(param)`. |

---

## 4. Formatting & Style

This project uses Mono style with tabs. Formatting violations create noisy diffs
and merge conflicts.

| Check | What to look for |
|-------|-----------------|
| **Tabs, not spaces** | Indentation must use tabs (width 8 in `.editorconfig`). |
| **Space before `(` and `[`** | Method calls: `Foo ()`, `Bar (1, 2)`. Array access: `array [0]`. This is Mono style — omitting the space is wrong here even though it's standard elsewhere. |
| **`""` not `string.Empty`** | Use `""` for empty strings. Use `[]` not `Array.Empty<T>()` for empty arrays. |
| **Raw string literals** | Multi-line strings should use C# raw string literals (`"""`) instead of `@""` with escaped quotes. |
| **No `#region`/`#endregion`** | Region directives hide code and make reviews harder. Remove them. |
| **File-scoped namespaces** | New files should use `namespace Foo;` not `namespace Foo { }`. Don't reformat existing files. |
| **Minimal diffs** | Don't leave random empty lines. Preserve existing formatting and comments in files you didn't write. |

---

## 5. Async & Cancellation Patterns

| Check | What to look for |
|-------|-----------------|
| **CancellationToken propagation** | Every `async` method that accepts a `CancellationToken` must pass it to ALL downstream async calls. A token that's accepted but never used is a broken contract. |
| **OperationCanceledException** | Catch-all blocks (`catch (Exception)`) must NOT swallow `OperationCanceledException`. Catch it explicitly first and rethrow, or use a type filter. |
| **Honor the token** | If a method accepts `CancellationToken`, it must observe it — register a callback to kill processes, check `IsCancellationRequested` in loops, pass it downstream. Don't accept it just for API completeness. |

---

## 6. Error Handling

| Check | What to look for |
|-------|-----------------|
| **No empty catch blocks** | Every `catch` must capture the `Exception` and log it (or rethrow). No silent swallowing. |
| **Validate parameters** | Enum parameters and string-typed "mode" values must be validated — throw `ArgumentException` or `NotSupportedException` for unexpected values. |
| **Fail fast on critical ops** | If a critical operation fails, throw immediately. Silently continuing leads to confusing downstream failures. |
| **Check process exit codes** | If one operation checks the process exit code, ALL similar operations must too. Inconsistent error checking creates a false sense of safety. |

---

## 7. Resource & Localization Files

| Check | What to look for |
|-------|-----------------|
| **Only modify English `.resx` files** | Only edit the main English `*.resx` files (e.g., `Resources.resx`). Never modify non-English `.resx` files or `*.lcl` files in `Localize/loc/` — they are auto-generated by the localization pipeline. |
| **Error messages in `Properties.Resources`** | New error/warning messages should be added to the resource file and referenced as `Properties.Resources.XA####`, not hard-coded in C# strings. This enables localization. |

---

## 8. Security

| Check | What to look for |
|-------|-----------------|
| **Zip Slip protection** | Archive extraction must validate that every entry path, after `Path.GetFullPath()`, resolves under the destination directory. |
| **Command injection** | Arguments passed to `Process.Start` must be sanitized. Use `ArgumentList` (not string interpolation into command strings). |
| **Path traversal** | `StartsWith()` checks on paths must normalize with `Path.GetFullPath()` first. |

---

## 9. Performance

| Check | What to look for |
|-------|-----------------|
| **Avoid unnecessary allocations** | Don't create intermediate collections when LINQ chaining or a single list would do. Char arrays for `string.Split()` should be `static readonly` fields. |
| **XmlReader over LINQ XML** | For forward-only XML parsing (manifests, config files), prefer `XmlReader` — it's streaming and allocation-free. `XElement`/`XDocument` builds a full DOM tree. |
| **ArrayPool for large buffers** | Buffers ≥ 1 KB should use `ArrayPool<byte>.Shared.Rent()` with `try`/`finally` return. Large allocations go to the LOH and are expensive to GC. |
| **p/invoke over process spawn** | For single syscalls like `chmod`, use `[DllImport("libc")]` instead of spawning a child process. Process creation is orders of magnitude more expensive. |

---

## 10. Code Organization

| Check | What to look for |
|-------|-----------------|
| **One type per file** | Each public class, struct, enum, or interface must be in its own `.cs` file named after the type. |
| **Use `record` for data types** | Immutable data-carrier types should be `record` types — they get value equality, `ToString()`, and deconstruction for free. |
| **Remove unused code** | Dead methods, speculative helpers, and code "for later" should be removed. Ship only what's needed. |
| **New helpers default to `internal`** | New utility methods should be `internal` unless a confirmed external consumer needs them. Use `InternalsVisibleTo` for test access. |

---

## 11. Patterns & Conventions

| Check | What to look for |
|-------|-----------------|
| **Use existing utilities** | Check `MonoAndroidHelper`, `FileUtil`, `PathUtil`, `ITaskItemExtensions`, and other utilities before writing new helpers. Duplicating existing logic is the most expensive AI pattern. |
| **`Log.LogDebugMessage` for diagnostics** | Use `Log.LogDebugMessage(…)` for verbose/debug output, not `Console.WriteLine` or `Debug.WriteLine`. |
| **Return `IReadOnlyList<T>`** | Public methods should return `IReadOnlyList<T>` or `IReadOnlyCollection<T>` instead of mutable `List<T>`. |
| **Prefer C# pattern matching** | Use `is`, `switch` expressions, and property patterns instead of `if`/`else` type-check chains. |
| **Structured args, not string interpolation** | Process arguments should be `IEnumerable<string>` or use `ArgumentList`, not a single interpolated string. |

---

## 12. Native Code

| Check | What to look for |
|-------|-----------------|
| **CMake** | Native code uses CMake. Changes must build for all ABIs: `arm64-v8a`, `armeabi-v7a`, `x86_64`, `x86`. |
| **API bindings** | Use `[Register]` attributes. Follow `Android.*` namespace patterns. |

---

## 13. Testing

| Check | What to look for |
|-------|-----------------|
| **Inherit from `BaseTest`** | Test fixtures should inherit from `BaseTest` (provides `Root`, `TestName`, SDK paths, platform helpers). |
| **NUnit conventions** | Use `[TestFixture]`, `[Test]`, `[NonParallelizable]` (for tests that hang without it). |
| **Test with `dotnet-local`** | Tests must run via `dotnet-local.cmd`/`dotnet-local.sh` to use the locally built SDK. |

---

## 14. YAGNI & AI-Specific Pitfalls

These are patterns that AI-generated code consistently gets wrong in this repo:

| Pattern | What to watch for |
|---------|------------------|
| **Reinventing the wheel** | AI creates new infrastructure instead of using `MonoAndroidHelper`, `FileUtil`, or other existing utilities. ALWAYS check if a similar utility exists before accepting new wrapper code. |
| **Over-engineering** | HttpClient injection "for testability", speculative helper classes, unused overloads. If no caller needs it today, remove it. |
| **Swallowed errors** | AI catch blocks love to eat exceptions silently. Check EVERY catch block. |
| **Null-forgiving operator** | AI sprinkles `!` everywhere to silence nullable warnings. This is banned in this repo. Use null checks, `IsNullOrEmpty()`, or make types non-nullable. |
| **Wrong formatting** | AI generates standard C# formatting (no space before parens). This repo requires Mono style: `Foo ()`, `array [0]`. |
| **`string.Empty` and `Array.Empty<T>()`** | AI defaults to these. Use `""` and `[]` instead. |
| **Sloppy structure** | Multiple types in one file, block-scoped namespaces, `#region` directives, classes where records would do. New helpers marked `public` when `internal` suffices. |
| **Docs describe intent not reality** | AI doc comments often describe what the code *should* do, not what it *actually* does. Review doc comments against the implementation. |
| **Unused parameters** | AI adds `CancellationToken` parameters but never observes them. Unused CancellationToken is a broken contract. |
| **Modifying localization files** | AI modifies non-English `.resx` or `.lcl` files. Only the main English resource files should be edited. |
| **`git commit --amend`** | AI uses `--amend` on commits. Always create new commits — the maintainer will squash as needed. |
