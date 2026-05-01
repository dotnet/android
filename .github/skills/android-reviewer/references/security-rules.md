# Security Review Rules

Security checks applicable to any repository handling file I/O, process
execution, or user-supplied paths.

---

## Security Checks

| Check | What to look for |
|-------|-----------------|
| **Zip Slip protection** | Archive extraction must validate that every entry path, after `Path.GetFullPath()`, resolves under the destination directory. |
| **Command injection** | Arguments passed to `Process.Start` must be sanitized. Use `ArgumentList` (not string interpolation into command strings). |
| **Path traversal** | `StartsWith()` checks on paths must normalize with `Path.GetFullPath()` first. |
