# Security Review Rules

Security checklist for code reviews. Applicable to any repository handling file I/O, archives, or process execution.

---

## 1. Archive & Path Safety

| Check | What to look for |
|-------|-----------------|
| **Zip Slip protection** | Archive extraction must validate that every entry path, after `Path.GetFullPath()`, resolves under the destination directory. Never use `ZipFile.ExtractToDirectory()` for untrusted archives without entry-by-entry validation. |
| **Path traversal** | `StartsWith()` checks on paths must normalize with `Path.GetFullPath()` first. A path like `C:\Program Files\..\Users\evil` bypasses naive prefix checks. Also check for directory boundary issues (`C:\Program FilesX` matching `C:\Program Files`). |

---

## 2. Process & Command Safety

| Check | What to look for |
|-------|-----------------|
| **Command injection** | Arguments passed to external processes must be sanitized. Pass arguments as separate strings (not a single interpolated string) so they are never parsed by a shell. Never interpolate user/external input into command strings. |
| **Elevation** | Don't auto-elevate. Don't include `IsElevated()` helpers that silently re-launch elevated. The calling tool should handle elevation prompts. The library should error if it lacks permissions. |
