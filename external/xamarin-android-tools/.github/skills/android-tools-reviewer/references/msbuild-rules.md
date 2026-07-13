# MSBuild Review Rules

Guidance for MSBuild tasks, targets, and props files. Applicable to any .NET repository with custom MSBuild infrastructure.

---

## 1. Task Logging

| Check | What to look for |
|-------|-----------------|
| **Don't use `Debug.WriteLine` or `Console.WriteLine`** | MSBuild tasks must use the task's logging facilities (e.g., `Log.LogMessage`, `Log.LogWarning`, `Log.LogError`). `Debug.WriteLine()` only reaches attached debuggers (invisible in CI). `Console.WriteLine()` bypasses MSBuild's logging pipeline entirely. |
| **Use appropriate log levels** | Use `MessageImportance.Low` for verbose diagnostics, `MessageImportance.Normal` for progress, and `MessageImportance.High` for important status. Don't spam high-importance messages. |

---

## 2. Process Management in Tasks

| Check | What to look for |
|-------|-----------------|
| **Don't redirect stdout/stderr without draining** | Background processes with `RedirectStandardOutput = true` must have async readers draining the output. Otherwise the OS pipe buffer fills and the child process deadlocks. For fire-and-forget processes, set `Redirect* = false`. |
| **Check exit codes consistently** | If one task operation checks the process exit code, ALL similar operations must too. Inconsistent error checking creates a false sense of safety. |
| **Include stdout in error diagnostics** | When a task captures stdout, pass it to error reporting so failure messages include all output, not just stderr. |

---

## 3. Downstream Coordination

| Check | What to look for |
|-------|-----------------|
| **Port, don't rewrite** | If a downstream consumer already has working logic for the same task, port it rather than writing new code. The existing code has real-world edge cases already handled. |
| **Draft downstream PR before merging** | Shared library changes should be accompanied by a draft PR in the consuming repo that proves the API actually works. Merge the library first, update the submodule pointer, then merge the consumer. |
