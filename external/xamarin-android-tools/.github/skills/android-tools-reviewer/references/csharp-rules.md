# C# Review Rules

General C# and .NET guidance for code reviews. Applicable to any .NET repository.

---

## 1. Target Framework Compatibility

When a library multi-targets (e.g., `netstandard2.0` + a modern TFM), every API call must work on all targets.

| Check | What to look for |
|-------|-----------------|
| **netstandard2.0 API surface** | Methods/overloads that only exist on net5+. Common traps: `HttpContent.ReadAsStringAsync(CancellationToken)`, `ProcessStartInfo.ArgumentList`, `Environment.IsPrivilegedProcess`, `ArrayPool<T>` (needs `System.Buffers` package on ns2.0). When unsure, check MS Learn docs. |
| **C# language features** | `init` accessors, `required` keyword, file-scoped types, raw string literals — may need polyfills or `#if` guards. |
| **Conditional compilation** | New API usage should be behind `#if NET5_0_OR_GREATER` (or similar) with a fallback for netstandard2.0. |

---

## 2. Async & Cancellation Patterns

| Check | What to look for |
|-------|-----------------|
| **CancellationToken propagation** | Every `async` method that accepts a `CancellationToken` must pass it to ALL downstream async calls (`GetAsync`, `ReadAsStreamAsync`, `SendAsync`, etc.). A token that's accepted but never used is a broken contract. |
| **OperationCanceledException** | Catch-all blocks (`catch (Exception)`) must NOT swallow `OperationCanceledException`. Either catch it explicitly first and rethrow, or use a type filter. |
| **GetStringAsync** | On netstandard2.0, `GetStringAsync(url)` doesn't accept a `CancellationToken`. Use `GetAsync(url, ct)` + `ReadAsStringAsync()` instead. |
| **Thread safety of shared state** | If a new field or property can be accessed from multiple threads (e.g., static caches, event handlers), verify thread-safe access: `ConcurrentDictionary`, `Interlocked`, or explicit locks. A `Dictionary<K,V>` read concurrently with a write is undefined behavior. |
| **Lock ordering** | If code acquires multiple locks, the order must be consistent everywhere. Document the ordering. Inconsistent ordering → deadlock. |
| **Avoid double-checked locking** | The double-checked locking (DCL) pattern is error-prone and [discouraged by Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile). Prefer `Lazy<T>` or `LazyInitializer.EnsureInitialized()` — they handle thread-safe initialization correctly. If DCL is truly necessary (e.g., for performance in a hot path with evidence), verify all initialization completes before the field is assigned and no thread can observe a partially-initialized instance. |
| **Singleton initialization completeness** | When a singleton is initialized behind a lock, ensure ALL setup steps (not just construction) complete before publishing the instance. If `Initialize()` does `instance = new Foo(); instance.Setup();`, another thread can see `instance != null` and use it before `Setup()` runs. Either do all setup in the constructor, or don't publish the reference until all setup is done. |

---

## 3. Resource Management

| Check | What to look for |
|-------|-----------------|
| **HttpClient must be static** | `HttpClient` instances should be `static readonly` fields, not per-instance. Creating/disposing `HttpClient` leads to socket exhaustion via `TIME_WAIT` accumulation. See [Microsoft guidelines](https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines). |
| **No HttpClient injection (YAGNI)** | Don't add `HttpClient` constructor parameters "for testability" unless a caller actually needs it today. The AI tends to over-engineer this. |
| **ArrayPool for large buffers** | Buffers ≥ 1KB (especially 80KB+ download buffers) should use `ArrayPool<byte>.Shared.Rent()` with `try/finally` return. Large allocations go to the LOH and are expensive to GC. |
| **IDisposable** | Classes that own unmanaged resources or expensive managed resources must implement `IDisposable` with a dispose guard (`ThrowIfDisposed`). |

---

## 4. Error Handling

| Check | What to look for |
|-------|-----------------|
| **No empty catch blocks** | Every `catch` must capture the `Exception` and log it (or rethrow). No silent swallowing. Even "expected" exceptions should be logged for diagnostics. |
| **Validate parameters** | Enum parameters and string-typed "mode" values must be validated — throw `ArgumentException` or `NotSupportedException` for unexpected values. Don't silently accept garbage. |
| **Fail fast on critical ops** | If an operation like `chmod` or checksum verification fails, throw immediately. Silently continuing leads to confusing downstream failures. |
| **Mandatory verification** | Checksum/hash verification must NOT be optional. If the checksum can't be fetched, the operation must fail — not proceed unverified. |
| **Include actionable details in exceptions** | Use `nameof` for parameter names. Include the unsupported value or unexpected type. Never throw empty exceptions. |
| **Log messages must have context** | A bare `"Operation failed"` could be anything. Include *what* you were doing and relevant identifiers so the message is actionable without a debugger. |
| **Differentiate similar error messages** | Two messages saying `"X failed"` for different operations are impossible to debug. Make each unique so log output is unambiguous. |
| **Assert boundary invariants** | If a name=value pair array must have even length, assert `(length % 2) == 0` before indexing `[i+1]`. Validate structural assumptions at the boundary, not deep inside the logic. |
| **Initialize output parameters in all paths** | Methods with `out` parameters must initialize them in all error paths, not just the success path. |
| **Use `ThrowIf` helpers where available** | Prefer `ArgumentOutOfRangeException.ThrowIfNegative`, `ArgumentNullException.ThrowIfNull`, etc. over manual if-then-throw patterns where these helpers are available. In `netstandard2.0` projects where these helpers are unavailable, use explicit checks such as `if (x is null) throw new ArgumentNullException (nameof (x));`. |
| **Challenge exception swallowing** | When a PR adds `catch { continue; }` or `catch { return null; }`, question whether the exception is truly expected or masking a deeper problem. The default should be to let unexpected exceptions propagate. |

---

## 5. Performance

| Check | What to look for |
|-------|-----------------|
| **Reuse hot-path buffers** | Pool repeated `byte[]` allocations with `ArrayPool<byte>.Shared` (see `DownloadUtils.cs`). For fixed-size repeated reads, use a `readonly byte[]` instance field. Document non-thread-safety in `<remarks>`. |
| **Reuse loop helpers** | If a loop creates resettable helpers (e.g., `TcpClient`), reuse one instance via `Reconnect()`/`Reset()` instead of `new` per iteration. |
| **XmlReader over LINQ XML** | For forward-only XML parsing (manifests, config files), use `XmlReader` — it's streaming and allocation-free. `XElement`/`XDocument` builds a full DOM tree. |
| **p/invoke over process spawn** | For single syscalls like `chmod`, use `[DllImport("libc")]` instead of spawning a child process. Process creation is orders of magnitude more expensive. |
| **Avoid intermediate collections** | Don't create two lists and `AddRange()` one to the other. Build a single list, or use LINQ to chain. |
| **Cache reusable arrays** | Char arrays for `string.Split()` (like whitespace chars) should be `static readonly` fields, not allocated on each call. |
| **Pre-allocate collections when size is known** | Use `new List<T>(capacity)` or `new Dictionary<TK, TV>(count)` when the size is known or estimable. Repeated resizing is O(n) allocation waste. |
| **Avoid closures in hot paths** | Lambdas that capture local variables allocate a closure object on every call. In loops or frequently-called methods, extract the lambda to a static method or cache the delegate. |
| **Place cheap checks before expensive ones** | In validation chains, test simple conditions (null checks, boolean flags) before allocating strings or doing I/O. Short-circuit with `&&`/`||`. |
| **Watch for O(n²)** | Nested loops over the same or related collections, repeated `.Contains()` on a `List<T>`, or LINQ `.Where()` inside a loop are O(n²). Switch to `HashSet<T>` or `Dictionary<TK, TV>` for lookups. |
| **`HashSet.Add()` already handles duplicates** | Calling `.Contains()` before `.Add()` does the hash lookup twice. Just call `.Add()` — it returns `false` if the item already exists. |
| **Don't wrap a value in an interpolated string** | `$"{someString}"` creates an unnecessary `string.Format` call when `someString` is already a string. Just use `someString` directly. |
| **`Split()` with count parameter** | `line.Split(new char[]{'='}, 2)` prevents values containing `=` from being split incorrectly. Follow existing patterns that limit the split count. |
| **Cache repeated accessor calls** | If `foo.Bar.Baz` is used multiple times in a block, assign it to a local. This avoids repeated property evaluation and makes intent clearer. |
| **Extract throw helpers** | Code like `if (x) throw new SomeException(...)` in a frequently-called method prevents inlining. Extract into a `[DoesNotReturn]` helper so the JIT can inline the happy path. |

---

## 6. Code Organization

| Check | What to look for |
|-------|-----------------|
| **File-scoped namespaces** | New files should use `namespace Foo;` (not `namespace Foo { ... }`). Don't reformat existing files. |
| **Use `record` for data types** | Immutable data-carrier types (progress, version info, license info) should be `record` types. They get value equality, `ToString()`, and deconstruction for free. |
| **Document thread-safety invariants** | When a class reuses buffers, caches, or mutable state assuming single-caller semantics, add `<remarks>This class is not thread-safe.</remarks>` to the type. Callers need to know if external synchronization is required. |
| **Remove unused code** | Dead methods, speculative helpers, and code "for later" should be removed. Ship only what's needed. |
| **No commented-out code** | Commented-out code should be deleted — Git has history. |
| **Reduce indentation with early returns** | Invert conditions with `continue` or early `return` to reduce nesting. `foreach (var x in items ?? Array.Empty<T>())` eliminates null-check nesting. |
| **Don't initialize fields to default values** | `bool flag = false;` and `int count = 0;` are noise. The CLR zero-initializes all fields. Only assign when the initial value is non-default. |
| **`sealed` classes skip full Dispose** | A `sealed` class doesn't need `Dispose(bool)` + `GC.SuppressFinalize`. Just implement `IDisposable.Dispose()` directly. The full pattern is only for unsealed base classes. |
| **Use interfaces over concrete types** | Fields and parameters should prefer interfaces (`IMetadataResolver`) over concrete classes. When the implementation changes, you swap the implementation — not every call site. |

---

## 7. API Design

| Check | What to look for |
|-------|-----------------|
| **Return `IReadOnlyList<T>` not `List<T>`** | Public methods should return `IReadOnlyList<T>` (or `IReadOnlyCollection<T>`) instead of mutable `List<T>`. Prevents callers from mutating internal state. |
| **New helpers default to `internal`** | New utility methods should be `internal` unless a confirmed external consumer needs them. Use `InternalsVisibleTo` for test access. |
| **Structured args, not string interpolation** | Additional arguments to processes should be `IEnumerable<string>`, not a single `string` that gets interpolated. |
| **Honor `CancellationToken`** | If a method accepts a `CancellationToken`, it MUST observe it — register a callback to kill processes, check `IsCancellationRequested` in loops, pass it to downstream async calls. Don't just accept it for API completeness. |
| **Add overloads to reduce caller ceremony** | If every caller performs the same conversion before calling a method, the method should have an overload that accepts the unconverted type directly. |
| **Prefer C# pattern matching** | Use `is`, `switch` expressions, and property patterns instead of `if`/`else` type-check chains. Pattern matching is more concise, avoids casts, and enables exhaustiveness checks. |
