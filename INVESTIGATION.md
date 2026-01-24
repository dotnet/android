# TypeMap v2 SIGSEGV Investigation

## Summary

We're migrating to TypeMap API v2 which uses compile-time generated proxies instead of reflection-based type lookup. The app crashes with SIGSEGV immediately after `CreatePeer` successfully creates a managed object.

## Current State

### What Works
- TypeMap lookup finds the correct proxy type (`HelloWorld_MainActivity_Proxy`)
- `GetFunctionPointer` returns valid function pointers for method indices
- Proxy's `CreateInstance` successfully creates `HelloWorld.MainActivity` instance
- `PeerReference.Handle` is set correctly (e.g., `0x3986`)
- All logging in `TypeMapAttributeTypeMap.CreatePeer` completes successfully

### The Crash
- **Fault address**: `0x5802000f` (consistent across runs)
- **Signal**: SIGSEGV (SEGV_MAPERR)
- **Timing**: Immediately after `CreatePeer` returns, before control reaches `ManagedValueManager.CreatePeer` line 574

### Call Stack (from stack trace)
```
_Microsoft.Android.TypeMaps.HelloWorld_MainActivity_Proxy.n_onCreate_mm_0(IntPtr, IntPtr, IntPtr)
  → HelloWorld.MainActivity.n_onCreate(IntPtr, IntPtr, IntPtr)
    → Java.Lang.Object._GetObject<T>(IntPtr, JniHandleOwnership)
      → Java.Lang.Object.GetObject(IntPtr, JniHandleOwnership, Type)
        → JniRuntime.JniValueManager.GetPeer(JniObjectReference, Type)
          → ManagedValueManager.CreatePeer(ref JniObjectReference, JniObjectReferenceOptions, Type)
            → TypeMapAttributeTypeMap.CreatePeer(IntPtr, JniHandleOwnership, Type)
```

### Timeline from Logs
```
22:28:27.350  CreatePeer: Returning result...
22:28:27.428  CreatePeer: Stack trace: [full trace]
22:28:27.429  SIGSEGV at 0x5802000f
```

The crash happens ~80ms after "Returning result..." log, which includes time to generate the stack trace. The actual crash is <1ms after the stack trace is printed.

## Key Observations

1. **ManagedValueManager logs were missing** - We initially used `LogLevel.Info` which may have been filtered. Changed to `LogLevel.Error` but haven't tested yet.

2. **Handle values are suspicious**:
   - Input to CreatePeer: `handle=0x7ffdb37f08` (looks like stack address on ARM64)
   - PeerReference.Handle after init: `0x3986` (looks like JNI local reference)
   - These don't match, which is expected (SetHandle creates a new reference)

3. **Fault address analysis**: `0x5802000f`
   - Not a null pointer
   - Low bits `0x0f` could be an offset into a structure
   - High bits `0x5802` could be a corrupted pointer/reference

4. **The crash happens in the return path** - after `return result;` but before the caller receives the value.

## Potential Causes

### 1. Corrupted Object State
The `GetUninitializedObject` + base ctor call pattern might be leaving the object in a bad state:
```csharp
var obj = RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
Activity..ctor(obj, handle, transfer);  // Call base ctor
return obj;
```

### 2. JNI Reference Corruption
The handle `0x7ffdb37f08` passed to CreatePeer looks like a stack pointer. If this is being interpreted as a JNI reference somewhere, it could cause corruption.

### 3. Stack Corruption in UCO Wrapper
The UCO wrapper's try/catch/finally structure or return value handling might be corrupting the stack.

### 4. GC/Memory Issue
The created object might be getting collected or moved before it's properly rooted.

### 5. IL Generation Bug
The generated IL for `CreateInstance` or the UCO wrapper might have subtle bugs.

## What We've Tried

1. ✅ Added extensive logging throughout CreatePeer flow
2. ✅ Verified TypeMap lookup works correctly
3. ✅ Verified GetFunctionPointer returns valid pointers
4. ✅ Confirmed CreateInstance creates the right type
5. ✅ Added stack trace logging to pinpoint crash location
6. ✅ Changed log level to Error (not yet tested)

## Next Steps to Investigate

### Immediate
1. **Run with Error log level** - Verify ManagedValueManager logs appear
2. **Add logging after return** - In `GetObject`, after `GetPeer` returns
3. **Inspect generated IL** - Decompile `_Microsoft.Android.TypeMaps.dll` to verify `CreateInstance` IL is correct

### If above doesn't help
4. **Test with type that HAS activation ctor** - Try `Activity` directly instead of `MainActivity` to rule out `GetUninitializedObject` pattern
5. **Add try/catch around return** - See if we can catch an exception instead of SIGSEGV
6. **Check native crash dump** - Look at registers/stack at crash time
7. **Simplify CreateInstance** - Try just returning `null` to see if crash persists

### Deeper investigation
8. **Verify UCO wrapper IL** - The wrapper's exception handling and return might be wrong
9. **Check JNI local reference validity** - The handle might be invalid by the time we use it
10. **Memory debugging** - Use AddressSanitizer or similar to detect corruption

## Files Involved

- `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs` - CreatePeer implementation
- `src/Mono.Android/Microsoft.Android.Runtime/ManagedValueManager.cs` - CreatePeer override
- `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs` - Proxy/IL generation
- `samples/HelloWorld/HelloWorld/MainActivity.cs` - Test case

## Commands to Reproduce

```bash
# Build
cd /Users/simonrozsival/Projects/dotnet/android
./dotnet-local.sh build src/Mono.Android/Mono.Android.csproj -v:q --nologo

# Build and install sample
cd samples/HelloWorld/HelloWorld
rm -rf bin obj
../../../dotnet-local.sh build -t:Install -c Release

# Run and check logs
adb logcat -c
adb shell am start -n com.xamarin.android.helloworld/example.MainActivity
sleep 5
adb logcat -d | grep -E "(monodroid|ManagedValue|SIGSEGV|Fatal)"
```

## Notes

- Must use `-c Release` to get CoreCLR (Debug defaults to MonoVM)
- Package name is `com.xamarin.android.helloworld`, activity is `example.MainActivity`
