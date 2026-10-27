---
applyTo: "external/Java.Interop/**"
---

# Java.Interop conventions

Additional context for code under `external/Java.Interop/`. This complements the top-level `.github/copilot-instructions.md` — the Mono formatting rules, nullable-reference-type rules, and CI/investigation practices there apply here too.

## Project overview

**Java.Interop** is the low-level .NET ↔ Java bridge that .NET for Android is built on. It provides JNI bindings and codegen tooling for generating C# bindings from Java API descriptions.

## Architecture & core concepts

### JNI (Java Native Interface)

- Industry-standard interface for Java-native code interaction.
- Type-safe bindings via `JniObjectReference` instead of raw `IntPtr`.
- Reference types: **Local**, **Global**, and **WeakGlobal** — each with a distinct lifecycle.

### Type system & marshaling

- **`JavaObject`** — base class for managed wrappers of Java objects.
- **`JniPeerMembers`** — caches method and field IDs for efficient access.
- **Value marshaling** — converts between Java and .NET types (`java.lang.String` ↔ `System.String`, etc.).
- **Exception marshaling** — translates Java exceptions to .NET exceptions.

### Code generation pipeline

1. **API description** — XML files describing Java APIs.
2. **`generator` tool** — converts API descriptions into C# binding code.
3. **Android application interop** — `src/Microsoft.Android.Sdk.TrimmableTypeMap/`
   in the enclosing repository generates JCW Java sources and managed typemap
   assemblies at build time.
4. **Native callbacks** — typemaps register `UnmanagedCallersOnly` entrypoints
   with JNI `RegisterNatives`. Generated binding callbacks use
   `JniMarshal.SafeInvokeAction`/`SafeInvokeFunc` or typed helpers to handle JNI
   transitions and exceptions, not runtime-emitted delegate wrappers.

The `XAJavaInterop1` C# binding generator and shared delegate declarations remain
supported. `JNINativeWrapper.CreateDelegate` remains only for legacy binary
references: it is obsolete with `error: true` and throws if invoked. Typemap
generation reads legacy connector metadata without executing those connectors.
See the [JNI interop guide](../../Documentation/guides/internals/JavaJNI_Interop.md)
and [exception-handling guide](../../Documentation/project-docs/exception-handling.md).

## Repository layout (within `external/Java.Interop/`)

### Core libraries (`src/`)
- **`Java.Interop/`** — main JNI binding library, core types and runtime.

### Code generation tools (`tools/`)
- **`generator/`** — primary tool for generating C# bindings from Java API descriptions.
- **`class-parse/`** — parses Java `.class` files and generates API descriptions.
- **`java-source-utils/`** — utilities for processing Java source code.

### Supporting libraries
- **`Java.Interop.Tools.JavaCallableWrappers/`** — shared naming, scanning, and
  type-name mapping utilities; not the Android application JCW emitter.
- **`Java.Interop.Tools.JavaSource/`** — Javadoc parsing, XML documentation conversion.
- **`Java.Interop.Tools.Maven/`** — Maven project integration and dependency resolution.
- **`Xamarin.Android.Tools.Bytecode/`** — Java bytecode analysis.
- **`Xamarin.SourceWriter/`** — code generation utilities.

## JNI patterns

### Error handling
- Java exceptions are automatically converted to .NET exceptions at JNI transitions.
- Use `JniEnvironment.Exceptions.ExceptionOccurred()` for manual exception checking after JNI calls.
- Wrap JNI calls in `try`/`finally` for proper reference cleanup.

### Memory management
- **Local references** — cleaned up at JNI frame boundaries, but explicit cleanup prevents local-reference-table exhaustion in loops.
- **Owned global references** — must be explicitly freed via `JniObjectReference.Dispose (ref reference)`.
- Use `using` for disposable managed peers and `try`/`finally` for JNI reference cleanup.

### Threading
- JNI environments are thread-local.
- Use `JniEnvironment.EnvironmentPointer` to access the current thread's JNI environment.
- Never cache a `JNIEnv*` across threads.

## Build system

The Java.Interop projects build as part of the outer dotnet/android SDK build.
Use the top-level build and test workflow for Android runtime integration;
prerequisites are documented in the top-level `.github/copilot-instructions.md`.

Standalone host tooling builds and tooling tests do not require a locally built
Android SDK. The standalone tooling and test target framework is .NET 10 via
`external/Java.Interop/Directory.Build.props`, independently of the Android
product's target framework. Follow the Java.Interop
[README](../../external/Java.Interop/README.md#building) and
[build configuration](../../external/Java.Interop/Documentation/BuildConfiguration.md)
for preparation and JDK tool-path settings (`JAVA_HOME`, `JdksRoot`,
`JavaSdkDirectory`, `JavaCPath`, and `JarPath`).

Core JNI tests and Java fixtures live in
`tests/Mono.Android-Tests/Java.Interop-Tests/` and run on Android with
`Mono.Android.NET-Tests.csproj`. Tooling tests remain under
`external/Java.Interop/tests/`; there is no standalone core host-JVM harness.

## Reference material

- [JNI Specification](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html)
- [Android JNI Performance Guide](https://developer.android.com/training/articles/perf-jni)
- `external/Java.Interop/Documentation/Architecture.md`
- `external/Java.Interop/Documentation/BuildConfiguration.md`
