# Native Code (C/C++) Review Rules

The native interop layer (`src/java-interop/`) provides low-level JNI and
platform integration. Bugs here cause crashes and memory leaks that are
extremely hard to diagnose.

---

## Memory Management

| Check | What to look for |
|-------|-----------------|
| **Every `new` needs a `delete` or justification** | If a `new` has no matching cleanup, document *why* the leak is acceptable and its worst-case size. |
| **Quantify leaks** | Is the leaked path hit once per process lifetime or once per invocation? The answer determines whether a leak matters. |
| **Use RAII (`std::unique_ptr`, etc.)** | Use smart pointers or RAII to ensure cleanup. Don't rely on manual `delete`. |
| **Watch for leaks in external APIs** | Functions that allocate memory for the caller must have their return values freed. Check the docs for every external API call. |

---

## C++ Best Practices

| Check | What to look for |
|-------|-----------------|
| **`nullptr` over `NULL`** | `NULL` is `0` in C++, which can silently convert to integral types. `nullptr` has proper pointer semantics. Use `!= nullptr` consistently in null checks. |
| **Use C++ standard headers** | Prefer `<cstdlib>` over `<stdlib.h>`, `<cstring>` over `<string.h>`, etc. The C++ headers place names in `std::`. |
| **Virtual destructor on base classes** | Any base class with virtual methods must have a public virtual destructor. Without one, `delete`-through-base-pointer is undefined behavior. |
| **Delete copy/move constructors when inappropriate** | Types holding non-copyable resources (JNI references, file handles) must use `= delete` on copy constructor and assignment operator. |
| **Prefer `private` over `protected`** | Unless the type is explicitly designed for subclassing, use `private`. Don't speculatively make things `protected`. |
| **Use `const` where possible** | If a parameter or function argument isn't modified, declare it `const`. |
| **Handle `EINTR` for system calls** | `read()`, `write()`, and other syscalls can return `EINTR` when interrupted by a signal. Retry in a loop. |
| **Use `sizeof()` not magic numbers** | `16` should be `sizeof(some_type)` or equivalent. Magic numbers make code fragile. |
| **`static_cast` over C-style casts** | `static_cast<int>(val)` is checked at compile time. `(int)val` can silently reinterpret bits. |
| **No commented-out code** | If it's not needed, delete it. Git has history. |
| **Don't use compiler-reserved identifiers** | Double-underscore `__` prefixed names are reserved by the C/C++ standard. |
| **Reasonable line width** | Don't combine two 80-char lines into one 160-char line. Keep code readable. |
