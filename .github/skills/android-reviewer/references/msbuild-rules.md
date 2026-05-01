# MSBuild Review Rules

MSBuild task and target guidance applicable to any .NET repository with MSBuild
build infrastructure.

---

## MSBuild Task Conventions (C#)

All build tasks ship to customers and run inside MSBuild. Getting them wrong
causes broken builds for every .NET Android developer.

| Check | What to look for |
|-------|-----------------|
| **Prefer `AndroidTask` / `AsyncTask`** | MSBuild tasks that execute build logic should extend `AndroidTask` (from `Microsoft.Android.Build.BaseTasks`). They must implement `TaskPrefix` (a short string for error codes) and `RunTask()`. Simple wrapper tasks that only log errors/warnings/messages (e.g., `AndroidError`, `AndroidWarning`, `AndroidMessage`) may inherit directly from `Microsoft.Build.Utilities.Task`. Unhandled exceptions in `AndroidTask` are automatically converted to proper error codes. |
| **Use `AsyncTask` for background work** | Tasks that need `async`/`await` should extend `AsyncTask` and override `RunTaskAsync()`. It handles `Yield()`, `try`/`finally`, and `Reacquire()` automatically. Use `AsyncTask.Log*` helpers for logging from the background thread — calling `Log.LogMessage` directly can cause IDE hangs. Use full paths on background threads (`Environment.CurrentDirectory` may differ if the task is on another MSBuild node). Leverage the `WhenAll` extension for parallel work over `ITaskItem[]`. |
| **Return `!Log.HasLoggedErrors`** | `RunTask()` must return `!Log.HasLoggedErrors`. Do not return `true`/`false` directly — it skips the centralized error-tracking mechanism. |
| **Use `Log.LogCoded*` methods** | Errors and warnings must use `Log.LogCodedError("XA####", …)` or `Log.LogCodedWarning("XA####", …)` — never bare `Log.LogError` without a code. Error messages should come from `Properties.Resources`. |
| **XA error codes** | Error codes follow `XA####` (4+ digits). New codes must not collide with existing ones. Check `Properties/Resources.cs.resx` for used codes. Every new code must have a documentation entry and be localized. (Postmortem `#10`) |
| **`[Required]` properties** | `[Required]` properties must be non-nullable with a default: `public string Foo { get; set; } = "";` or `public ITaskItem[] Bar { get; set; } = [];`. Non-`[Required]` and `[Output]` properties must be nullable (`string?`, `ITaskItem[]?`). Mark properties `[Required]` when the task crashes without them. (Postmortem `#51`) |
| **`UsingTask` for internal tasks** | `<UsingTask/>` elements for `xa-prep-tasks` and `BootstrapTasks` (internal, not shipped) must use `TaskFactory="TaskHostFactory"` and `Runtime="NET"`. Do NOT add these attributes to shipped task definitions in `Xamarin.Android.Common.targets` or `Microsoft.Android.Sdk/*.targets`. |
| **Caching with `RegisterTaskObject`** | Use `BuildEngine4.RegisterTaskObject()` (via the `RegisterTaskObjectAssemblyLocal()` extension method) instead of `static` variables for sharing data between tasks or across builds. Use `as` for casts to avoid `InvalidCastException`. Cache keys should include context that invalidates properly (device target, file path, version). Cache primitive/small values only. |
| **Use appropriate log levels** | Use `MessageImportance.Low` for verbose diagnostics, `MessageImportance.Normal` for progress, and `MessageImportance.High` for important status. Don't spam high-importance messages. |

---

## Process Management in Tasks

| Check | What to look for |
|-------|-----------------|
| **Don't redirect stdout/stderr without draining** | Background processes with `RedirectStandardOutput = true` must have async readers draining the output. Otherwise the OS pipe buffer fills and the child process deadlocks. For fire-and-forget processes, set `Redirect* = false`. |
| **Include stdout in error diagnostics** | When a task captures stdout, pass it to error reporting so failure messages include all output, not just stderr. |

---

## MSBuild Targets & XML

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
| **`Condition` attribute first** | On `<Target>` and task elements, put the `Condition` attribute first — it's the most important for debugging. Be consistent with attribute ordering within a file. (Postmortem `#33`) |
