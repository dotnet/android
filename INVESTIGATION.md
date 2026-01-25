# TypeMap v2 SIGSEGV Investigation

## Summary

We're migrating to TypeMap API v2 which uses compile-time generated proxies instead of reflection-based type lookup. The app was crashing with SIGSEGV immediately after `CreatePeer` successfully created a managed object.

## **RESOLVED** ✅

### Root Cause

**Method index mismatch between LLVM IR stubs and IL `GetFunctionPointer`.**

The LLVM IR generation (`GenerateLlvmIr`) and the IL proxy's `GetFunctionPointer` method were using different index orderings:

- **LLVM IR stubs**: Regular methods at indices 0..n-1, activation ctors at indices n..m-1
- **IL GetFunctionPointer**: Activation ctors at indices 0..k-1, regular methods at k..m-1

This caused the wrong function pointer to be returned when JNI called `GetFunctionPointer(0)` for `onCreate` - it got the activation ctor pointer instead of the `n_onCreate` wrapper.

The result was that when the native code called the function pointer with `(env, this, bundle)` arguments, it was actually calling the wrong wrapper which expected different arguments, corrupting the stack.

### The Fix

Changed `GenerateUcoWrappers` to generate wrappers in the same order as the LLVM IR:
1. Regular marshal methods first (indices 0..n-1)
2. Activation constructors second (indices n..m-1)

### Key Evidence

The argument value `native_savedInstanceState=0x7542a39c08` was the **function pointer address** from `GetFunctionPointer: Returning 0x7542A39C08`, not a JNI reference. This proved the indices were mismatched.

### Recommendation for Future

**Refactor to generate IL, Java JCW, and LLVM IR in a single unified loop** instead of separate loops that must stay synchronized. Having three independent loops with matching indices is error-prone.

## Previous Investigation Notes

### What Works
- TypeMap lookup finds the correct proxy type (`HelloWorld_MainActivity_Proxy`)
- `GetFunctionPointer` returns valid function pointers for method indices
- Proxy's `CreateInstance` successfully creates `HelloWorld.MainActivity` instance
- `PeerReference.Handle` is set correctly (e.g., `0x3986`)
- All logging in `TypeMapAttributeTypeMap.CreatePeer` completes successfully

### The Crash (FIXED)
- **Fault address**: `0x5802000f` (consistent across runs)
- **Signal**: SIGSEGV (SEGV_MAPERR)
- **Cause**: `java.lang.System.identityHashCode` called with corrupted object reference

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

## Files Modified

- `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs`:
  - Changed `GenerateUcoWrappers` to generate regular methods FIRST, then activation ctors
  - Added debug logging via `EmitLogCall` helper
  - Added `_logInfoRef` and `_androidLogTypeRef` for Android.Util.Log.Info

- `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs` - Added debug logging
- `src/Mono.Android/Microsoft.Android.Runtime/ManagedValueManager.cs` - Added debug logging  
- `src/Mono.Android/Java.Lang/Object.cs` - Added debug logging
- `samples/HelloWorld/HelloWorld/MainActivity.cs` - Added argument logging, disabled Click handler

## Commands to Reproduce

```bash
# Build
cd /Users/simonrozsival/Projects/dotnet/android
./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj -v:q --nologo

# Build and install sample (must use Release for CoreCLR)
cd samples/HelloWorld/HelloWorld
rm -rf bin obj
../../../dotnet-local.sh build -t:Install -c Release

# Run and check logs
adb logcat -c
adb shell am start -n com.xamarin.android.helloworld/example.MainActivity
sleep 5
adb logcat -d | grep -E "(UCO-wrapper|n_onCreate|monodroid)"
```

## Notes

- Must use `-c Release` to get CoreCLR (Debug defaults to MonoVM)
- Package name is `com.xamarin.android.helloworld`, activity is `example.MainActivity`
- The sample's Click handler is disabled because `View_OnClickListenerImplementor` JCW is not generated yet
