# MSBuild Review Rules

Guidance for MSBuild targets, props, and project files. Loaded when `.targets`,
`.props`, `.projitems`, or `.csproj` files change.

---

## Task Logging

| Check | What to look for |
|-------|-----------------|
| **Don't use `Debug.WriteLine` or `Console.WriteLine`** | MSBuild tasks must use the task's logging facilities (e.g., `Log.LogMessage`, `Log.LogWarning`, `Log.LogError`). `Debug.WriteLine()` only reaches attached debuggers (invisible in CI). `Console.WriteLine()` bypasses MSBuild's logging pipeline entirely. |
| **Use appropriate log levels** | Use `MessageImportance.Low` for verbose diagnostics, `MessageImportance.Normal` for progress, and `MessageImportance.High` for important status. Don't spam high-importance messages. |

---

## Process Management in Tasks

| Check | What to look for |
|-------|-----------------|
| **Don't redirect stdout/stderr without draining** | Background processes with `RedirectStandardOutput = true` must have async readers draining the output. Otherwise the OS pipe buffer fills and the child process deadlocks. For fire-and-forget processes, set `Redirect* = false`. |
| **Check exit codes consistently** | If one task operation checks the process exit code, ALL similar operations must too. Inconsistent error checking creates a false sense of safety. |
| **Include stdout in error diagnostics** | When a task captures stdout, pass it to error reporting so failure messages include all output, not just stderr. |

---

## MSBuild Targets & XML

| Check | What to look for |
|-------|-----------------|
| **Underscore prefix for private names** | Internal targets, properties, and item groups should be prefixed with `_` (e.g., `_CompileJava`, `$(_JarFile)`). MSBuild has no visibility — the underscore signals "internal." |
| **Incremental builds (`Inputs`/`Outputs`)** | Every target that *writes files* must have `Inputs` and `Outputs` so MSBuild can skip it when nothing changed. Targets that only read files, set properties, or populate item groups do NOT need them. |
| **`FileWrites` for intermediate files** | Intermediate files must be added to `@(FileWrites)` so `IncrementalClean` doesn't delete them. |
| **XML indentation** | MSBuild/XML files use 2 spaces for indentation (per `.editorconfig`), not tabs. |
| **`Condition` attribute first** | On `<Target>` and task elements, put the `Condition` attribute first — it's the most important for debugging. |

---

## Downstream Coordination

| Check | What to look for |
|-------|-----------------|
| **Port, don't rewrite** | If a downstream consumer already has working logic for the same task, port it rather than writing new code. The existing code has real-world edge cases already handled. |
| **Draft downstream PR before merging** | Shared library changes should be accompanied by a draft PR in the consuming repo (dotnet/android) that proves the API actually works. |
