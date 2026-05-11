#### App build and runtime

- [GitHub PR #11123](https://github.com/dotnet/android/pull/11123):
  Add trimming-friendly support for the `[Export]` and `[ExportField]`
  attributes to the trimmable type map (`_AndroidTypeMapImplementation=trimmable`).

  Customer-visible behavior notes when building with
  `_AndroidTypeMapImplementation=trimmable`:

  - `[Export]` and `[ExportField]` are supported on user types deriving from
    _Java.Lang.Object_ (and other registered Java peers). The build emits
    `[UnmanagedCallersOnly]` wrappers and registers them with the matching
    Java _native_ methods directly — no dynamic codegen at runtime.

  - The _Mono.Android.Export_ assembly is excluded from the trimmable build
    because its `DynamicMethod`-based codegen is incompatible with trimming
    and AOT. Applications that only consume the `[Export]` /
    `[ExportField]` attributes are unaffected (the attribute types are
    defined on the user assembly's referenced types).

    Apps or libraries that directly call APIs in the _Mono.Android.Export_
    assembly are not supported under the trimmable type map.

  - User-visible managed constructors are now invoked when Java instantiates
    a peer via a registered Java constructor (for example, the `(Throwable cause)`
    constructor on `Throwable` subclasses and parameterized `[Export]` constructors).
    Previously the trimmable path always built the peer via the activation
    constructor `(IntPtr, JniHandleOwnership)`, which skipped any user
    constructor body.

  - Unhandled managed exceptions thrown from an `[Export]` method are routed
    through `JniRuntime.OnUserUnhandledException` (matching the modern
    _JavaInterop_ exception contract). The original managed exception type
    and message are preserved across the JNI boundary. This differs from
    the legacy _LLVM IR_ type map path, which converted the exception into
    a _Java.Lang.Throwable_ via `AndroidEnvironment.UnhandledException`.
