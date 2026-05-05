# C# Review Rules

General C# guidance applicable to any .NET repository.

---

## Nullable Reference Types

| Check | What to look for |
|-------|-----------------|
| **`#nullable enable`** | New files should have `#nullable enable` at the top — unless nullable is already enabled at the project level via the `Nullable` MSBuild property, in which case it is not needed per-file. |
| **Never use `!` (null-forgiving operator)** | The postfix `!` null-forgiving operator (e.g., `foo!.Bar`) is banned. If the value can be null, add a proper null check. If it can't be null, make the type non-nullable. AI-generated code frequently sprinkles `!` to silence warnings — this turns compile-time safety into runtime `NullReferenceException`s. Note: this rule is about the postfix `!` operator, not the logical negation `!` (e.g., `if (!someBool)` or `if (!string.IsNullOrEmpty (s))`). |

---

## Async, Cancellation & Thread Safety Patterns

| Check | What to look for |
|-------|-----------------|
| **CancellationToken propagation** | Every `async` method that accepts a `CancellationToken` must pass it to ALL downstream async calls. A token that's accepted but never used is a broken contract. |
| **OperationCanceledException** | Catch-all blocks (`catch (Exception)`) must NOT swallow `OperationCanceledException`. Catch it explicitly first and rethrow, or use a type filter. |
| **Honor the token** | If a method accepts `CancellationToken`, it must observe it — register a callback to kill processes, check `IsCancellationRequested` in loops, pass it downstream. Don't accept it just for API completeness. |
| **Thread safety of shared state** | If a new field or property can be accessed from multiple threads (e.g., static caches, event handlers), verify thread-safe access: `ConcurrentDictionary`, `Interlocked`, or explicit locks. A `Dictionary<K,V>` read concurrently with a write is undefined behavior. |
| **Lock ordering** | If code acquires multiple locks, the order must be consistent everywhere. Document the ordering. Inconsistent ordering → deadlock. |
| **Avoid double-checked locking — use `Lazy<T>` or `LazyInitializer`** | The double-checked locking (DCL) pattern is error-prone and [discouraged by Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile). Prefer `Lazy<T>` or `LazyInitializer.EnsureInitialized()`. If DCL is truly necessary, verify all initialization completes before the field is assigned and no thread can observe a partially-initialized instance. |
| **Singleton initialization completeness** | When a singleton is initialized behind a lock, ensure ALL setup steps (not just construction) complete before publishing the instance. If `Initialize()` does `instance = new Foo(); instance.Setup();`, another thread can see `instance != null` and use it before `Setup()` runs. |

---

## Error Handling

| Check | What to look for |
|-------|-----------------|
| **No empty catch blocks** | Every `catch` must capture the `Exception` and log it (or rethrow). No silent swallowing. |
| **Validate parameters** | Enum parameters and string-typed "mode" values must be validated — throw `ArgumentException` or `NotSupportedException` for unexpected values. |
| **Fail fast on critical ops** | If a critical operation fails, throw immediately. Silently continuing leads to confusing downstream failures. |
| **Check process exit codes** | If one operation checks the process exit code, ALL similar operations must too. Inconsistent error checking creates a false sense of safety. |
| **Log messages must have context** | A bare `"Operation failed"` could be anything. Include *what* you were doing and relevant identifiers. |
| **Differentiate similar error messages** | Two messages saying `"X failed"` for different operations are impossible to debug. Make each unique. |
| **Assert boundary invariants** | If a name=value pair array must have even length, assert `(length % 2) == 0` before indexing `[i+1]`. |
| **Include actionable details in exceptions** | Use `nameof` for parameter names. Include the unsupported value or unexpected type. Never throw empty exceptions. |
| **Initialize output parameters in all paths** | Methods with `out` parameters must initialize them in all error paths, not just the success path. |
| **Challenge exception swallowing** | When a PR adds `catch { continue; }` or `catch { return null; }`, question whether the exception is truly expected or masking a deeper problem. The default should be to let unexpected exceptions propagate. |

---

## Performance

| Check | What to look for |
|-------|-----------------|
| **Avoid unnecessary allocations** | Don't create intermediate collections when LINQ chaining or a single list would do. Char arrays for `string.Split()` should be `static readonly` fields. |
| **ArrayPool for large buffers** | Buffers ≥ 1 KB should use `ArrayPool<byte>.Shared.Rent()` with `try`/`finally` return. Large allocations go to the LOH and are expensive to GC. |
| **`HashSet.Add()` already handles duplicates** | Calling `.Contains()` before `.Add()` does the hash lookup twice. Just call `.Add()`. |
| **Don't wrap a value in an interpolated string** | `$"{someString}"` creates an unnecessary `string.Format` call when `someString` is already a string. |
| **Pre-allocate collections when size is known** | Use `new List<T>(capacity)` or `new Dictionary<TK, TV>(count)` when the size is known or estimable. Repeated resizing is O(n) allocation waste. |
| **Avoid closures in hot paths** | Lambdas that capture local variables allocate a closure object on every call. In loops or frequently-called methods, extract the lambda to a static method or cache the delegate. |
| **Place cheap checks before expensive ones** | In validation chains, test simple conditions (null checks, boolean flags) before allocating strings or doing I/O. Short-circuit with `&&`/`||`. |
| **Cache repeated accessor calls** | If `foo.Bar.Baz` is used multiple times in a block, assign it to a local. This avoids repeated property evaluation and makes intent clearer. |
| **Watch for O(n²)** | Nested loops over the same or related collections, repeated `.Contains()` on a `List<T>`, or LINQ `.Where()` inside a loop are O(n²). Switch to `HashSet<T>` or `Dictionary<TK, TV>` for lookups. |
| **Extract throw helpers** | Code like `if (x) throw new SomeException(...)` in a frequently-called method prevents inlining. Extract into a `[DoesNotReturn]` helper so the JIT can inline the happy path. |
| **`Split()` with count parameter** | `line.Split (new char[]{'='}, 2)` prevents values containing `=` from being split incorrectly. Follow existing patterns. |

---

## Code Organization

| Check | What to look for |
|-------|-----------------|
| **One type per file** | Each public class, struct, enum, or interface must be in its own `.cs` file named after the type. |
| **Use `record` for data types** | Immutable data-carrier types should be `record` types — they get value equality, `ToString()`, and deconstruction for free. |
| **Remove unused code** | Dead methods, speculative helpers, and code "for later" should be removed. Ship only what's needed. No commented-out code — Git has history. |
| **New helpers default to `internal`** | New utility methods should be `internal` unless a confirmed external consumer needs them. Use `InternalsVisibleTo` for test access. |
| **Use interfaces over concrete types** | Fields and parameters should prefer interfaces (`IMetadataResolver`) over concrete classes. When the implementation changes, you swap the implementation — not every call site. |
| **Reduce indentation with early returns** | `foreach (var x in items ?? [])` eliminates a null-check nesting level. Invert logic for the common case with `continue` so complex cases have less nesting. |
| **Don't initialize fields to default values** | `bool flag = false;` and `int count = 0;` are noise. The CLR zero-initializes all fields. Only assign when the initial value is non-default. |
| **`sealed` classes skip full Dispose** | A `sealed` class doesn't need `Dispose(bool)` + `GC.SuppressFinalize`. Just implement `IDisposable.Dispose()` directly. The full pattern is only for unsealed base classes. |
| **Well-named constants over magic numbers** | `if (retryCount > 3)` should be `if (retryCount > MaxRetries)`. Constants document intent and make the value easy to find and change. |
