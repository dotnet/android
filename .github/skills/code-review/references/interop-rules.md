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
| **Retaining JNI handle owners (`GC.KeepAlive`)** | Reading `.Handle` / `.PeerReference` or passing a peer to `new JValue (peer)` / `new JniArgumentValue (peer)` does **not** keep the managed peer alive. On CoreCLR/NativeAOT, `JavaMarshalGCBridge` deletes and replaces the global ref of any managed-unreachable peer — even when the Java object is still alive (e.g., `java.lang.Class` or a receiver on the Java stack). Flag any borrowed handle passed to JNI/P/Invoke without a post-call `GC.KeepAlive` or enclosing `using`/`lock`. |

---

## JNI Peer Lifetimes (`GC.KeepAlive`) Review Checklist

When reviewing `src/Java.Interop/`, `src/Mono.Android/`, `src/generator/`, or handwritten JNI bindings:

| Pattern | Severity | What to verify |
|---------|----------|----------------|
| **Raw `Handle` / `PeerReference` passed to JNI or P/Invoke** | ❌ error | Every borrowed owner (`this` and peer parameters) must remain reachable past the native call via `GC.KeepAlive (owner)` (or enclosing `using`/`lock`). Straight-line `GC.KeepAlive` after the call is fine when there is no exception cleanup; use `finally` when a `try`/`finally` block already exists or loops/multiple exits require it. |
| **`WithLocalJniHandle` / `fixed` false sense of safety** | ❌ error | `JavaConvert.WithLocalJniHandle (arg, lref => ... Handle ...)` keeps `arg` alive, **not** `this` (closures die once `Handle` is read). `fixed (T* p = arr)` pins `arr`, **not** `this`. Require `GC.KeepAlive (this)` after the call. |
| **`NewLocalRef ()` on a peer's `PeerReference`** | ❌ error | Reading `peer.PeerReference` (including `ConditionalWeakTable` proxies) and calling `.NewLocalRef ()` requires `GC.KeepAlive (peer)` afterward. Also read `PeerReference` once into a local before checking `.IsValid` to avoid double-read races. |
| **`new JValue (peer)` / `new JniArgumentValue (peer)`** | ❌ error | Both copy the raw handle without rooting `peer`. Verify `GC.KeepAlive (peer)` follows the JNI call (e.g., `JavaList.Set`, handwritten constructors). |
| **Handwritten `JNIEnv.FinishCreateInstance (Handle, ...)`** | ❌ error | Passes a raw `Handle` without retaining `this`. Require `GC.KeepAlive (this)` after the call. Note that `JniPeerMembers.InstanceMethods.FinishCreateInstance` does not retain `self` on `main` until #13758 lands, so migrating to it alone is not sufficient until that fix is merged. |
| **Multi-call native wrappers holding `JniObjectReference`** | ❌ error | Types like `JniArrayElements` that retain a raw pointer or `JniObjectReference` across calls must hold a strong reference to the owning peer until final release — not just a copied `JniObjectReference`. |
| **Unrooted new peer stored only in `WeakReference`** | ❌ error | Storing `new Foo ()` directly into `new WeakReference (new Foo ())` before passing `Target` to Java (`SetListener`/`AddListener`) allows GC to collect it before registration. Require a strong local across registration and return. |
| **Inaccurate `// SAFETY:` lifetime claims** | ⚠️ warning | Flag comments like `"this peer retains its JNI handle"` when no `GC.KeepAlive (this)` or subsequent use actually enforces it. |

**Do NOT flag (avoid false positives):**
- Locally created owned references deleted in `finally` (`JNIEnv.NewString`, `JNIEnv.NewArray`, `ToLocalJniHandle`, `GetObjectClass`).
- `this` in methods calling `JniPeerMembers.InstanceMethods.Invoke*` or `JniInstanceFields.*` (those helpers already retain `self`).
- `JniType` (no finalizer; statically cached) and static `class_ref` / `IntPtr` handles.
- Peers already retained by `using` or `lock` spanning the entire native call.
- Low-level raw-`IntPtr` APIs (`JNIEnv.Call*Method (IntPtr, ...)`) whose caller owns handle liveness via `/// <safety>`.

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
