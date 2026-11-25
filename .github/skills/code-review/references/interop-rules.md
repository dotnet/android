# Managed ↔ Native Interop Review Rules

Rules for managed/native boundaries and Java/JNI name handling. Load for interop
markers, Java/JNI name conversions, or changes under Java.Interop, Mono.Android,
native runtime, or TrimmableTypeMap paths.

---

## JNI Interop Checks (`src/Java.Interop/`, `src/Mono.Android/`)

| Check | What to look for |
|-------|-----------------|
| **`JniObjectReference` lifecycle** | Every `JniObjectReference` obtained from JNI calls must be disposed in a `try`/`finally` block. Local references that escape their JNI frame exhaust the local reference table (default 512 entries). Global references that aren't freed are permanent leaks. |
| **`JniPeerMembers` for method/field IDs** | Method and field IDs should be cached via `JniPeerMembers.InstanceMethods`, `JniPeerMembers.StaticMethods`, or `JniPeerMembers.InstanceFields`. Looking up IDs on every call is expensive. |
| **Virtual vs non-virtual dispatch** | Default interface method implementations should use `InvokeNonvirtualVoidMethod()` (non-virtual). Class method overrides should use `InvokeVirtualVoidMethod()` (virtual). Getting this wrong changes dispatch semantics. |
| **`[Register]` attribute accuracy** | `[Register]` attributes must exactly match the Java method name and JNI signature. Mismatches cause `NoSuchMethodError` at runtime. Verify against the Java API description. |
| **`JniTransition` for native callbacks** | Entry points from Java into managed code should use `JniTransition` to properly handle exception marshaling. Without it, managed exceptions propagate into the JVM as undefined behavior. |
| **Exception checking after JNI calls** | After JNI calls that can throw (most of them), check for pending Java exceptions via `JniEnvironment.Errors.ExceptionOccurred()` or rely on the built-in checking in `JniPeerMembers` wrappers. Don't ignore JNI error return codes. |
| **Thread-local JNI environments** | JNI environments are thread-local. Use `JniEnvironment.Current` to access the current thread's environment. Never cache a `JNIEnv*` across threads. |
| **Retaining JNI handle owners (`GC.KeepAlive`)** | `.Handle`/`.PeerReference`/`new JValue (peer)`/`new JniArgumentValue (peer)` copy raw handles without rooting peers. On CoreCLR/NativeAOT, Java reachability doesn't protect copied handles: `JavaMarshalGCBridge` deletes/replaces grefs of unreachable peers (even `java.lang.Class`), causing CheckJNI `use of deleted global reference` aborts. See checklist below. |

---

## JNI Peer Lifetimes (`GC.KeepAlive`) Review Checklist

Applies to `src/Java.Interop/`, `src/Mono.Android/`, `src/generator/`, and handwritten JNI:

| Pattern | Severity | What to verify |
|---------|----------|----------------|
| **Borrowed handles (`Handle`, `PeerReference`, `JValue`, `JniArgumentValue`)** | ❌ error | Keep every owner (`this`, peer args, `Equals`, all paths incl. unregistered `Keep` peers) alive via `GC.KeepAlive` right after JNI/P/Invoke, before error checks/conversions. Straight-line without exception cleanup; `finally` if `try`/`finally` exists (`DeleteLocalRef`) or loops/multiple exits require it. |
| **`WithLocalJniHandle` / `fixed`** | ❌ error | `WithLocalJniHandle (arg, ...)` retains `arg`, **not** `this`; `fixed` pins array, **not** `this`. Require `GC.KeepAlive (this)`. |
| **`NewLocalRef ()` on `PeerReference`** | ❌ error | Require `GC.KeepAlive (peer)` afterward (incl. `ConditionalWeakTable` proxies); read `PeerReference` once into a local before `.IsValid`/`.NewLocalRef ()`. |
| **Handwritten `JNIEnv.FinishCreateInstance (Handle, ...)`** | ❌ error | Needs `GC.KeepAlive (this)`. `JniPeerMembers.InstanceMethods.FinishCreateInstance` doesn't retain `self` on `main` until #13758 lands. |
| **Multi-call wrappers (`JniArrayElements`)** | ❌ error | Hold strong peer ref, keep local copy alive in `finally` across `Synchronize`, clear on every terminal `Release` (incl. `Release (Commit)`); avoid throwing between `Get*ArrayElements` and wrapper construction. |
| **Unrooted peer / `WeakReference` race** | ❌ error | Don't pass `new WeakReference (new Foo ()).Target` to Java (`SetListener`/`AddListener`) or split `IsAlive`/`.Target`; bind strong local (`if (wref?.Target is Foo f)`) across registration and return. |
| **`// SAFETY:` comments** | ⚠️ warning | Flag retention claims without `GC.KeepAlive`/later use, dropped ABI/type contracts, or lines > 180 cols. |

**Do NOT flag:** locally owned refs deleted in `finally` (`NewString`, `NewArray`, `ToLocalJniHandle`, `GetObjectClass`); `this` in `JniPeerMembers.InstanceMethods.Invoke*`/`JniInstanceFields.*`; non-finalizable `JniType`/static `class_ref`; enclosing `using`/`lock`; low-level raw-`IntPtr` APIs (`JNIEnv.Call*Method (IntPtr, ...)`) with `/// <safety>`.

---

## Java Type Names

Determine the required representation at each changed call site:

| Representation | Nested-class example | Typical consumers |
|----------------|----------------------|-------------------|
| **JNI internal name** | `android/view/View$OnClickListener` | JNI registration, descriptors after removing `L`/`;`, runtime JNI lookup |
| **Java binary name** | `android.view.View$OnClickListener` | `Class.forName`, Android manifests, ACW maps, ProGuard/R8 class rules |
| **Java source name** | `android.view.View.OnClickListener` | Generated `.java` type references such as `extends`, `implements`, parameters, and return types |
| **Managed nested type name** | `Android.Views.View+IOnClickListener` | Managed metadata and assembly-qualified names |

- Read conversion helpers instead of trusting their names, and compare their full behavior with any inline logic they replace.
- Trace serialized names to the final consumer; correctness is per call site.
- Preserve `$` for binary-name consumers. Replace it with `.` only for Java source names.

---

## P/Invoke & Marshaling Checks

| Check | What to look for |
|-------|-----------------|
| **`static_cast` over C-style casts** | `static_cast<int>(val)` is checked at compile time. `(int)val` can silently reinterpret bits. Always use C++ casts in interop boundaries. |
| **`nullptr` over `NULL`** | `NULL` is `0` in C++, which can silently convert to integral types. `nullptr` has proper pointer semantics. |
| **Struct field ordering for padding** | When defining structs shared between managed and native code, order fields largest-to-smallest to minimize padding. Explicit `[StructLayout(LayoutKind.Sequential)]` and matching C struct must be kept in sync. |
| **Bool marshalling** | Boolean marshalling is a common source of bugs. C++ `bool` is 1 byte, Windows `BOOL` is 4 bytes. When P/Invoking, explicitly specify `[MarshalAs(UnmanagedType.U1)]` or `[MarshalAs(UnmanagedType.Bool)]` (4-byte). |
| **String marshalling charset** | P/Invoke string parameters should specify `CharSet.Unicode` (UTF-16) or use `[MarshalAs(UnmanagedType.LPUTF8Str)]` for UTF-8. Don't rely on the default (ANSI on Windows). |
