# Instructions for AIs

This repository is **.NET for Android** (formerly Xamarin.Android), the open-source bindings and tooling for Android development using .NET languages like C#.

This is the main branch targeting **.NET 10**.

## Repository Overview

.NET for Android provides:
- Android SDK bindings in C# (`src/Mono.Android/`)
- MSBuild tasks for building Android apps (`src/Xamarin.Android.Build.Tasks/`)
- Native runtime components (`src/native/`)
- Build tooling and infrastructure (`build-tools/`)
- Comprehensive test suite (`tests/`)

### Key Directories
- `bin/`: Build output (Debug/Release configurations)
- `src/`: Main redistributable source code
- `tests/`: Unit tests and integration tests
- `build-tools/`: Build-time-only tooling
- `external/`: Git submodules (Java.Interop, mono, etc.)
- `Documentation/`: Project documentation
- `eng/`: .NET Arcade SDK build infrastructure

### Project Types in this Repository
- **Android API Bindings**: C# wrappers for Android Java APIs
- **MSBuild Tasks**: Build logic for .NET Android projects
- **Native Libraries**: C++ runtime components using CMake
- **Java Support Code**: Java runtime classes
- **Build Tools**: Custom tools for build process
- **Tests**: NUnit tests, integration tests, and device tests

## Build System

This repository uses:
- **MSBuild** as the primary build system with extensive `.targets` and `.props` files
- **[.NET Arcade SDK](https://github.com/dotnet/arcade)** for consistent .NET build infrastructure  
- **CMake** for native C++ components
- **Gradle** for some Android-specific build tasks

Common build commands:
- `./build.sh` or `build.cmd` - Main build
- `./dotnet-local.sh` or `dotnet-local.cmd` - Use locally built .NET tools
- `make` - Various make targets for specific components

## Development Guidelines

**Always search Microsoft documentation (MS Learn) when working with .NET, Windows, or Microsoft features, APIs, or integrations.** Use the `microsoft_docs_search` tool to find the most current and authoritative information about capabilities, best practices, and implementation patterns before making changes.

## Localization Files

**DO NOT modify localization files that are automatically maintained by bots and build integrations:**

- **Never modify `*.lcl` files** in the `Localize/loc/` directory - these are managed by localization automation
- **Never modify non-English `*.resx` files** (e.g., `Resources.ja.resx`, `Resources.ko.resx`, etc.) - these are auto-generated from the main English resources
- **Only modify the main English `*.resx` files** (e.g., `Resources.resx`) when updating user-facing strings
- The localization bots will automatically update all translated versions based on changes to the main English resources

When making changes to user-facing text:
1. Only update the main English `*.resx` files
2. Let the automated systems handle all translations and localized files
3. Do not manually edit translated content as it will be overwritten

## Android Development Patterns

### API Bindings
- Android Java APIs are bound to C# in `src/Mono.Android/`
- Follow existing patterns for Android namespaces (e.g., `Android.App`, `Android.Content`)
- Use `[Register]` attributes for Java type registration

### MSBuild Integration
- Build tasks extend `Microsoft.Build.Utilities.Task` or related base classes
- Place custom MSBuild logic in `src/Xamarin.Android.Build.Tasks/Tasks/`
- Follow existing error code patterns (e.g., `XA####` for errors, `XA####` for warnings)
- Support incremental builds where possible
- Follow patterns in [`Documentation/guides/MSBuildBestPractices.md`](Documentation/guides/MSBuildBestPractices.md)

### Native Code
- C++ code uses CMake build system
- Native libraries are in `src/native/`
- Follow Android NDK patterns and conventions
- Use proper JNI patterns for Java interop

### Testing Patterns
- Unit tests go in `tests/` directory
- Device integration tests in `tests/MSBuildDeviceIntegration/`
- Use NUnit for C# tests
- Mock Android APIs appropriately for unit testing
- Follow patterns in [`Documentation/workflow/UnitTests.md`](Documentation/workflow/UnitTests.md) for comprehensive testing guidance

### Development and Debugging
- Use `MSBUILDDEBUGONSTART=2` environment variable to debug MSBuild tasks
- Follow patterns in [`Documentation/workflow/DevelopmentTips.md`](Documentation/workflow/DevelopmentTips.md)
- Use update directories for rapid testing of Debug builds on devices
- Utilize `dotnet test --filter` for running specific unit tests
- Reference [`Documentation/workflow/MSBuildBestPractices.md`](Documentation/workflow/MSBuildBestPractices.md) for MSBuild debugging techniques

## Nullable Reference Types

When opting C# code into nullable reference types:

* Only make the following changes when asked to do so.

* Add `#nullable enable` at the top of the file.

* Don't *ever* use `!` to handle `null`!

* Declare variables non-nullable, and check for `null` at entry points.

* Use `throw new ArgumentNullException (nameof (parameter))` in `netstandard2.0` projects.

* Use `ArgumentNullException.ThrowIfNull (parameter)` in Android projects that will be .NET 10+.

* `[Required]` properties in MSBuild task classes should always be non-nullable with a default value.

* Non-`[Required]` properties should be nullable and have null-checks in C# code using them.

* For MSBuild task properties like:

```csharp
public string NonRequiredProperty { get; set; }
public ITaskItem [] NonRequiredItemGroup { get; set; }

[Output]
public string OutputProperty { get; set; }
[Output]
public ITaskItem [] OutputItemGroup { get; set; }

[Required]
public string RequiredProperty { get; set; }
[Required]
public ITaskItem [] RequiredItemGroup { get; set; }
```

Fix them such as:

```csharp
public string? NonRequiredProperty { get; set; }
public ITaskItem []? NonRequiredItemGroup { get; set; }

[Output]
public string? OutputProperty { get; set; }
[Output]
public ITaskItem []? OutputItemGroup { get; set; }

[Required]
public string RequiredProperty { get; set; } = "";
[Required]
public ITaskItem [] RequiredItemGroup { get; set; } = [];
```

If you see a `string.IsNullOrEmpty()` check:

```csharp
if (!string.IsNullOrEmpty (NonRequiredProperty)) {
    // Code here
}
```

Convert this to use the extension method:

```csharp
if (!NonRequiredProperty.IsNullOrEmpty ()) {
    // Code here
}
```

## Formatting

C# code uses tabs (not spaces) and the Mono code-formatting style defined in `.editorconfig`

* Your mission is to make diffs as absolutely as small as possible, preserving existing code formatting.

* If you encounter additional spaces or formatting within existing code blocks, LEAVE THEM AS-IS.

* If you encounter code comments, LEAVE THEM AS-IS.

* Place a space prior to any parentheses `(` or `[`

* Use `""` for empty string and *not* `string.Empty`

* Use `[]` for empty arrays and *not* `Array.Empty<T>()`

* Do *NOT* leave random empty lines when removing code.

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

## Error and Warning Patterns

### Error Codes
- Use `XA####` for MSBuild errors (e.g., `XA1234`)
- Use `XA####` for MSBuild warnings  
- Use `APT####` for Android Asset Packaging Tool errors
- Include clear, actionable error messages
- Reference documentation when available

### Logging
- Use MSBuild logging (`Log.LogError`, `Log.LogWarning`, `Log.LogMessage`)
- Every `LogCodedWarning` and `LogCodedError` needs an error/warning code
- Include relevant context (file paths, line numbers when available)
- Make error messages helpful for developers

## Documentation Patterns

### Code Documentation
- Use XML documentation comments for public APIs
- Document Android API level requirements where relevant
- Include `<example>` tags for complex APIs
- Reference official Android documentation where helpful:
  - [Android Developer Guide](https://developer.android.com/develop)
  - [Android API Reference](https://developer.android.com/reference)

### Project Documentation
- Update relevant `.md` files in `Documentation/` when making significant changes
- Follow existing documentation structure and formatting
- Include code examples that actually work

## Commit Message Guidelines

Follow the patterns in `Documentation/workflow/commit-messages.md`:

### Summary Format
```
[Component] Summary
```

Where Component is either:
- Directory name (e.g., `[Mono.Android]`, `[Build.Tasks]`)
- Broad category: `build`, `ci`, `docs`, `tests`

### Dependency Bumps
```
Bump to org/repo/branch@commit
Bump to [Dependency Name] [Dependency Version]
```

### Required Sections
- **Changes**: What was modified
- **Fixes**: Issues resolved (include GitHub issue numbers)
- **Context**: Why the change was needed

## Common Troubleshooting

### Build Issues
- Clean `bin/` and `obj/` directories
- Ensure Android SDK/NDK are properly configured
- Use `make clean` for complete rebuild

### MSBuild Task Development
- Test tasks in isolation first
- Handle incremental build scenarios
- Consider cross-platform compatibility (Windows/macOS/Linux)
- Validate inputs and provide clear error messages
- Use `MSBUILDDEBUGONSTART=2` for debugging MSBuild tasks

### Device Testing
- Use `tests/MSBuildDeviceIntegration/` for comprehensive device tests
- Leverage update directories for rapid iteration on Debug builds
- Use `dotnet test --filter` to run specific tests
- Ensure proper Android emulator/device setup for testing

### Performance and Diagnostics
- Use profiling capabilities documented in [`Documentation/guides/profiling.md`](Documentation/guides/profiling.md)
- Leverage tracing features documented in [`Documentation/guides/tracing.md`](Documentation/guides/tracing.md)
- Monitor build performance and optimize accordingly

### Native Development
- Use appropriate CMake patterns from existing code
- Handle different Android ABIs (arm64-v8a, armeabi-v7a, x86_64, x86)
- Note: Native CoreCLR components in `src/native/clr` only target 64-bit platforms (arm64-v8a, x86_64)
- Follow Android NDK security best practices
- Test on multiple Android API levels when relevant

### Android API Management
- Follow [`Documentation/workflow/HowToAddNewApiLevel.md`](Documentation/workflow/HowToAddNewApiLevel.md) for adding new Android API levels
- Use existing patterns for Java-to-C# API bindings
- Understand Android backward/forward compatibility requirements

## Contributing Guidelines

### Updating AI Instructions
- Always update `copilot-instructions.md` when making changes that would affect how AI assistants should work with the codebase
- This includes new patterns, conventions, build processes, or significant structural changes
