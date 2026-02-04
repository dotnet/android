# NativeAOT SSL/HTTPS Fix

This document describes the issues that prevented HTTPS/SSL from working in NativeAOT builds and the solutions applied.

## The Problem

HTTPS requests in NativeAOT builds failed with:
```
No implementation found for boolean net.dot.android.crypto.DotnetProxyTrustManager.verifyRemoteCertificate(long)
```

## Root Causes

There were **5 separate issues** that all needed to be fixed:

### 1. Crypto JAR Missing from APK

**Issue**: The `libSystem.Security.Cryptography.Native.Android.jar` file wasn't included in the APK for NativeAOT builds. This JAR contains Java classes like `DotnetProxyTrustManager` that the .NET crypto library uses.

**Location**: The JAR is in the NativeAOT runtime pack at:
```
$(IlcFrameworkPath)libSystem.Security.Cryptography.Native.Android.jar
```

**Fix**: Added `_IncludeCryptoJarForNativeAot` target that copies the JAR to `$(IntermediateOutputPath)android-nativeaot/` so it gets compiled to DEX and included in the APK.

### 2. R8/ProGuard Stripping Java Classes

**Issue**: Even when the JAR was included, R8 (ProGuard) was stripping the `net.dot.android.crypto.*` classes because they appeared unused from Java's perspective (they're only called via JNI).

**Fix**: Added `_AddCryptoProguardRulesForNativeAot` target that generates a ProGuard rules file with:
```
-keep class net.dot.android.crypto.** { *; }
```

### 3. --gc-sections Removing Native Crypto Code

**Issue**: The NativeAOT linker uses `--gc-sections` which eliminates "unreachable" code. The crypto native code appeared unreachable because it's called dynamically via JNI, not through static references.

**Why it happened**: In `Microsoft.NETCore.Native.targets`, gc-sections is added when:
```xml
<LinkerArg Include="-Wl,--gc-sections" Condition="'$(LinkerFlavor)' == '' or '$(LinkerFlavor)' == 'bfd' or '$(LinkerFlavor)' == 'lld'" />
```

**Fix**: Set `<LinkerFlavor>android</LinkerFlavor>` in a PropertyGroup to bypass this condition. This causes an invalid `-fuse-ld=android` flag, but we add `-fuse-ld=lld` later which overrides it (last flag wins).

### 4. Crypto JNI Init Handler Not Registered

**Issue**: The crypto library needs `AndroidCryptoNative_InitLibraryOnLoad(JavaVM*, void*)` called during `JNI_OnLoad` to initialize its JNI references. Without this, calling any crypto function caused a SIGSEGV (null JNIEnv pointer).

**Fix**: Added the init handler to a static ItemGroup (must be static, not in a target):
```xml
<ItemGroup>
  <AndroidStaticJniInitFunction Include="AndroidCryptoNative_InitLibraryOnLoad" />
</ItemGroup>
```

This gets picked up by `GenerateNativeAotLibraryLoadAssemblerSources` which generates `jni_init_funcs.ll` with the handler array.

### 5. JNI Callback Symbol Not Exported

**Issue**: The `Java_net_dot_android_crypto_DotnetProxyTrustManager_verifyRemoteCertificate` symbol was in the binary (verified with `strings`) but not in the dynamic symbol table (verified with `nm -D`). The symbol was LOCAL, not GLOBAL, so Java couldn't find it.

**Why it happened**: NativeAOT uses a version script (`.exports` file) to control symbol visibility. Only symbols explicitly listed are exported. The crypto JNI callback wasn't in the list.

**Fix**: Added code to write the crypto symbol to an exports file and append it using `AppendMarshalMethodExports`:
```xml
<ItemGroup>
  <_CryptoExportSymbol Include="Java_net_dot_android_crypto_DotnetProxyTrustManager_verifyRemoteCertificate" />
</ItemGroup>
<WriteLinesToFile File="$(_CryptoExportsFile)" Lines="@(_CryptoExportSymbol)" Overwrite="true" />
<AppendMarshalMethodExports MarshalMethodsExportsFile="$(_CryptoExportsFile)" ExportsFile="$(ExportsFile)" />
```

## Additional Fix: --whole-archive

To ensure all crypto symbols are included (not just the ones referenced), the crypto `.a` file is linked with `--whole-archive`:

```xml
<LinkerArg Include="-Wl,--whole-archive" />
<LinkerArg Include="&quot;$(_CryptoLibPath)&quot;" />
<LinkerArg Include="-Wl,--no-whole-archive" />
```

This is needed because the crypto library is also linked earlier without `--whole-archive`, so we use `--allow-multiple-definition` to handle duplicates.

## Verification

After all fixes, verify:

1. **Crypto JAR in APK**: Check `classes.dex` contains `net.dot.android.crypto` classes
2. **Crypto init called**: Look for log `AndroidCryptoNative_InitLibraryOnLoad` during startup
3. **Symbol exported**: `nm -D libApp.so | grep verifyRemoteCertificate` should show the symbol
4. **HTTPS works**: App can make HTTPS requests with certificate validation

## Files Modified

- `src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.NativeAOT.targets`
  - PropertyGroup: `<LinkerFlavor>android</LinkerFlavor>`
  - ItemGroup: `<AndroidStaticJniInitFunction Include="AndroidCryptoNative_InitLibraryOnLoad" />`
  - Target: `_IncludeCryptoJarForNativeAot`
  - Target: `_AddCryptoProguardRulesForNativeAot`
  - Target: `_AndroidCryptoWholeArchive`
  - Inline in `_CompileTypeMapMarshalMethodsForNativeAot`: Crypto exports appending

## Future Improvement

Only include the crypto JAR, ProGuard rules, and static library if `System.Net.Security.SslStream` survives ILC trimming. This would reduce APK size for apps that don't use HTTPS/SSL.
