# Native Code (C/C++) Review Rules

The native runtime (`src/native/`, historically `src/monodroid/`) is critical
path code running on every Android device. Bugs here cause crashes, memory
leaks, and security vulnerabilities that are extremely hard to diagnose
remotely.

---

## Memory Management

| Check | What to look for |
|-------|-----------------|
| **Every `new` needs a `delete` or justification** | If a `new` has no matching cleanup, document *why* the leak is acceptable and its worst-case size. "Small leak" is not a justification without quantifying "how small" and "how often." (Postmortem `#11`) |
| **Quantify leaks** | Is the leaked path hit once per assembly resolution (dozens of times) or once per P/Invoke invocation (millions)? The answer determines whether a leak matters. (Postmortem `#12`) |
| **Document known leaks in commit messages** | If a small leak is deliberately accepted, say so in the commit message so reviewers don't rediscover it later. (Postmortem `#13`) |
| **Watch for leaks in external APIs** | Functions like `mono_guid_to_string()` allocate memory that the caller must free. Check the docs for every external API call. (Postmortem `#14`) |
| **Use RAII (`std::unique_ptr`, etc.)** | If a library can be unloaded or an object has a clear owner, use smart pointers or RAII to ensure cleanup. Don't rely on manual `delete`. (Postmortem `#15`) |
| **Stack memory adds up on Android** | Android threads can have only 2–4 KB of stack. A struct with 88 bytes of wrappers is non-trivial on the stack. Make sentinel/invalid instances `static` to avoid per-instance overhead. (Postmortem `#43`) |

---

## C++ Best Practices

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

---

## Symbol Visibility & Naming

| Check | What to look for |
|-------|-----------------|
| **Use `-fvisibility=hidden` by default** | Only export symbols that are explicitly needed. If a native function isn't called from managed code or another library, it shouldn't be exported. (Postmortem `#30`) |
| **Question every exported symbol** | Search GitHub for actual usage before keeping an exported function. If nothing outside `src/native/` calls it, make it internal. (Postmortem `#27`) |
| **Document cross-references for exports** | Add comments with direct links to callers (e.g., the Mono BCL line that P/Invokes the function). When the caller changes, it's clear the export can be removed. (Postmortem `#28`) |
| **Remove dead symbols proactively** | When an upstream consumer (e.g., a Mono branch) no longer uses a function, remove it now. Don't wait for "someday." (Postmortem `#29`) |
| **Avoid "monodroid" in new filenames** | The runtime libraries use `libmono-android*` names. Keep new files consistent. (Postmortem `#1`) |

---

## Platform-Specific Code

| Check | What to look for |
|-------|-----------------|
| **Prefer `W` (wide) Win32 functions** | Use `GetModuleHandleExW` not `GetModuleHandleEx` (the macro). Avoid the `A` (ANSI) variants entirely. (Postmortem `#23`) |
| **Don't change platform-guarded code unnecessarily** | If a change is in a `#if defined(WINDOWS)` block, verify it's actually needed on that platform. (Postmortem `#26`) |
| **Check return codes on all platform APIs** | Even APIs that "shouldn't fail" (like `PathRemoveFileSpec`) have return values. Check them. (Postmortem `#8`) |

---

## Build & ABI

| Check | What to look for |
|-------|-----------------|
| **CMake** | Native code uses CMake. Changes must build for all ABIs: `arm64-v8a`, `armeabi-v7a`, `x86_64`, `x86`. |
| **API bindings** | Use `[Register]` attributes. Follow `Android.*` namespace patterns. |
