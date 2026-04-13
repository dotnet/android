# .NET for Android Review Rules

Distilled from past code reviews by senior maintainers of dotnet/android, the
conventions in [copilot-instructions.md](../../../.github/copilot-instructions.md),
[MSBuildBestPractices.md](../../../Documentation/guides/MSBuildBestPractices.md),
and [CODE_REVIEW_POST_MORTEM.md](../../../docs/CODE_REVIEW_POST_MORTEM.md).

---

## 1. MSBuild Task Conventions (C#)

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
| **`Condition` attribute first** | On `<Target>` and task elements, put the `Condition` attribute first — it's the most important for debugging. Be consistent with attribute ordering within a file. (Postmortem `#33`) |

---

## 3. Nullable Reference Types

| Check | What to look for |
|-------|-----------------|
| **`#nullable enable`** | New files should have `#nullable enable` at the top with no preceding blank lines — **unless** nullable is already enabled at the project level (for example via the `Nullable` MSBuild property in the project or imported props), in which case it is not needed per-file. |
| **Never use `!` (null-forgiving operator)** | The postfix `!` null-forgiving operator (e.g., `foo!.Bar`) is banned. If the value can be null, add a proper null check. If it can't be null, make the type non-nullable. AI-generated code frequently sprinkles `!` to silence warnings — this turns compile-time safety into runtime `NullReferenceException`s. Note: this rule is about the postfix `!` operator, not the logical negation `!` (e.g., `if (!someBool)` or `if (!string.IsNullOrEmpty (s))`). |
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
| **`#else`/`#endif` comments** | Always annotate `#else` and `#endif` with the original expression: `#else // !NET5_LINKER` and `#endif // !NET5_LINKER`. (Postmortem `#37`) |
| **Braces outside `#if` blocks** | Don't split `{` and `}` across `#if`/`#else` branches — it confuses editors. Put the opening `{` after all the conditionally-selected base types/interfaces. (Postmortem `#38`) |
| **Reasonable line width** | Don't merge two lines into a single 160-character monster. Keep lines readable (max 180 per `.editorconfig`). (Postmortem `#36`) |
| **Consistent indentation per file** | Don't mix 2-space, 3-space, and 4-space indentation within the same file. (Postmortem `#35`) |

---

## 5. Async, Cancellation & Thread Safety Patterns

| Check | What to look for |
|-------|-----------------|
| **CancellationToken propagation** | Every `async` method that accepts a `CancellationToken` must pass it to ALL downstream async calls. A token that's accepted but never used is a broken contract. |
| **OperationCanceledException** | Catch-all blocks (`catch (Exception)`) must NOT swallow `OperationCanceledException`. Catch it explicitly first and rethrow, or use a type filter. |
| **Honor the token** | If a method accepts `CancellationToken`, it must observe it — register a callback to kill processes, check `IsCancellationRequested` in loops, pass it downstream. Don't accept it just for API completeness. |
| **Thread safety of shared state** | If a new field or property can be accessed from multiple threads (e.g., static caches, event handlers, `AsyncTask` callbacks), verify thread-safe access: `ConcurrentDictionary`, `Interlocked`, or explicit locks. A `Dictionary<K,V>` read concurrently with a write is undefined behavior. |
| **Lock ordering** | If code acquires multiple locks, the order must be consistent everywhere. Document the ordering. Inconsistent ordering → deadlock. |
| **Avoid double-checked locking — use `Lazy<T>` or `LazyInitializer`** | The double-checked locking (DCL) pattern is error-prone and [discouraged by Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile). It requires subtle memory model understanding and is easy to get wrong. Prefer `Lazy<T>` or `LazyInitializer.EnsureInitialized()` — they handle thread-safe initialization correctly. If a PR introduces or modifies a DCL pattern, flag it and suggest `Lazy<T>` instead. If DCL is truly necessary (e.g., for performance in a hot path with evidence), verify: (1) all initialization (including post-construction setup like `RegisterNatives()`) completes *before* the field is assigned, (2) there is no window where another thread can observe the singleton before initialization is fully done. |
| **Singleton initialization completeness** | When a singleton is initialized behind a lock, ensure ALL setup steps (not just construction) complete before publishing the instance. If `Initialize()` does `instance = new Foo(); instance.Setup();`, another thread can see `instance != null` and use it before `Setup()` runs. Either do all setup in the constructor, or don't publish the reference until all setup is done. |

---

## 6. Error Handling

| Check | What to look for |
|-------|-----------------|
| **No empty catch blocks** | Every `catch` must capture the `Exception` and log it (or rethrow). No silent swallowing. |
| **Validate parameters** | Enum parameters and string-typed "mode" values must be validated — throw `ArgumentException` or `NotSupportedException` for unexpected values. |
| **Fail fast on critical ops** | If a critical operation fails, throw immediately. Silently continuing leads to confusing downstream failures. |
| **Check process exit codes** | If one operation checks the process exit code, ALL similar operations must too. Inconsistent error checking creates a false sense of safety. |
| **Log messages must have context** | A bare `"GetModuleHandle failed"` could be anything. Include *what* you were doing: `"Unable to get HANDLE to libmono-android.debug.dll; GetModuleHandle returned %d"`. (Postmortem `#6`) |
| **Differentiate similar error messages** | Two messages saying `"X failed"` for different operations are impossible to debug. Make each unique. (Postmortem `#7`) |
| **Assert boundary invariants** | If a name=value pair array must have even length, assert `(length % 2) == 0` before indexing `[i+1]`. (Postmortem `#44`) |
| **Include actionable details in exceptions** | Use `nameof` for parameter names. Include the unsupported value or unexpected type. Never throw empty exceptions. |
| **Initialize output parameters in all paths** | Methods with `out` parameters must initialize them in all error paths, not just the success path. |
| **Use `ThrowIf` helpers where available** | In .NET 10+ projects, prefer `ArgumentOutOfRangeException.ThrowIfNegative`, `ArgumentNullException.ThrowIfNull`, etc. over manual if-then-throw patterns. In `netstandard2.0` projects where these helpers are unavailable, use explicit checks such as `if (x is null) throw new ArgumentNullException (nameof (x));`. |
| **Challenge exception swallowing** | When a PR adds `catch { continue; }` or `catch { return null; }`, question whether the exception is truly expected or masking a deeper problem. The default should be to let unexpected exceptions propagate. |

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
| **`HashSet.Add()` already handles duplicates** | Calling `.Contains()` before `.Add()` does the hash lookup twice. Just call `.Add()`. (Postmortem `#41`) |
| **Don't wrap a value in an interpolated string** | `$"{someString}"` creates an unnecessary `string.Format` call when `someString` is already a string. (Postmortem `#42`) |
| **Consider allocations when choosing types** | `Stopwatch` is heap-allocated; `DateTime`/`ValueStopwatch` is a struct. On hot paths or startup, prefer value types. (Postmortem `#39`) |
| **Use `.Ordinal` when comparing IL/C# identifiers** | `.Ordinal` is always faster than `.OrdinalIgnoreCase`. Use `.OrdinalIgnoreCase` only for filesystem paths. (Postmortem `#49`) |
| **`Split()` with count parameter** | `line.Split(new char[]{'='}, 2)` prevents values containing `=` from being split incorrectly. Follow existing patterns. (Postmortem `#50`) |
| **Use `Files.CopyIfStringChanged()`** | Don't write to a file if the content hasn't changed — it breaks incremental builds by updating timestamps. (Postmortem `#53`) |
| **Don't remove caches without measurement** | If a cache (like `TypeDefinitionCache`) had a measured perf win, removing it requires proving the replacement provides equivalent caching. (Postmortem `#57`) |
| **Pre-allocate collections when size is known** | Use `new List<T>(capacity)` or `new Dictionary<TK, TV>(count)` when the size is known or estimable. Repeated resizing is O(n) allocation waste. |
| **Avoid closures in hot paths** | Lambdas that capture local variables allocate a closure object on every call. In loops or frequently-called methods, extract the lambda to a static method or cache the delegate. |
| **Place cheap checks before expensive ones** | In validation chains, test simple conditions (null checks, boolean flags) before allocating strings or doing I/O. Short-circuit with `&&`/`||`. |
| **Cache repeated accessor calls** | If `foo.Bar.Baz` is used multiple times in a block, assign it to a local. This avoids repeated property evaluation and makes intent clearer. |
| **Watch for O(n²)** | Nested loops over the same or related collections, repeated `.Contains()` on a `List<T>`, or LINQ `.Where()` inside a loop are O(n²). Switch to `HashSet<T>` or `Dictionary<TK, TV>` for lookups. |
| **Extract throw helpers** | Code like `if (x) throw new SomeException(...)` in a frequently-called method prevents inlining. Extract into a `[DoesNotReturn]` helper so the JIT can inline the happy path. |

---

## 10. Code Organization

| Check | What to look for |
|-------|-----------------|
| **One type per file** | Each public class, struct, enum, or interface must be in its own `.cs` file named after the type. |
| **Use `record` for data types** | Immutable data-carrier types should be `record` types — they get value equality, `ToString()`, and deconstruction for free. |
| **Remove unused code** | Dead methods, speculative helpers, and code "for later" should be removed. Ship only what's needed. No commented-out code — Git has history. (Postmortem `#58`) |
| **New helpers default to `internal`** | New utility methods should be `internal` unless a confirmed external consumer needs them. Use `InternalsVisibleTo` for test access. |
| **Centralize duplicate algorithms** | If multiple repos have their own implementation of the same logic (e.g., "do these Cecil methods have the same parameter list?"), push it into a shared package. Duplication is a bug farm. (Postmortem `#54`) |
| **Use interfaces over concrete types** | Fields and parameters should prefer interfaces (`IMetadataResolver`) over concrete classes. When the implementation changes, you swap the implementation — not every call site. (Postmortem `#56`) |
| **Introduce base types to reduce `#if` noise** | Instead of scattering `#if` in every class, create a base type (e.g., `BaseMarkHandler`) and let subclasses just override what differs. (Postmortem `#55`) |
| **Reduce indentation with early returns** | `foreach (var x in items ?? Array.Empty<T>())` eliminates a null-check nesting level. Invert logic for the common case with `continue` so complex cases have less nesting. (Postmortem `#62`, `#63`) |
| **Don't initialize fields to default values** | `bool flag = false;` and `int count = 0;` are noise. The CLR zero-initializes all fields. Only assign when the initial value is non-default. |
| **`sealed` classes skip full Dispose** | A `sealed` class doesn't need `Dispose(bool)` + `GC.SuppressFinalize`. Just implement `IDisposable.Dispose()` directly. The full pattern is only for unsealed base classes. |
| **Well-named constants over magic numbers** | `if (retryCount > 3)` should be `if (retryCount > MaxRetries)`. Constants document intent and make the value easy to find and change. |

---

## 11. Patterns & Conventions

| Check | What to look for |
|-------|-----------------|
| **Use existing utilities** | Check `MonoAndroidHelper`, `FileUtil`, `PathUtil`, `ITaskItemExtensions`, and other utilities before writing new helpers. Duplicating existing logic is the most expensive AI pattern. |
| **`Log.LogDebugMessage` for diagnostics** | Use `Log.LogDebugMessage(…)` for verbose/debug output, not `Console.WriteLine` or `Debug.WriteLine`. Don't spam logcat with messages that fire on every type lookup miss. (Postmortem `#9`) |
| **Return `IReadOnlyList<T>`** | Public methods should return `IReadOnlyList<T>` or `IReadOnlyCollection<T>` instead of mutable `List<T>`. |
| **Prefer C# pattern matching** | Use `is`, `switch` expressions, and property patterns instead of `if`/`else` type-check chains. |
| **Structured args, not string interpolation** | Process arguments should be `IEnumerable<string>` or use `ArgumentList`, not a single interpolated string. |
| **Method names must reflect behavior** | If `CreateFoo()` sometimes returns an existing instance, rename it `GetOrCreateFoo()` or `GetFoo()`. (Postmortem `#4`) |
| **Choose collision-proof names** | Types and constants that could collide with user code or Android concepts need disambiguating prefixes (e.g., `__Xamarin.Android.Resource.Designer` with a `__` prefix). (Postmortem `#2`) |
| **Don't assume transitive assembly references** | An assembly containing an `Activity` subclass does not necessarily reference `Mono.Android.dll` directly — the reference may be transitive. Skipping assemblies based on direct reference checks can break user code. (Postmortem `#64`) |
| **Document array mutability semantics** | If a property returns a cached `int[]` (not a copy), callers who mutate it corrupt global state. Document "don't do that" explicitly. (Postmortem `#66`) |
| **Track TODOs as issues** | A `// TODO` hidden in code will be forgotten. File an issue and reference it in the comment. (Postmortem `#60`) |
| **Remove stale comments** | If the code changed, update the comment. "This loads libmonodroid.so" is wrong if we now load `libxa-internal-api.so`. (Postmortem `#59`) |
| **Link vendored source to its origin** | When importing third-party code (e.g., `CryptoConvert.cs` from Mono), add a comment with the URL and commit hash of the original source. (Postmortem `#68`) |
| **Question unnecessary path normalization** | If you normalize `\` → `/` only to normalize back later, the intermediate step is pointless. (Postmortem `#52`) |
| **Comments explain "why", not "what"** | `// increment i` adds nothing. `// skip the BOM marker — Android aapt2 chokes on it` explains intent. If a comment restates the code, delete it. |

---

## 12. Native Code (C/C++)

The native runtime (`src/native/`, historically `src/monodroid/`) is critical path
code running on every Android device. Bugs here cause crashes, memory leaks, and
security vulnerabilities that are extremely hard to diagnose remotely.

### 12a. Memory Management

| Check | What to look for |
|-------|-----------------|
| **Every `new` needs a `delete` or justification** | If a `new` has no matching cleanup, document *why* the leak is acceptable and its worst-case size. "Small leak" is not a justification without quantifying "how small" and "how often." (Postmortem `#11`) |
| **Quantify leaks** | Is the leaked path hit once per assembly resolution (dozens of times) or once per P/Invoke invocation (millions)? The answer determines whether a leak matters. (Postmortem `#12`) |
| **Document known leaks in commit messages** | If a small leak is deliberately accepted, say so in the commit message so reviewers don't rediscover it later. (Postmortem `#13`) |
| **Watch for leaks in external APIs** | Functions like `mono_guid_to_string()` allocate memory that the caller must free. Check the docs for every external API call. (Postmortem `#14`) |
| **Use RAII (`std::unique_ptr`, etc.)** | If a library can be unloaded or an object has a clear owner, use smart pointers or RAII to ensure cleanup. Don't rely on manual `delete`. (Postmortem `#15`) |
| **Stack memory adds up on Android** | Android threads can have only 2–4 KB of stack. A struct with 88 bytes of wrappers is non-trivial on the stack. Make sentinel/invalid instances `static` to avoid per-instance overhead. (Postmortem `#43`) |

### 12b. C++ Best Practices

| Check | What to look for |
|-------|-----------------|
| **Virtual destructor on base classes** | Any base class with virtual methods must have a public virtual destructor. Without one, `delete`-through-base-pointer is undefined behavior. (Postmortem `#16`) |
| **Delete copy/move constructors when inappropriate** | Types holding non-copyable resources (JNI references, file handles) must use `= delete` on copy constructor and assignment operator. (Postmortem `#17`) |
| **Prefer `private` over `protected`** | Unless the type is explicitly designed for subclassing, use `private`. Don't speculatively make things `protected`. (Postmortem `#18`) |
| **Use `const` where possible** | If a JNI parameter or function argument isn't modified, declare it `const`. (Postmortem `#19`) |
| **Follow STL naming conventions** | Collection wrappers should use `size()` not `length()` or `count()`, for consistency with `std::vector`. (Postmortem `#20`) |
| **Handle `EINTR` for system calls** | `read()`, `write()`, and other syscalls can return `EINTR` when interrupted by a signal. Retry in a loop. (Postmortem `#22`) |
| **Use `sizeof()` not magic numbers** | `16` should be `sizeof(module_uuid_t)` or equivalent. Magic numbers make code fragile and unreadable. (Postmortem `#48`) |
| **No commented-out code** | If it's not needed, delete it. Git has history. (Postmortem `#58`) |
| **Don't use compiler-reserved identifiers** | Double-underscore `__` prefixed names are reserved by the C/C++ standard. Use `_monodroid_` or similar instead. (Postmortem `#3`) |
| **Prefer `nothrow new` + null check where appropriate** | Have `operator new(size_t)` abort on OOM, but `operator new(size_t, nothrow_t)` return `nullptr` for callers that want to handle failure gracefully. |
| **Avoid merging lines for no reason** | Don't combine two 80-char lines into one 160-char line. Keep code readable. (Postmortem `#36`) |

### 12c. Symbol Visibility & Naming

| Check | What to look for |
|-------|-----------------|
| **Use `-fvisibility=hidden` by default** | Only export symbols that are explicitly needed. If a native function isn't called from managed code or another library, it shouldn't be exported. (Postmortem `#30`) |
| **Question every exported symbol** | Search GitHub for actual usage before keeping an exported function. If nothing outside `src/native/` calls it, make it internal. (Postmortem `#27`) |
| **Document cross-references for exports** | Add comments with direct links to callers (e.g., the Mono BCL line that P/Invokes the function). When the caller changes, it's clear the export can be removed. (Postmortem `#28`) |
| **Remove dead symbols proactively** | When an upstream consumer (e.g., a Mono branch) no longer uses a function, remove it now. Don't wait for "someday." (Postmortem `#29`) |
| **Avoid "monodroid" in new filenames** | The runtime libraries use `libmono-android*` names. Keep new files consistent. (Postmortem `#1`) |

### 12d. Platform-Specific Code

| Check | What to look for |
|-------|-----------------|
| **Prefer `W` (wide) Win32 functions** | Use `GetModuleHandleExW` not `GetModuleHandleEx` (the macro). Avoid the `A` (ANSI) variants entirely. (Postmortem `#23`) |
| **Don't change platform-guarded code unnecessarily** | If a change is in a `#if defined(WINDOWS)` block, verify it's actually needed on that platform. (Postmortem `#26`) |
| **Check return codes on all platform APIs** | Even APIs that "shouldn't fail" (like `PathRemoveFileSpec`) have return values. Check them. (Postmortem `#8`) |

### 12e. Build & ABI

| Check | What to look for |
|-------|-----------------|
| **CMake** | Native code uses CMake. Changes must build for all ABIs: `arm64-v8a`, `armeabi-v7a`, `x86_64`, `x86`. |
| **API bindings** | Use `[Register]` attributes. Follow `Android.*` namespace patterns. |

### 12f. Managed ↔ Native Interop

| Check | What to look for |
|-------|-----------------|
| **`static_cast` over C-style casts** | `static_cast<int>(val)` is checked at compile time. `(int)val` can silently reinterpret bits. Always use C++ casts in interop boundaries. |
| **`nullptr` over `NULL`** | `NULL` is `0` in C++, which can silently convert to integral types. `nullptr` has proper pointer semantics. |
| **Struct field ordering for padding** | When defining structs shared between managed and native code, order fields largest-to-smallest to minimize padding. Explicit `[StructLayout(LayoutKind.Sequential)]` and matching C struct must be kept in sync. |
| **Bool marshalling** | Boolean marshalling is a common source of bugs. C++ `bool` is 1 byte, Windows `BOOL` is 4 bytes. When P/Invoking, explicitly specify `[MarshalAs(UnmanagedType.U1)]` or `[MarshalAs(UnmanagedType.Bool)]` (4-byte). |
| **String marshalling charset** | P/Invoke string parameters should specify `CharSet.Unicode` (UTF-16) or use `[MarshalAs(UnmanagedType.LPUTF8Str)]` for UTF-8. Don't rely on the default (ANSI on Windows). |

---

## 13. Testing

| Check | What to look for |
|-------|-----------------|
| **Inherit from `BaseTest`** | Test fixtures should inherit from `BaseTest` (provides `Root`, `TestName`, SDK paths, platform helpers). |
| **NUnit conventions** | Use `[TestFixture]`, `[Test]`, `[NonParallelizable]` (for tests that hang without it). |
| **Test with `dotnet-local`** | Tests must run via `dotnet-local.cmd`/`dotnet-local.sh` to use the locally built SDK. |
| **Bug fixes need regression tests** | Every PR that fixes a bug should include a test that fails without the fix and passes with it. If the PR description says "fixes #1234" but adds no test, ask for one. |
| **Test assertions must be specific** | `Assert.IsNotNull(result)` or `Assert.IsTrue(success)` don't tell you what went wrong. Prefer `Assert.AreEqual(expected, actual)` or `StringAssert.Contains`. Use `Assert.That` with constraints for richer failure messages. |
| **Deterministic test data** | Tests should not depend on system locale, timezone, or current date. Use explicit `CultureInfo.InvariantCulture` and hardcoded dates when testing formatting. |
| **Test edge cases** | Empty collections, null inputs, boundary values, concurrent calls, and very large inputs should all be considered. If the PR only tests the happy path, suggest edge cases. |

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
| **Docs describe intent not reality** | AI doc comments often describe what the code *should* do, not what it *actually* does. Review doc comments against the implementation. (Postmortem `#59`) |
| **Unused parameters** | AI adds `CancellationToken` parameters but never observes them. Unused CancellationToken is a broken contract. |
| **Modifying localization files** | AI modifies non-English `.resx` or `.lcl` files. Only the main English resource files should be edited. |
| **`git commit --amend`** | AI uses `--amend` on commits. Always create new commits — the maintainer will squash as needed. |
| **Commit messages omit non-obvious choices** | Behavioral decisions ("styleable arrays are cached, not copied per-access") and known limitations ("this leaks N bytes on Android 9") belong in the commit message. (Postmortem `#13`, `#69`) |
| **Typos in user-visible strings** | Users copy-paste error messages into bug reports. Get them right. (Postmortem `#61`) |
| **Filler words in docs** | "So" at the start of a sentence adds nothing. Be direct. (Postmortem `#71`) |

---

## 15. Assembly & File Pipeline

Build tasks that transform assemblies (e.g., linking, stripping, instrumentation)
must never read from and write to the same file on disk. In-place modification
causes races with parallel tasks, breaks incremental builds, and makes debugging
impossible because the original input is destroyed.

| Check | What to look for |
|-------|-----------------|
| **Never modify assemblies in-place** | A task must not read an assembly from a path and write the modified assembly back to the same path. Instead, read from an input location, write to a separate output location, and update the in-memory MSBuild `ItemGroup` so downstream targets pick up the new paths. |
| **Preserve MSBuild metadata on updated items** | When a task replaces items in an `ItemGroup` to point at new output paths, it must copy all existing metadata from the original `ITaskItem` to the replacement item (`ITaskItem.CopyMetadataTo` or manual metadata transfer). Downstream targets rely on metadata like `%(DestinationSubDirectory)`, `%(Culture)`, `%(TargetPath)`, etc. Dropping metadata silently breaks later steps. |
| **Use `[Output]` items for relocated files** | If a task moves files to a new location, expose the updated items via an `[Output] ITaskItem[]?` property so the calling target can replace the original item group with the new paths. |
| **Separate input and output directories** | Input and output directories should be distinct (e.g., `$(IntermediateOutputPath)original/` → `$(IntermediateOutputPath)modified/`). Writing outputs alongside inputs makes cleanup and incremental-build tracking fragile. |
