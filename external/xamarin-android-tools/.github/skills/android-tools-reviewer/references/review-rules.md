# Android Tools Review Rules

Distilled from [CODE_REVIEW_POSTMORTEM.md](../../../docs/CODE_REVIEW_POSTMORTEM.md) — 56 findings
from reviews by @jonathanpeppers on PRs #274, #275, #281–#284.

---

## 1. Target Framework Compatibility

This library multi-targets `netstandard2.0` + `net10.0` and runs inside Visual Studio on .NET
Framework. Every API call must work on both targets.

| Check | What to look for |
|-------|-----------------|
| **netstandard2.0 API surface** | Methods/overloads that only exist on net5+. Common traps: `HttpContent.ReadAsStringAsync(CancellationToken)`, `ProcessStartInfo.ArgumentList`, `Environment.IsPrivilegedProcess`, `ArrayPool<T>` (needs `System.Buffers` package on ns2.0). When unsure, check MS Learn docs. |
| **C# language features** | `init` accessors, `required` keyword, file-scoped types, raw string literals — may need polyfills or `#if` guards. |
| **Conditional compilation** | New API usage should be behind `#if NET5_0_OR_GREATER` (or similar) with a fallback for netstandard2.0. |

**Postmortem refs:** #3, #4, #30

---

## 2. Async & Cancellation Patterns

| Check | What to look for |
|-------|-----------------|
| **CancellationToken propagation** | Every `async` method that accepts a `CancellationToken` must pass it to ALL downstream async calls (`GetAsync`, `ReadAsStreamAsync`, `SendAsync`, etc.). A token that's accepted but never used is a broken contract. |
| **OperationCanceledException** | Catch-all blocks (`catch (Exception)`) must NOT swallow `OperationCanceledException`. Either catch it explicitly first and rethrow, or use a type filter. |
| **GetStringAsync** | On netstandard2.0, `GetStringAsync(url)` doesn't accept a `CancellationToken`. Use `GetAsync(url, ct)` + `ReadAsStringAsync()` instead. |

**Postmortem refs:** #5, #37

---

## 3. Resource Management

| Check | What to look for |
|-------|-----------------|
| **HttpClient must be static** | `HttpClient` instances should be `static readonly` fields, not per-instance. Creating/disposing `HttpClient` leads to socket exhaustion via `TIME_WAIT` accumulation. See [Microsoft guidelines](https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines). |
| **No HttpClient injection (YAGNI)** | Don't add `HttpClient` constructor parameters "for testability" unless a caller actually needs it today. The AI tends to over-engineer this. |
| **ArrayPool for large buffers** | Buffers ≥ 1KB (especially 80KB+ download buffers) should use `ArrayPool<byte>.Shared.Rent()` with `try/finally` return. Large allocations go to the LOH and are expensive to GC. |
| **IDisposable** | Classes that own unmanaged resources or expensive managed resources must implement `IDisposable` with a dispose guard (`ThrowIfDisposed`). |

**Postmortem refs:** #6, #7, #13

---

## 4. Error Handling

| Check | What to look for |
|-------|-----------------|
| **No empty catch blocks** | Every `catch` must capture the `Exception` and log it (or rethrow). No silent swallowing. Even "expected" exceptions should be logged for diagnostics. |
| **Validate parameters** | Enum parameters and string-typed "mode" values must be validated — throw `ArgumentException` or `NotSupportedException` for unexpected values. Don't silently accept garbage. |
| **Fail fast on critical ops** | If an operation like `chmod` or checksum verification fails, throw immediately. Silently continuing leads to confusing downstream failures ("permission denied" when the real problem was chmod). |
| **Mandatory verification** | Checksum/hash verification must NOT be optional. If the checksum can't be fetched, the operation must fail — not proceed unverified. |

**Postmortem refs:** #11, #20, #22, #35

---

## 5. Security

| Check | What to look for |
|-------|-----------------|
| **Zip Slip protection** | Archive extraction must validate that every entry path, after `Path.GetFullPath()`, resolves under the destination directory. Never use `ZipFile.ExtractToDirectory()` for untrusted archives without entry-by-entry validation. |
| **Command injection** | Arguments passed to `Process.Start` or written to `.cmd`/`.sh` scripts must be sanitized. Use `ProcessUtils.CreateProcessStartInfo()` with separate argument strings — it uses `ArgumentList` on net5+ (no shell parsing). Never interpolate user/external input into command strings. |
| **Path traversal** | `StartsWith()` checks on paths must normalize with `Path.GetFullPath()` first. A path like `C:\Program Files\..\Users\evil` bypasses naive prefix checks. Also check for directory boundary issues (`C:\Program FilesX` matching `C:\Program Files`). |
| **Elevation** | Don't auto-elevate. Don't include `IsElevated()` helpers that silently re-launch elevated. The calling tool (VS, VS Code) should handle elevation prompts. The library should error if it lacks permissions. |

**Postmortem refs:** #17, #34, #39

---

## 6. Code Organization

| Check | What to look for |
|-------|-----------------|
| **One type per file** | Each public class, struct, enum, or interface must be in its own `.cs` file named after the type. No multiple top-level types in a single file. |
| **File-scoped namespaces** | New files should use `namespace Foo;` (not `namespace Foo { ... }`). Don't reformat existing files. |
| **No #region directives** | `#region` hides code and makes reviews harder. Remove them. This also applies to banner/section-separator comments (e.g., `// --- Device Tests ---`) — they serve the same purpose as `#region` and signal the file should be split instead. |
| **Use `record` for data types** | Immutable data-carrier types (progress, version info, license info) should be `record` types. They get value equality, `ToString()`, and deconstruction for free. |
| **Remove unused code** | Dead methods, speculative helpers, and code "for later" should be removed. Ship only what's needed. |

**Postmortem refs:** #9, #12, #25, #28, #56

---

## 7. Naming & Constants

| Check | What to look for |
|-------|-----------------|
| **Avoid ambiguous names** | Types that could collide with Android concepts (e.g., `ManifestComponent` vs `AndroidManifest.xml`) need disambiguating prefixes (e.g., `SdkManifestComponent`). |
| **No magic numbers** | Literal values like buffer sizes (`81920`), divisors (`1048576`), permission masks (`0x1ED` = 0755) should be named constants. |
| **Environment variable constants** | Use `EnvironmentVariableNames.AndroidHome` — not raw `"ANDROID_HOME"` strings. Typos in env var names produce silent, hard-to-debug failures. |
| **ANDROID_SDK_ROOT is deprecated** | Per [Android docs](https://developer.android.com/tools/variables#envar), use `ANDROID_HOME` everywhere. Do not introduce new references to `ANDROID_SDK_ROOT`. |

**Postmortem refs:** #10, #14, #18, #19

---

## 8. Performance

| Check | What to look for |
|-------|-----------------|
| **XmlReader over LINQ XML** | For forward-only XML parsing (manifests, config files), use `XmlReader` — it's streaming and allocation-free. `XElement`/`XDocument` builds a full DOM tree. |
| **p/invoke over process spawn** | For single syscalls like `chmod`, use `[DllImport("libc")]` instead of spawning a child process. Process creation is orders of magnitude more expensive. |
| **Avoid intermediate collections** | Don't create two lists and `AddRange()` one to the other. Build a single list, or use LINQ to chain. |
| **Cache reusable arrays** | Char arrays for `string.Split()` (like whitespace chars) should be `static readonly` fields, not allocated on each call. |

**Postmortem refs:** #8, #14, #21, #31

---

## 9. Patterns & Conventions

| Check | What to look for |
|-------|-----------------|
| **Use `ProcessUtils`** | All process creation must go through `ProcessUtils.CreateProcessStartInfo()` and `ProcessUtils.StartProcess()`. No direct `new ProcessStartInfo()` or `Process.Start()`. |
| **Use `FileUtil`** | File extraction, downloads, checksum verification, and path operations belong in `FileUtil`. Don't duplicate file helpers in domain classes. |
| **Null-object pattern** | Methods accepting nullable dependencies (`IProgress<T>?`, `ILogger?`, `Action<string>?`) should assign a null-object sentinel early (e.g., `progress ??= NullProgress.Instance`, `logger ??= NullLogger.Instance`) and then use the dependency without `?.` null checks throughout the method. Scattered `logger?.Log(...)` or `progress?.Report(...)` calls are a code smell — they add noise, invite missed spots, and signal a missing null-object type. If no null-object type exists yet, recommend creating one. |
| **Version-based directories** | Install SDK/JDK to versioned paths (`cmdline-tools/19.0/`, not `cmdline-tools/latest/`). Versioned paths are self-documenting and allow side-by-side installs. |
| **Safe directory replacement** | Use move-with-rollback: rename existing → temp, move new in place, validate, delete temp only after validation succeeds. Never delete the backup before confirming the new install works. |
| **Cross-volume moves** | `Directory.Move` is really a rename — it fails across filesystems. Extract archives near the target path (same parent directory), or catch `IOException` and fall back to recursive copy + delete. |

**Postmortem refs:** #15, #16, #23, #36, #38

---

## 10. YAGNI & AI-Specific Pitfalls

These are patterns that AI-generated code consistently gets wrong:

| Pattern | What to watch for |
|---------|------------------|
| **Reinventing the wheel** | AI creates new infrastructure (e.g., `AndroidToolRunner`) instead of using existing utilities (`ProcessUtils`). ALWAYS check if a similar utility exists before accepting new wrapper code. This is the most expensive AI pattern — hundreds of lines of plausible code that duplicates what's already there. |
| **Over-engineering** | HttpClient injection "for testability", elevation auto-detection, speculative helper classes, unused overloads. If no caller needs it today, remove it. |
| **Swallowed errors** | AI catch blocks love to eat exceptions silently. Check EVERY catch block. Also check that exit codes are checked consistently — if `ListDevicesAsync` checks exit codes, `StopEmulatorAsync` should too. |
| **Ignoring target framework** | AI generates code for the newest .NET. Check every API call against netstandard2.0. |
| **Sloppy structure** | Multiple types in one file, block-scoped namespaces, #region directives, classes where records would do. New helpers marked `public` when `internal` suffices. |
| **Confidently wrong domain facts** | AI once claimed `ANDROID_SDK_ROOT` was the recommended variable (it's deprecated). Always verify domain-specific claims against official docs. |
| **Over-mocking** | Not everything needs to be mocked. Network integration tests with `Assert.Ignore` on failure are fine and catch real API changes that mocks never will. |
| **Docs describe intent not reality** | AI doc comments often describe what the code *should* do, not what it *actually* does. Review doc comments against the implementation. |
| **Unused parameters** | AI adds `CancellationToken` parameters but never observes them, or accepts `additionalArgs` as a string and interpolates it into a command. Unused CancellationToken is a broken contract; string args are injection risks. |
| **Null-forgiving operator (`!`)** | Never use `!` to suppress nullable warnings. If the value can be null, add a proper null check. If it can't be null, make the parameter/variable non-nullable. AI frequently sprinkles `!` to make the compiler happy — this turns compile-time warnings into runtime `NullReferenceException`s. Use `IsNullOrEmpty()` extension methods or null-coalescing instead. |

**Postmortem refs:** #7, #28, #29, #40, #41, #42, #49, #50, #51, #52, #54

---

## 11. API Design

| Check | What to look for |
|-------|-----------------|
| **Return `IReadOnlyList<T>` not `List<T>`** | Public methods should return `IReadOnlyList<T>` (or `IReadOnlyCollection<T>`) instead of mutable `List<T>`. Prevents callers from mutating internal state. |
| **New helpers default to `internal`** | New utility methods should be `internal` unless a confirmed external consumer (e.g., `dotnet/android`) needs them. Use `InternalsVisibleTo` for test access. |
| **Structured args, not string interpolation** | Additional arguments to processes should be `IEnumerable<string>`, not a single `string` that gets interpolated. Use `ProcessUtils.CreateProcessStartInfo()` which handles `ArgumentList` safely. |
| **Honor `CancellationToken`** | If a method accepts a `CancellationToken`, it MUST observe it — register a callback to kill processes, check `IsCancellationRequested` in loops, pass it to downstream async calls. Don't just accept it for API completeness. |
| **Add overloads to reduce caller ceremony** | If every caller performs the same conversion before calling a method (e.g., `writer.ToString()` before `ThrowIfFailed()`), the method should have an overload that accepts the unconverted type directly. |
| **Prefer C# pattern matching** | Use `is`, `switch` expressions, and property patterns instead of `if`/`else` type-check chains. Pattern matching is more concise, avoids casts, and enables exhaustiveness checks. |

**Postmortem refs:** #46, #47, #49, #50, #53, #55

---

## 12. Code Sharing & Downstream Coordination

| Check | What to look for |
|-------|-----------------|
| **Port, don't rewrite** | If `dotnet/android` (or another downstream consumer) already has working logic for the same task, port it rather than writing new code. The existing code has real-world edge cases already handled. |
| **Draft downstream PR before merging** | Shared library changes should be accompanied by a draft PR in the consuming repo that proves the API actually works. Merge the library first, update the submodule pointer, then merge the consumer. |
| **Don't redirect stdout/stderr without draining** | Background processes with `RedirectStandardOutput = true` must have async readers draining the output. Otherwise the OS pipe buffer fills and the child process deadlocks. For fire-and-forget processes, set `Redirect* = false`. |
| **Check exit codes consistently** | If one operation (`ListDevicesAsync`) checks the process exit code, ALL similar operations (`StopEmulatorAsync`, `WaitForDeviceAsync`) must too. Inconsistent error checking creates a false sense of safety. |

**Postmortem refs:** #42, #43, #44, #45, #48
