# Code Review Post-Mortem

Findings from @jonpryor's reviews on dotnet/android PRs
[#4914](https://github.com/dotnet/android/pull/4914),
[#6427](https://github.com/dotnet/android/pull/6427),
[#2515](https://github.com/dotnet/android/pull/2515),
[#3992](https://github.com/dotnet/android/pull/3992), and
[#5748](https://github.com/dotnet/android/pull/5748) — 170 review comments
distilled into actionable lessons.

---

## 1. Naming & Conventions

| # | Finding | PR |
|---|---------|-----|
| 1 | **Avoid "monodroid" in new filenames.** The runtime libraries use `libmono-android*` names; keep new files consistent. Historically `monodroid` leaked into filenames but we've been removing it. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r453797657) |
| 2 | **Choose names that can't collide.** A constant named `"Xamarin.Android.Resource.Designer"` could plausibly collide with real assemblies — prefix with `__` or use a name impossible for user code. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037368329) |
| 3 | **Don't use compiler-reserved identifiers.** Double-underscore `__` prefixed names are reserved by the C/C++ standard — use `_monodroid_` instead. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r453799320) |
| 4 | **Method names should reflect behavior.** If `CreateStaticCtor()` sometimes returns an *existing* `.cctor` instead of creating one, rename it `GetStaticCtor()`. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037476302) |
| 5 | **Signed vs. unsigned matters in docs.** When documenting binary formats, explicitly state "unsigned 32-bit integer" — not just "32-bit integer." | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r367068478) |

---

## 2. Error Messages & Logging

| # | Finding | PR |
|---|---------|-----|
| 6 | **Log messages must have context.** A bare `"GetModuleHandle failed"` could be triggered by anything. Include *what* you were trying to do: `"Unable to get HANDLE to libmono-android.debug.dll; GetModuleHandle returned %d"`. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460151533) |
| 7 | **Differentiate similar error messages.** Two messages saying `"X failed"` for different operations are impossible to debug. Make each unique. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460153301) |
| 8 | **Check return codes even for "infallible" APIs.** `PathRemoveFileSpec` returns `BOOL` — check it. Document *why* if you intentionally ignore a return value. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460153850) |
| 9 | **Don't spam logcat.** If a message fires on every type lookup miss (which can be thousands), guard it behind a verbosity flag. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376195244) |
| 10 | **Error codes need documentation.** Every new `XA####` error needs a docs entry and should be localized per existing patterns. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376198743) |

---

## 3. Memory Management (Native Code)

| # | Finding | PR |
|---|---------|-----|
| 11 | **Every `new` needs a corresponding `delete` (or justification).** If a `new` has no matching cleanup, document why the leak is acceptable and its worst-case size. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r453927525) |
| 12 | **Quantify leaks.** Is the leaked path hit once per assembly resolution (dozens of times) or once per P/Invoke invocation (millions)? The answer determines whether the leak matters. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r454027874) |
| 13 | **Commit messages should document known leaks.** If a small leak is deliberately accepted, say so in the commit message so reviewers don't rediscover it later. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460159997) |
| 14 | **Watch for leaks in Mono APIs.** `mono_guid_to_string()` allocates memory that must be freed. Check the docs for every external API call. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376203547) |
| 15 | **Consider `std::unique_ptr` for cleanup.** If a library can be unloaded, use RAII to ensure destructors run. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r454734712) |

---

## 4. C++ Best Practices

| # | Finding | PR |
|---|---------|-----|
| 16 | **Base classes with virtual methods need a virtual destructor.** Without one, `delete`-through-base-pointer is undefined behavior. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r454032688) |
| 17 | **Delete copy constructors and assignment operators** for types that hold non-copyable resources (`= delete`). | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240863043) |
| 18 | **Prefer `private` over `protected`** unless the type is designed for subclassing. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240863043) |
| 19 | **Use `const` where possible.** If a JNI parameter isn't modified, declare it `const jstring`. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240863093) |
| 20 | **Follow STL naming conventions.** Collection wrappers should use `size()` not `length()` for consistency with `std::vector`. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240863977) |
| 21 | **Make singleton instances `static`** to avoid wasting per-instance memory on sentinel objects. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245189056) |
| 22 | **Handle `EINTR` for system calls.** `read()` can return `EINTR`; retry in a loop. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r367076212) |

---

## 5. Platform & API Usage

| # | Finding | PR |
|---|---------|-----|
| 23 | **Prefer `W` (wide) Win32 functions over `A` (ANSI) or macros.** Use `GetModuleHandleExW`, not `GetModuleHandleEx`. Avoid the undecorated macro forms. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460148940) |
| 24 | **Use `Guid.TryWriteBytes(Span<byte>)` when available.** Avoids array allocation. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r367061610) |
| 25 | **Consider data format implications.** UTF-8 Java type names require allocation for comparison; UTF-16 would match `jchar` natively and avoid runtime conversion — even though it doubles the data size. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r367070590) |
| 26 | **Don't introduce platform-specific changes unless needed on that platform.** Check whether a code change in a `#if defined(WINDOWS)` block is actually necessary. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245188054) |

---

## 6. Symbol Visibility & API Surface

| # | Finding | PR |
|---|---------|-----|
| 27 | **Question every exported symbol.** If a native function isn't called from managed code or another library, why is it exported? Search GitHub for actual usage before keeping it. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r456465023) |
| 28 | **Document cross-references for exported symbols.** Add comments with direct links to callers (e.g., the Mono BCL line that P/Invokes the function). When the caller changes, it's clear the export can be removed. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460138357) |
| 29 | **Remove dead symbols proactively.** When a Mono branch no longer uses a function, remove it. Don't wait for "someday." | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r460139588) |
| 30 | **Use `-fvisibility=hidden` by default.** Only export symbols that are explicitly needed. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r456135372) |

---

## 7. Formatting & Code Style

| # | Finding | PR |
|---|---------|-----|
| 31 | **Space before `(`** — Mono style: `Foo ()`, `Bar (1, 2)`. Multiple missing instances in a file compound into a noisy follow-up fix. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037478485) |
| 32 | **`{` on new line** after method/class declarations. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037471561) |
| 33 | **XML attributes: consistent indentation (2 spaces), `Condition` first.** The `Condition` attribute is the most important for debugging — put it first. Be consistent with attribute ordering. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037414806) |
| 34 | **Newlines between methods.** Don't smash methods together. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037387448) |
| 35 | **Don't mix indentation widths** (2 spaces, 3 spaces, 4 spaces) within the same file. Be consistent. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037412099) |
| 36 | **Keep lines at reasonable width.** Don't merge two 80-char lines into one 160-char line. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245189258) |

---

## 8. Conditional Compilation

| # | Finding | PR |
|---|---------|-----|
| 37 | **`#else` and `#endif` must have comments with the original expression.** `#else // !NET5_LINKER` and `#endif // !NET5_LINKER` so readers know what they're inside. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037384457), [#5748](https://github.com/dotnet/android/pull/5748#discussion_r633951632) |
| 38 | **Keep braces outside `#if` blocks.** Don't split `{` and `}` across `#if`/`#else` branches — it confuses editors and humans. Put the `{` after all the `#if`-selected base types. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r633952914) |

---

## 9. Performance & Allocations

| # | Finding | PR |
|---|---------|-----|
| 39 | **Consider allocations when choosing types.** `Stopwatch` is a heap-allocated class; `DateTime` is a struct. If allocation cost matters (hot paths, startup), prefer the struct. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240861287) |
| 40 | **Prefer inline field initialization over static constructors.** `static` blocks add a line per field; inline initialization keeps changes to 1 line each and prevents accidentally forgetting an assignment. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240995902) |
| 41 | **`HashSet.Add()` already handles duplicates.** Calling `.Contains()` before `.Add()` does the hash lookup twice. Just call `.Add()`. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376198115) |
| 42 | **Don't wrap a value in an interpolated string that *is* the value.** `$"{someString}"` creates an unnecessary `string.Format` call. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245186702) |
| 43 | **Stack memory adds up on Android.** With only 2–4 KB stacks, a struct with 88 bytes of wrappers is non-trivial. Consider making sentinel instances `static`. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245342108) |

---

## 10. Boundary Validation & Safety

| # | Finding | PR |
|---|---------|-----|
| 44 | **Assert array length invariants.** If a name=value pair array must have even length, assert `(length % 2) == 0` before indexing `[i+1]`. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245128388) |
| 45 | **Validate boundary values for managed-to-native interfaces.** If native code reads `mvid` bytes, check `mvid.Length` on the managed side first. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r367062162) |
| 46 | **Assert on "impossible" null values.** If `bsearch` returning null is impossible by construction, `assert()` and crash rather than silently returning — it means a bug elsewhere. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376203676) |
| 47 | **Watch for unsigned underflow in loops.** `size_t` is unsigned; subtraction can wrap to a huge number, causing infinite loops. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376200580) |
| 48 | **Use `sizeof()` not magic numbers.** `16` should be `sizeof(module_uuid_t)` or similar. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376203742) |

---

## 11. String Comparison

| # | Finding | PR |
|---|---------|-----|
| 49 | **`.Ordinal` is faster than `.OrdinalIgnoreCase`** — always. If the source is IL/C# identifiers (case-sensitive), use `.Ordinal`. If the source is filesystem paths, use `.OrdinalIgnoreCase`. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1040169437) |
| 50 | **`Split()` with a count parameter** prevents surprises: `line.Split(new char[]{'='}, 2)` ensures a value containing `=` isn't incorrectly split. Follow existing patterns in the codebase. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245187391) |

---

## 12. MSBuild Task Properties

| # | Finding | PR |
|---|---------|-----|
| 51 | **Mark task properties `[Required]` when they are required.** If the task crashes without a property, it should be `[Required]`. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037433398) |
| 52 | **Question path normalization.** If you normalize `\` → `/` only to normalize back later, the intermediate step is pointless. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037440968) |
| 53 | **Use `Files.CopyIfStringChanged()`.** Don't write to a file if the content hasn't changed — it breaks incremental builds by updating timestamps. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245187658) |

---

## 13. Code Organization & Duplication

| # | Finding | PR |
|---|---------|-----|
| 54 | **Centralize algorithms that appear in multiple repos.** If three different repos have "do these Cecil methods have the same parameter list?" implementations, that's a bug farm. Push common logic into shared packages. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r633947036) |
| 55 | **Introduce base types to reduce `#if` noise.** Instead of scattering `#if NET5_LINKER` in every class, create a `BaseMarkHandler` and let subclasses just override `Initialize()`. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r633955110) |
| 56 | **Use interfaces (`IMetadataResolver`) over concrete types** for fields/parameters. When the upstream implementation changes, you swap the implementation — not every call site. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r641030363) |
| 57 | **Don't remove performance caches without measurement.** `TypeDefinitionCache` had a measured perf win — removing it requires proving the replacement (e.g., `LinkContext`) provides equivalent caching. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r633948697) |

---

## 14. Code Cleanliness

| # | Finding | PR |
|---|---------|-----|
| 58 | **No commented-out code.** If it's not needed, delete it. Git has history. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r367076377) |
| 59 | **Remove stale comments.** If the code changed, update the comment. "This loads libmonodroid.so" is wrong if we now load `libxa-internal-api.so`. | [#4914](https://github.com/dotnet/android/pull/4914#discussion_r454020060) |
| 60 | **Track TODOs as issues.** A `// TODO: load` hidden in native code will be forgotten. File an issue and reference it. | [#3992](https://github.com/dotnet/android/pull/3992#discussion_r376201401) |
| 61 | **Typos in error messages matter.** Users copy-paste them into bug reports. Get them right. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r245187781) |
| 62 | **Reduce indentation with early returns.** `foreach (var x in items ?? Array.Empty<T>())` eliminates a null-check nesting level. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037441863) |
| 63 | **Invert logic for clarity.** If the common case is simpler, handle it first with `continue` so the complex case has less nesting. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037518719) |

---

## 15. Architecture & Design

| # | Finding | PR |
|---|---------|-----|
| 64 | **Don't assume transitive assembly references.** An assembly containing an `Activity` subclass does *not* necessarily reference `Mono.Android.dll` directly — the reference may be transitive. Skipping assemblies based on direct reference checks can break user code. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r641035596) |
| 65 | **Method override checking must handle IL contravariance.** `MethodDefinition.Overrides` in Cecil handles `.override` IL directives that parameter-matching alone misses. | [#5748](https://github.com/dotnet/android/pull/5748#discussion_r633943826) |
| 66 | **Document array mutability semantics.** If `Resource.Styleable.foo` returns a cached `int[]` (not a copy), callers who mutate it corrupt global state. Document "don't do that" explicitly. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037479847) |
| 67 | **Watch parameter count explosion.** A JNI `init` function with 12+ parameters is a code smell. Consider whether some parameters should be fields or a struct. | [#2515](https://github.com/dotnet/android/pull/2515#discussion_r240861629) |
| 68 | **Link third-party source to its origin.** When vendoring code (e.g., `CryptoConvert.cs` from Mono), add a comment with the URL and commit hash of the original. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037379997) |

---

## 16. Documentation & Commit Messages

| # | Finding | PR |
|---|---------|-----|
| 69 | **Commit messages should document non-obvious behavioral choices.** "Styleable array resources are cached, not copied per-access" belongs in the commit message so future readers understand the design. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1062003615) |
| 70 | **Document known limitations.** If a property *cannot* be turned off for app projects, the documentation should say so explicitly — don't make users discover it by trial and error. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037377832) |
| 71 | **Remove filler words.** "So" at the start of a sentence adds nothing. | [#6427](https://github.com/dotnet/android/pull/6427#discussion_r1037368823) |
