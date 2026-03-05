# Instructions for AIs

**.NET for Android** (formerly Xamarin.Android) - Open-source Android development bindings for .NET languages. `main` branch targets **.NET 11**.

## Architecture
- `src/Mono.Android/` - Android SDK bindings in C#
- `src/Xamarin.Android.Build.Tasks/` - MSBuild tasks for Android apps  
- `src/native/` - Native runtime (MonoVM/CoreCLR/NativeAOT)
- `external/Java.Interop/` - JNI bindings and Java-to-.NET interop
- `tests/` - NUnit tests, integration tests, device tests

**Build System:** MSBuild + .NET Arcade SDK + CMake (native)

## Essential Commands
- **Build:** `./build.sh` or `build.cmd`
- **Test with local build:** `dotnet-local.sh`/`dotnet-local.cmd` 
- **Run tests:** `dotnet-local.cmd test bin/TestDebug/net9.0/Xamarin.Android.Build.Tests.dll --filter Name~TestName`
- **Device tests:** `dotnet-local.cmd test bin/TestDebug/MSBuildDeviceIntegration/net9.0/MSBuildDeviceIntegration.dll`

## Critical Rules

Reference official Android documentation where helpful:
* [Android Developer Guide](https://developer.android.com/develop)
* [Android API Reference](https://developer.android.com/reference)
* [Android `aapt2` Documentation](https://developer.android.com/tools/aapt2)

**Only modify the main English `*.resx` files** (e.g., `Resources.resx`)

**Never modify non-English localization files:** `*.lcl` files in `Localize/loc/` or non-English `*.resx` files are auto-generated.

**Use Microsoft docs:** Search MS Learn before making .NET, Windows, or Microsoft features, APIs, or integrations. Use the `microsoft_docs_search` tool.

**MSBuild Tasks:** Extend `AndroidTask` base class, use `XA####` error codes, test in isolation.

**API Bindings:** Use `[Register]` attributes, follow `Android.*` namespace patterns.

**Native Code:** Use CMake, handle multiple ABIs (arm64-v8a, armeabi-v7a, x86_64, x86).

## Nullable Reference Types

When opting C# code into nullable reference types:

* Only make the following changes when asked to do so.

* Add `#nullable enable` at the top of the file without any preceding blank lines.

* Don't *ever* use `!` (null-forgiving operator) to handle `null`! Always check for null explicitly and throw appropriate exceptions.

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

If you see a `string.IsNullOrWhiteSpace()` check:

```csharp
if (!string.IsNullOrWhiteSpace (UncompressedFileExtensions)) {
    foreach (var ext in UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
        // Code here
    }
}
```

Convert this to use the extension method:

```csharp
if (!UncompressedFileExtensions.IsNullOrWhiteSpace ()) {
    foreach (var ext in UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
        // Code here
    }
}
```

## Formatting

C# code uses tabs (not spaces) and Mono style (`.editorconfig`):
- **NEVER** use `!` (null-forgiving operator) in C# code. Always refactor to avoid it, e.g. by having helper methods return non-null types or by checking for null explicitly.
- Preserve existing formatting and comments
- Space before `(` and `[`: `Foo ()`, `array [0]`
- Use `""` not `string.Empty`, `[]` not `Array.Empty<T>()`
- Minimal diffs - don't leave random empty lines
- Do NOT use `#region` or `#endregion`

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

## Error Patterns
- **MSBuild Errors:** `XA####` (errors), `XA####` (warnings), `APT####` (Android tools)
- **Logging:** Use `Log.LogError`, `Log.LogWarning` with error codes and context

## Troubleshooting
- **Build:** Clean `bin/`+`obj/`, check Android SDK/NDK, `make clean`
- **MSBuild:** Test in isolation, validate inputs
- **Device:** Use update directories for rapid Debug iteration
- **Performance:** See `../Documentation/guides/profiling.md` and `../Documentation/guides/tracing.md`
