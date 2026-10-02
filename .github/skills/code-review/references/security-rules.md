# Security Review Rules

Security checklist for code reviews. Applicable to any repository handling file
I/O, archives, or process execution.

---

## Archive & Path Safety

| Check | What to look for |
|-------|-----------------|
| **Zip Slip protection** | Archive extraction must validate that every entry path, after `Path.GetFullPath()`, resolves under the destination directory. Never use `ZipFile.ExtractToDirectory()` for untrusted archives without entry-by-entry validation. |
| **Path traversal** | `StartsWith()` checks on paths must normalize with `Path.GetFullPath()` first. A path like `C:\Program Files\..\Users\evil` bypasses naive prefix checks. Also check for directory boundary issues (`C:\Program FilesX` matching `C:\Program Files`). |
| **Mandatory checksum verification** | Downloads or archive installs in Android tools code must not proceed unverified when checksum/hash data is expected but missing or mismatched. Fail closed with an actionable error. |

---

## Process & Command Safety

| Check | What to look for |
|-------|-----------------|
| **Command injection** | Arguments passed to `Process.Start` must be sanitized. Use `ArgumentList` (not string interpolation into command strings). Never interpolate user/external input into command strings. |
| **Don't auto-elevate** | Don't include `IsElevated()`-style helpers that silently re-launch the current process with elevated privileges. The calling tool should handle elevation prompts. The library should error out with a clear message if it lacks permissions. |
