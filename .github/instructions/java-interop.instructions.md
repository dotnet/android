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
3. **Java Callable Wrappers (JCWs)** — Java stubs that call managed methods.
4. **Marshal methods** — runtime-generated or pre-compiled bridging code.

## Repository layout (within `external/Java.Interop/`)

### Core libraries (`src/`)
- **`Java.Interop/`** — main JNI binding library, core types and runtime.
- **`Java.Interop.Export/`** — `[Export]` attribute support for exposing managed methods to Java.

### Code generation tools (`tools/`)
- **`generator/`** — primary tool for generating C# bindings from Java API descriptions.
- **`class-parse/`** — parses Java `.class` files and generates API descriptions.
- **`java-source-utils/`** — utilities for processing Java source code.
- **`jcw-gen/`** — generates Java Callable Wrapper classes.
- **`param-name-importer/`** — imports parameter names from Java source.

### Supporting libraries
- **`Java.Interop.Tools.JavaSource/`** — Javadoc parsing, XML documentation conversion.
- **`Java.Interop.Tools.Maven/`** — Maven project integration and dependency resolution.
- **`Xamarin.Android.Tools.Bytecode/`** — Java bytecode analysis.
- **`Xamarin.SourceWriter/`** — code generation utilities.

## JNI patterns

### Error handling
- Java exceptions are automatically converted to .NET exceptions at JNI transitions.
- Use `JniEnvironment.Errors.ExceptionOccurred()` for manual exception checking after JNI calls.
- Wrap JNI calls in `try`/`finally` for proper reference cleanup.

### Memory management
- **Local references** — automatically cleaned up by the JVM at JNI frame boundaries, but explicit cleanup prevents local-reference-table exhaustion (default 512 entries) in loops.
- **Global references** — must be explicitly freed via `JniObjectReference.Dispose()`.
- Prefer `using` statements or `try`/`finally` for cleanup.

### Threading
- JNI environments are thread-local.
- Use `JniEnvironment.Current` to access the current thread's JNI environment.
- Never cache a `JNIEnv*` across threads.

## Build system

The Java.Interop projects build as part of the outer dotnet/android build via the top-level `build.sh` / `build.cmd`. Prerequisites and workflow are documented in the top-level `.github/copilot-instructions.md`.

Java-specific configuration:
- `$(JAVA_HOME)` for Java tooling.
- `$(JdkJvmPath)` in `Configuration.Override.props` for JVM library location.

## Reference material

- [JNI Specification](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html)
- [Android JNI Performance Guide](https://developer.android.com/training/articles/perf-jni)
- `external/Java.Interop/Documentation/Architecture.md`
- `external/Java.Interop/Documentation/BuildConfiguration.md`
