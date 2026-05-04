# C# Review Rules

General C# guidance applicable to any .NET repository.

---

## Nullable Reference Types

| Check | What to look for |
|-------|-----------------|
| **`#nullable enable`** | New files should have `#nullable enable` at the top with no preceding blank lines — **unless** nullable is already enabled at the project level (for example via the `Nullable` MSBuild property in the project or imported props), in which case it is not needed per-file. |
| **Never use `!` (null-forgiving operator)** | The postfix `!` null-forgiving operator (e.g., `foo!.Bar`) is banned. If the value can be null, add a proper null check. If it can't be null, make the type non-nullable. AI-generated code frequently sprinkles `!` to silence warnings — this turns compile-time safety into runtime `NullReferenceException`s. Note: this rule is about the postfix `!` operator, not the logical negation `!` (e.g., `if (!someBool)` or `if (!string.IsNullOrEmpty (s))`). |
| **Use `IsNullOrEmpty()` extension** | Use the `NullableExtensions` instance methods (`str.IsNullOrEmpty()`, `str.IsNullOrWhiteSpace()`) instead of the static `string.IsNullOrEmpty(str)` / `string.IsNullOrWhiteSpace(str)` — they integrate with `[NotNullWhen]` for NRT flow analysis. |
| **`ArgumentNullException.ThrowIfNull`** | Android-targeted code (.NET 10+) should use `ArgumentNullException.ThrowIfNull(param)`. |

---

## Async, Cancellation & Thread Safety Patterns

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

## Error Handling

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

## Performance

| Check | What to look for |
|-------|-----------------|
| **Avoid unnecessary allocations** | Don't create intermediate collections when LINQ chaining or a single list would do. Char arrays for `string.Split()` should be `static readonly` fields. |
| **ArrayPool for large buffers** | Buffers ≥ 1 KB should use `ArrayPool<byte>.Shared.Rent()` with `try`/`finally` return. Large allocations go to the LOH and are expensive to GC. |
| **`HashSet.Add()` already handles duplicates** | Calling `.Contains()` before `.Add()` does the hash lookup twice. Just call `.Add()`. (Postmortem `#41`) |
| **Don't wrap a value in an interpolated string** | `$"{someString}"` creates an unnecessary `string.Format` call when `someString` is already a string. (Postmortem `#42`) |
| **Consider allocations when choosing types** | `Stopwatch` is heap-allocated; `DateTime`/`ValueStopwatch` is a struct. On hot paths or startup, prefer value types. (Postmortem `#39`) |
| **Use `.Ordinal` when comparing IL/C# identifiers** | `.Ordinal` is always faster than `.OrdinalIgnoreCase`. Use `.OrdinalIgnoreCase` only for filesystem paths. (Postmortem `#49`) |
| **`Split()` with count parameter** | `line.Split(new char[]{'='}, 2)` prevents values containing `=` from being split incorrectly. Follow existing patterns. (Postmortem `#50`) |
| **Pre-allocate collections when size is known** | Use `new List<T>(capacity)` or `new Dictionary<TK, TV>(count)` when the size is known or estimable. Repeated resizing is O(n) allocation waste. |
| **Avoid closures in hot paths** | Lambdas that capture local variables allocate a closure object on every call. In loops or frequently-called methods, extract the lambda to a static method or cache the delegate. |
| **Place cheap checks before expensive ones** | In validation chains, test simple conditions (null checks, boolean flags) before allocating strings or doing I/O. Short-circuit with `&&`/`||`. |
| **Cache repeated accessor calls** | If `foo.Bar.Baz` is used multiple times in a block, assign it to a local. This avoids repeated property evaluation and makes intent clearer. |
| **Watch for O(n²)** | Nested loops over the same or related collections, repeated `.Contains()` on a `List<T>`, or LINQ `.Where()` inside a loop are O(n²). Switch to `HashSet<T>` or `Dictionary<TK, TV>` for lookups. |
| **Extract throw helpers** | Code like `if (x) throw new SomeException(...)` in a frequently-called method prevents inlining. Extract into a `[DoesNotReturn]` helper so the JIT can inline the happy path. |

---

## Code Organization

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
