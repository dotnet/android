# Managed ↔ Native Interop Review Rules

Rules for the boundary between C# and C/C++ code — P/Invoke declarations, JNI
bindings, and shared structs. Load when both managed and native files change, or
when the diff contains interop markers (`DllImport`, `[Register]`, `JNIEnv`,
`[MarshalAs]`, `[StructLayout]`).

---

## Interop Checks

| Check | What to look for |
|-------|-----------------|
| **`static_cast` over C-style casts** | `static_cast<int>(val)` is checked at compile time. `(int)val` can silently reinterpret bits. Always use C++ casts in interop boundaries. |
| **`nullptr` over `NULL`** | `NULL` is `0` in C++, which can silently convert to integral types. `nullptr` has proper pointer semantics. |
| **Struct field ordering for padding** | When defining structs shared between managed and native code, order fields largest-to-smallest to minimize padding. Explicit `[StructLayout(LayoutKind.Sequential)]` and matching C struct must be kept in sync. |
| **Bool marshalling** | Boolean marshalling is a common source of bugs. C++ `bool` is 1 byte, Windows `BOOL` is 4 bytes. When P/Invoking, explicitly specify `[MarshalAs(UnmanagedType.U1)]` or `[MarshalAs(UnmanagedType.Bool)]` (4-byte). |
| **String marshalling charset** | P/Invoke string parameters should specify `CharSet.Unicode` (UTF-16) or use `[MarshalAs(UnmanagedType.LPUTF8Str)]` for UTF-8. Don't rely on the default (ANSI on Windows). |
