# Java.Interop Copilot Instructions

## Project Overview

**Java.Interop** is a .NET library that provides Java Native Interface (JNI) bindings for managed languages such as C#. It enables bidirectional interoperability between .NET's Common Language Runtime (CLR) and Java Virtual Machines (JVMs), allowing .NET code to invoke Java methods and Java code to call back into managed code.

**Primary Use Cases**:
- .NET for Android development (successor to Xamarin.Android)
- Desktop Java interop scenarios  
- Binding Java libraries for .NET consumption
- Cross-platform Java integration

## Architecture & Core Concepts

### JNI (Java Native Interface)
- Industry-standard interface for Java-native code interaction
- Provides type-safe bindings using structs like `JniObjectReference` instead of raw `IntPtr`
- Supports both SafeHandle-based (safer) and IntPtr-based (faster) implementations
- Reference types: Local, Global, and WeakGlobal references with proper lifecycle management

### Type System & Marshaling
- **JavaObject**: Base class for managed wrappers of Java objects
- **JniPeerMembers**: Caches method and field IDs for efficient access
- **Value Marshaling**: Converts between Java and .NET types (e.g., `java.lang.String` ↔ `System.String`)
- **Exception Marshaling**: Translates Java exceptions to .NET exceptions

### Code Generation Pipeline
1. **API Description**: XML files describing Java APIs
2. **Generator Tool**: Converts API descriptions to C# binding code
3. **Java Callable Wrappers (JCWs)**: Java stubs for calling managed methods
4. **Marshal Methods**: Runtime-generated or pre-compiled bridging code

## Repository Structure

### Core Libraries (`src/`)
- **`Java.Interop/`**: Main JNI binding library with core types and runtime
- **`Java.Interop.Dynamic/`**: C# 4.0 `dynamic` provider for runtime method invocation
- **`Java.Interop.Export/`**: `[Export]` attribute support for exposing managed methods to Java
- **`Java.Runtime.Environment/`**: JVM loading and lifecycle management
- **`Java.Base/`**: Bindings for core Java types (`java.lang.*`, etc.)

### Code Generation Tools (`tools/`)
- **`generator/`**: Primary tool for generating C# bindings from Java API descriptions
- **`class-parse/`**: Parses Java `.class` files and generates API descriptions
- **`java-source-utils/`**: Utilities for processing Java source code
- **`jcw-gen/`**: Generates Java Callable Wrapper classes
- **`param-name-importer/`**: Imports parameter names from Java source

### Supporting Libraries
- **`Java.Interop.Tools.JavaSource/`**: Javadoc parsing and XML documentation conversion
- **`Java.Interop.Tools.Maven/`**: Maven project integration and dependency resolution
- **`Xamarin.Android.Tools.Bytecode/`**: Java bytecode analysis and processing
- **`Xamarin.SourceWriter/`**: Code generation utilities

### Testing (`tests/`)
- Unit tests for all major components
- Performance benchmarks (`Java.Interop-PerformanceTests/`)
- Integration tests with real JVM instances
- Generator tests with sample API descriptions

### Samples (`samples/`)
- **`Hello-Core/`**: Minimal JNI usage without object mapping
- **`Hello-Java.Base/`**: Using core Java type bindings
- **`Hello-NativeAOT*/`**: Ahead-of-time compilation scenarios

## Development Patterns & Conventions

### Code Formatting

C# code uses tabs (not spaces) and the Mono code-formatting style defined in `.editorconfig`

* Your mission is to make diffs as absolutely as small as possible, preserving existing code formatting.

* If you encounter additional spaces or formatting within existing code blocks, LEAVE THEM AS-IS.

* If you encounter code comments, LEAVE THEM AS-IS.

* Place a space prior to any parentheses `(` or `[`

* Use `""` for empty string and *not* `string.Empty`

* Use `[]` for empty arrays and *not* `Array.Empty<T>()`

Examples of properly formatted code:

```csharp
Foo ();
Bar (1, 2, "test");
myarray [0] = 1;

if (someValue) {
    // Code here
}

try {
    // Code here
} catch (Exception e) {
    // Code here
}
```

### Code Comments
- Use XML documentation comments (`///`) for public APIs
- Document JNI interop behavior and threading requirements
- Include usage examples for complex scenarios

### Error Handling
- Java exceptions are automatically converted to .NET exceptions
- Use `JniEnvironment.Errors.ExceptionOccurred()` for manual exception checking
- Wrap JNI calls in `try`/`finally` blocks for proper resource cleanup

### Memory Management
- Local references: Automatically cleaned up by JVM
- Global references: Must be explicitly freed via `JniObjectReference.Dispose()`
- Use `using` statements or `try`/`finally` for proper cleanup

### Threading
- JNI environments are thread-local
- Use `JniEnvironment.Current` to access the current thread's JNI environment
- Java objects can be shared across threads with proper reference management

## Build System

### Prerequisites
- .NET 9+ SDK
- Java Development Kit (for compiling Java test classes)
- Platform-specific JVM libraries

### Build Commands
```bash
# Initialize submodules and prepare build
dotnet build -t:Prepare

# Build all projects
dotnet build

# Run specific tests
dotnet test tests/Java.Interop-Tests/Java.Interop-Tests.csproj

# Build with specific configuration
dotnet build -c Release
```

### Configuration
- Use `Configuration.Override.props` for local build customization
- Set `$(JdkJvmPath)` to specify JVM library location
- Configure `$(JAVA_HOME)` for Java tooling

## Useful Resources

- [JNI Specification](http://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html)
- [.NET for Android Documentation](https://learn.microsoft.com/en-us/dotnet/android/)
- [Android JNI Performance Guide](https://developer.android.com/training/articles/perf-jni)
- [Project Architecture Documentation](Documentation/Architecture.md)
- [Build Configuration Guide](Documentation/BuildConfiguration.md)

## Getting Help

- Review existing tests for usage patterns
- Check [GitHub Issues](https://github.com/dotnet/java-interop/issues) for known problems
- Consult the [.NET Discord](https://aka.ms/dotnet-discord) for community support
- Follow [Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/) for contributions