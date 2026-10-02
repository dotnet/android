# Security Review Rules

Security checklist for code reviews. Applicable to any repository handling file
I/O, archives, or process execution.

---

## Archive & Path Safety

| Check | What to look for |
|-------|-----------------|
| **Archive names vs filesystem paths** | Distinguish ZIP creation from extraction. A canonical source filename does not prove the generated ZIP name is safe. Validate the final archive-controlled name after separator conversion, prefixing, or renaming; literal Unix backslashes can become `../` components. Reject parent traversal, rooted/drive/UNC names, and ambiguous Windows path aliases where applicable. |
| **Canonical containment** | Extraction destinations must resolve under the selected root after `Path.GetFullPath()`. Prefix comparisons need a directory separator boundary (`rootSibling` must not match `root`), with ordinal case-sensitive comparison on Unix and case-insensitive comparison on Windows. Verify library guarantees rather than assuming all required protections are built in. |
| **Linked descendants** | Lexical containment alone does not prevent escape through symbolic links or junctions. Reject archive-controlled linked descendants before I/O; distinguish them from a caller-selected linked root that the operation explicitly supports. Use APIs available on the actual target framework. |
| **Fail closed before side effects** | Preflight every selected original and transformed name before extraction, then revalidate the final destination immediately before writing or deleting. Unsafe input must produce an error, not a successful skip. Static checks alone are not race-proof if another actor can replace filesystem components concurrently. |
| **Mandatory checksum verification** | Downloads or archive installs in Android tools code must not proceed unverified when checksum/hash data is expected but missing or mismatched. Fail closed with an actionable error. |

---

## Process & Command Safety

| Check | What to look for |
|-------|-----------------|
| **Command injection** | Arguments passed to `Process.Start` must be sanitized. Use `ArgumentList` (not string interpolation into command strings). Never interpolate user/external input into command strings. |
| **Don't auto-elevate** | Don't include `IsElevated()`-style helpers that silently re-launch the current process with elevated privileges. The calling tool should handle elevation prompts. The library should error out with a clear message if it lacks permissions. |
