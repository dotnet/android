# Repo Conventions

Formatting, style, and patterns specific to the dotnet/java-interop repository.
Always loaded during reviews.

---

## Formatting & Style

This project uses Mono style with tabs. Formatting violations create noisy diffs
and merge conflicts.

| Check | What to look for |
|-------|-----------------|
| **Tabs, not spaces** | Indentation must use tabs (width 8 in `.editorconfig`). MSBuild/XML files use 2 spaces. |
| **Space before `(` and `[`** | Method calls: `Foo ()`, `Bar (1, 2)`. Array access: `array [0]`. This is Mono style — omitting the space is wrong here even though it's standard elsewhere. |
| **`""` not `string.Empty`** | Use `""` for empty strings. Use `[]` not `Array.Empty<T>()` for empty arrays. |
| **No `#region`/`#endregion`** | Region directives hide code and make reviews harder. Remove them. |
| **`#else`/`#endif` comments** | Always annotate `#else` and `#endif` with the original expression: `#else // !NETCOREAPP` and `#endif // !NETCOREAPP`. |
| **Minimal diffs** | Don't leave random empty lines. Preserve existing formatting and comments in files you didn't write. Only format code you add or modify; never reformat existing lines. |
| **Reasonable line width** | Max 180 characters per `.editorconfig`. Don't merge two lines into a single 160-character monster. |
| **Attributes on their own line** | Long attributes like `[DynamicallyAccessedMembers (...)]` should be on their own line, not inline with the method/parameter declaration. Each parameter with long attributes should get its own line. |
| **Named parameters for many-argument calls** | When calling a method with more than 4 parameters, use named parameters for clarity. |

---

## Naming

| Check | What to look for |
|-------|-----------------|
| **Disambiguate Java vs C# names** | Terms like `FullName`, `Name`, or `ReferenceType` are ambiguous in a JNI interop context. When both Java and C# interpretations exist, prefer a prefix (`JavaFullName`, `ManagedFullName`) or clear documentation. |
| **Method names must reflect behavior** | If `CreateFoo()` sometimes returns an existing instance, rename it `GetOrCreateFoo()` or `GetFoo()`. |
| **Named constants for string lengths** | `n.Length - 7` should be `n.Length - "Invoker".Length`. Magic numbers for string suffix lengths are fragile and unreadable. |
| **No magic numbers** | Literal values like buffer sizes and permission masks should be named constants. `sizeof()` in native code, `const` or `static readonly` in C#. |
| **`KeyedCollection` for name-indexed lists** | When a `List<T>` is frequently searched by a name property, consider `KeyedCollection<string, T>` or `Dictionary<string, T>` for O(1) lookups. |

---

## Error Messages & Localization

| Check | What to look for |
|-------|-----------------|
| **Error/warning codes are required** | User-facing errors/warnings in generator and other tools must have codes (e.g., `BG####`, `JM####`). Error codes must not collide with existing ones. |
| **Error messages must be actionable** | Tell the user what to do, not just what went wrong. "Unable to find type" → "Unable to find type '…'. Make sure the paths to all referenced assemblies are provided with the `-L` option." |
| **Demote non-actionable messages** | If a warning can't suggest an action, demote it to informational. Warnings that users can't act on are noise. |
| **Localization resource comments** | Include comments explaining what terms should NOT be translated: `The following terms should not be translated: Metadata.xml, -L.` |
| **Don't use internal jargon in messages** | Terms like "Cecil" or "corlib" are meaningless to users. Use "referenced assemblies" or "mscorlib" instead. |
| **Error messages in resource files** | New error/warning messages should be added to `.resx` resource files and referenced via `Properties.Resources`, not hard-coded in C# strings. |

---

## JNI Interop Patterns

| Check | What to look for |
|-------|-----------------|
| **JNI reference lifecycle** | Every `JniObjectReference` must be properly disposed. Use `try`/`finally` or `using` patterns. Local references are cleaned up by the JVM at JNI frame boundaries, but explicit cleanup prevents native reference table exhaustion. |
| **Use `JniTransition` for exception marshaling** | Native callbacks into managed code should use `JniTransition` to properly handle exception marshaling between Java and C#. |
| **`JniPeerMembers` caching** | Method and field IDs should be accessed via `JniPeerMembers` for efficient caching. Don't look up method IDs on every call. |
| **Thread-local JNI environments** | JNI environments are thread-local. Always use `JniEnvironment.Current` to access the current thread's environment. Don't cache `JNIEnv*` across threads. |
| **`[Register]` attribute correctness** | `[Register]` attributes must match the Java method signature exactly. Mismatches cause runtime `NoSuchMethodError`. |

---

## Performance

| Check | What to look for |
|-------|-----------------|
| **Startup time is critical** | Changes to `Java.Interop` core affect every .NET Android app startup. Minimize type loading, lazy-initialize where possible, avoid unnecessary allocations in hot paths. |
| **Prefer arrays over dictionaries for small lookups** | For datasets with < ~50 items, linear search through an array is faster than dictionary lookup due to hash computation overhead and initialization cost. Back assertions with benchmarks. |
| **`StringComparison.Ordinal` for identifiers** | Use `StringComparison.Ordinal` for Java/C# identifier comparisons. `string.EndsWith(string)` without a `StringComparison` is locale-aware and slower. Use `StringComparison.OrdinalIgnoreCase` only for filesystem paths. |
| **Static `readonly` for reusable fields** | Static fields like singletons or shared instances should be `readonly` when they shouldn't be reassigned. `static HttpClient` instances are mandatory (no per-use disposal). |
| **Consider trimmer/NativeAOT impact** | Use `[DynamicallyAccessedMembers]` for types accessed via reflection. Use `[UnconditionalSuppressMessage]` instead of `#pragma` for trimmer warnings. Avoid `Type.GetType()` with assembly-qualified names when a direct type reference or typemap lookup can be used. |

---

## Downstream Impact

| Check | What to look for |
|-------|-----------------|
| **Consider dotnet/android consumers** | Changes to shared types (`JniPeerMembers`, `JavaObject`, `JniRuntime`, `JniTypeManager`) affect dotnet/android. API changes should be validated with a draft downstream PR. |
| **Port, don't rewrite** | If dotnet/android already has working logic for the same task, port it rather than writing new code. Existing code has real-world edge cases already handled. |
| **Target framework compatibility** | Tools shipped to customers (e.g., `generator`, `class-parse`) must target the correct .NET version. Verify against the oldest supported target framework. |

---

## Patterns & Conventions

| Check | What to look for |
|-------|-----------------|
| **Comments explain "why", not "what"** | `// increment i` adds nothing. `// skip the BOM — XmlReader chokes on it` explains intent. If a comment restates the code, delete it. |
| **Track TODOs as issues** | A `// TODO` hidden in code will be forgotten. File an issue and reference it in the comment. |
| **Remove stale comments** | If the code changed, update the comment. Comments that describe old behavior are misleading. |
| **Use existing utilities** | Check for existing helpers before writing new ones. Duplicating existing logic is the most expensive AI pattern. |
| **Return `IReadOnlyList<T>`** | Public methods should return `IReadOnlyList<T>` or `IReadOnlyCollection<T>` instead of mutable `List<T>`. |
| **Prefer C# pattern matching** | Use `is`, `switch` expressions, and property patterns instead of `if`/`else` type-check chains. |
| **Use `record` for data types** | Immutable data-carrier types should be `record` types — they get value equality, `ToString()`, and deconstruction for free. |
