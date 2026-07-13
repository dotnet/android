# Repo Conventions

<!-- Derived from docs/CODE_REVIEW_POSTMORTEM.md — see that file for the original 56 findings. -->

Patterns, naming, and style rules specific to this repository (`dotnet/android-tools`). Always loaded during reviews.

---

## 1. Required Utilities

| Check | What to look for |
|-------|-----------------|
| **Use `ProcessUtils`** | All process creation must go through `ProcessUtils.CreateProcessStartInfo()` and `ProcessUtils.StartProcess()`. No direct `new ProcessStartInfo()` or `Process.Start()`. Pass arguments as separate strings — `ProcessUtils` uses `ArgumentList` on net5+ and falls back to `Arguments` on netstandard2.0. |
| **Use `FileUtil`** | File extraction, downloads, checksum verification, and path operations belong in `FileUtil`. Don't duplicate file helpers in domain classes. |
| **Use `Action<TraceLevel, string>` logger** | Diagnostic output must use the existing `Action<TraceLevel, string>? logger` delegate pattern — never `System.Diagnostics.Debug.WriteLine()` or `Console.WriteLine()`. Methods that might be called from MSBuild tasks must accept a `logger` parameter and invoke it with the appropriate `TraceLevel`. See `AndroidSdkInfo.DefaultConsoleLogger` for the canonical implementation. |

---

## 2. Null-Object Pattern

| Check | What to look for |
|-------|-----------------|
| **Assign null-object sentinels early** | Methods accepting nullable dependencies (`IProgress<T>?`, `ILogger?`, `Action<string>?`) should assign a null-object sentinel early (e.g., `progress ??= NullProgress.Instance`) and then use the dependency without `?.` null checks throughout the method. Scattered `?.` calls are noise, invite missed spots, and signal a missing null-object type. If no null-object type exists yet, recommend creating one. |

---

## 3. Naming & Constants

| Check | What to look for |
|-------|-----------------|
| **Avoid ambiguous names** | Types that could collide with Android concepts (e.g., `ManifestComponent` vs `AndroidManifest.xml`) need disambiguating prefixes (e.g., `SdkManifestComponent`). |
| **No magic numbers** | Literal values like buffer sizes (`81920`), divisors (`1048576`), permission masks (`0x1ED` = 0755) should be named constants. |
| **Environment variable constants** | Use `EnvironmentVariableNames.AndroidHome` — not raw `"ANDROID_HOME"` strings. Typos in env var names produce silent, hard-to-debug failures. |
| **ANDROID_SDK_ROOT is deprecated** | Per [Android docs](https://developer.android.com/tools/variables#envar), use `ANDROID_HOME` everywhere. Do not introduce new references to `ANDROID_SDK_ROOT`. |

---

## 4. File & Directory Patterns

| Check | What to look for |
|-------|-----------------|
| **Version-based directories** | Install SDK/JDK to versioned paths (`cmdline-tools/19.0/`, not `cmdline-tools/latest/`). Versioned paths are self-documenting and allow side-by-side installs. |
| **Safe directory replacement** | Use move-with-rollback: rename existing → temp, move new in place, validate, delete temp only after validation succeeds. Never delete the backup before confirming the new install works. |
| **Cross-volume moves** | `Directory.Move` is really a rename — it fails across filesystems. Extract archives near the target path (same parent directory), or catch `IOException` and fall back to recursive copy + delete. |

---

## 5. Code Style

| Check | What to look for |
|-------|-----------------|
| **One type per file** | Each public class, struct, enum, or interface must be in its own `.cs` file named after the type. No multiple top-level types in a single file. |
| **No #region directives** | `#region` hides code and makes reviews harder. Remove them. This also applies to banner/section-separator comments (e.g., `// --- Device Tests ---`) — they signal the file should be split instead. |
| **Method names must reflect behavior** | If `CreateFoo()` sometimes returns an existing instance, rename it `GetOrCreateFoo()` or `GetFoo()`. |
| **Comments explain "why", not "what"** | `// increment i` adds nothing. `// skip the BOM — aapt2 chokes on it` explains intent. If a comment restates the code, delete it. |
| **Track TODOs as issues** | A `// TODO` hidden in code will be forgotten. File an issue and reference it in the comment. |
| **Remove stale comments** | If the code changed, update the comment. Comments that describe old behavior are misleading. |
| **Formatting** | Tabs (not spaces), K&R braces. See [Mono Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/). Only format code you add or modify; never reformat existing lines. |
