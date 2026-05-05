# Managed ↔ Native Interop Review Rules

Rules for the boundary between C# and C/C++ code — P/Invoke declarations, JNI
bindings, and shared structs. Load when both managed and native files change, or
when the diff contains interop markers (`JniObjectReference`, `JniPeerMembers`,
`DllImport`, `[Register]`, `JNIEnv`, `[MarshalAs]`, `[StructLayout]`).

---

## JNI Interop Checks

| Check | What to look for |
|-------|-----------------|
| **`JniObjectReference` lifecycle** | Every `JniObjectReference` obtained from JNI calls must be disposed in a `try`/`finally` block. Local references that escape their JNI frame exhaust the local reference table (default 512 entries). Global references that aren't freed are permanent leaks. |
| **`JniPeerMembers` for method/field IDs** | Method and field IDs should be cached via `JniPeerMembers.InstanceMethods`, `JniPeerMembers.StaticMethods`, or `JniPeerMembers.InstanceFields`. Looking up IDs on every call is expensive. |
| **Virtual vs non-virtual dispatch** | Default interface method implementations should use `InvokeNonvirtualVoidMethod()` (non-virtual). Class method overrides should use `InvokeVirtualVoidMethod()` (virtual). Getting this wrong changes dispatch semantics. |
| **`[Register]` attribute accuracy** | `[Register]` attributes must exactly match the Java method name and JNI signature. Mismatches cause `NoSuchMethodError` at runtime. Verify against the Java API description. |
| **`JniTransition` for native callbacks** | Entry points from Java into managed code should use `JniTransition` to properly handle exception marshaling. Without it, managed exceptions propagate into the JVM as undefined behavior. |
| **Exception checking after JNI calls** | After JNI calls that can throw (most of them), check for pending Java exceptions via `JniEnvironment.Errors.ExceptionOccurred()` or rely on the built-in checking in `JniPeerMembers` wrappers. Don't ignore JNI error return codes. |

---

## P/Invoke Checks

| Check | What to look for |
|-------|-----------------|
| **`static_cast` over C-style casts** | `static_cast<int>(val)` is checked at compile time. `(int)val` can silently reinterpret bits. Always use C++ casts in interop boundaries. |
| **`nullptr` over `NULL`** | `NULL` is `0` in C++, which can silently convert to integral types. `nullptr` has proper pointer semantics. |
| **Struct field ordering for padding** | When defining structs shared between managed and native code, order fields largest-to-smallest to minimize padding. Explicit `[StructLayout(LayoutKind.Sequential)]` and matching C struct must be kept in sync. |
| **Bool marshalling** | Boolean marshalling is a common source of bugs. C++ `bool` is 1 byte, Windows `BOOL` is 4 bytes. When P/Invoking, explicitly specify `[MarshalAs(UnmanagedType.U1)]` or `[MarshalAs(UnmanagedType.Bool)]` (4-byte). |
| **String marshalling charset** | P/Invoke string parameters should specify `CharSet.Unicode` (UTF-16) or use `[MarshalAs(UnmanagedType.LPUTF8Str)]` for UTF-8. Don't rely on the default (ANSI on Windows). |
