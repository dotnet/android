# NativeAOT Complex App Build Artifacts Analysis

## Summary

Created a complex NativeAOT test app with:
- Complex layout (buttons, images, progress bar, scroll view, cards)
- HTTP requests with `HttpClientHandler.ServerCertificateCustomValidationCallback`
- Async/await with UI updates via `RunOnUiThread`
- Button click handlers (View.OnClickListener)

## Build Artifacts Analysis

### APK Size
- **Compressed APK**: 4.4 MB
- **classes.dex**: 201 KB
- **Native library (libNativeAotComplexApp.so)**: 10 MB (uncompressed)

### LLVM IR Marshal Methods
| Metric | Count |
|--------|-------|
| Total .ll files generated | 310 |
| Total .o files generated | 310 |
| Java JCW files generated | 312 |
| **Symbols before filtering** | 759 |
| **Symbols after filtering** | 27 |
| **Reduction** | 96.5% |

### ILC Output
- **ILC object file**: 44 MB (NativeAotComplexApp.o)
- **TypeMap proxy types surviving ILC trimming**: 324

### Linked Proxy Types (actually kept)
The following types have marshal methods that survived ILC trimming:
1. `NativeAotComplexApp.MainActivity` - app's main activity
2. `Android.OS.ActionHandlerCallback` - for Handler.Post
3. `Android.Runtime.InputStreamAdapter` - for HTTP response streams
4. `Android.Runtime.OutputStreamAdapter` - for HTTP request streams
5. `Android.Runtime.JavaObject` - base object wrapper
6. `Android.View.View.OnClickListenerImplementor` - for button clicks
7. `Java.Lang.Runnable` - for threading
8. `Java.Lang.RunnableImplementor` - for threading

## Key Observations

### 1. Filtering Works Effectively
The DGML-based filtering reduces marshal method symbols from 759 to 27 (96.5% reduction).

### 2. HTTP/SSL Requires Stream Adapters
Using `HttpClient` with `ServerCertificateCustomValidationCallback` pulls in:
- `InputStreamAdapter` - reading response body
- `OutputStreamAdapter` - sending request body

### 3. UI Event Handlers Pull in Specific Types
- Button clicks → `View.OnClickListenerImplementor`
- Handler.Post → `ActionHandlerCallback`
- Threading → `Runnable` + `RunnableImplementor`

### 4. Native Library Size
10 MB native library contains:
- .NET runtime (CoreCLR NativeAOT)
- Application code
- TypeMap attributes
- JNI marshal method stubs (filtered)

## Recommendations

### Immediate Next Steps

1. **Test on Physical Device**
   The emulator boot is slow with swiftshader. Test on a physical ARM64 device.

2. **ProGuard/R8 Filtering for Java Classes**
   Currently all 312 Java JCW classes are included in the APK. We should filter these based on ILC trimming results.
   ```
   # Generate proguard-rules based on surviving proxy types
   -keep class crc64...MainActivity { *; }
   -keep class mono.android.runtime.InputStreamAdapter { *; }
   # etc.
   ```

3. **Fix IlcGenerateMetadataLog Property Timing**
   The `IlcGenerateMetadataLog=true` property needs to be set before ILC targets load. Currently requires explicit project setting.

### Medium-Term Improvements

4. **Multi-ABI Support**
   Currently only android-arm64 is supported. Add support for:
   - android-x64 (for emulators)
   - android-arm (for older devices)

5. **Incremental Build Support**
   - Cache ILC metadata analysis results
   - Only regenerate .ll/.java files for changed assemblies
   - Incremental .o file compilation

6. **ProGuard Configuration Generation**
   Automatically generate ProGuard keep rules for:
   - Surviving proxy types
   - JCW classes that have marshal methods
   - Required reflection targets

### Long-Term Considerations

7. **DEX Optimization**
   The 201 KB classes.dex contains many unused Java classes. Integrate with R8 to:
   - Shrink unused classes
   - Optimize based on actual usage

8. **Native Library Stripping**
   Investigate further size reduction:
   - Symbol stripping
   - LTO (Link-Time Optimization)
   - Section removal

9. **Startup Performance**
   Profile and optimize:
   - TypeMap attribute scanning
   - JNI method registration
   - Initial peer creation

## Files Created

- `samples/HelloWorld/NativeAotComplexApp/` - Complete sample app
  - `NativeAotComplexApp.csproj` - Project file with NativeAOT settings
  - `MainActivity.cs` - Main activity with HTTP/SSL testing
  - `AndroidManifest.xml` - With INTERNET permission
  - `Resources/layout/activity_main.xml` - Complex UI layout
  - `Resources/drawable/` - Vector icons
  - `Resources/values/` - Strings and styles
