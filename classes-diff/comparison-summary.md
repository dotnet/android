# TypeMap Comparison Summary

## classes.dex Differences

### Classes REMOVED in NewTypeMap (15 classes moved to managed):
| Class | Purpose |
|-------|---------|
| `mono.android.TypeManager` | Main TypeManager (moved to managed) |
| `android.app.ActivityTracker` | Activity lifecycle tracking |
| `android.runtime.JavaProxyThrowable` | Exception proxy |
| `android.runtime.XmlReaderPullParser` | XML parsing |
| `android.runtime.XmlReaderResourceParser` | Resource XML parsing |
| `android.security.KeyChain_KeyChainAliasCallback` | KeyChain callback |
| `mono.android.animation.AnimatorEventDispatcher` | Animation events |
| `mono.android.app.TabEventDispatcher` | Tab events |
| `mono.android.os.ActionHandlerCallback` | Handler callback |
| `mono.android.runtime.InputStreamAdapter` | Stream adapter |
| `mono.android.runtime.JavaObject` | Java object wrapper |
| `mono.android.runtime.OutputStreamAdapter` | Stream adapter |
| `xamarin.android.net.ServerCertificateCustomValidator_*` | SSL validation (3 classes) |

**Net reduction: 15 JCW classes removed**

## DLL Type Differences

### Types REMOVED in NewTypeMap:
- `Java.Interop.ManagedMarshalMethodsLookupTable` - Replaced by TypeMapAttribute system
- `Java.Nio.Channels.FileChannelInvoker` - Invoker trimmed
- `Java.Nio.Channels.Spi.AbstractInterruptibleChannelInvoker` - Invoker trimmed
- `Java.Interop.ExportParameterAttribute` - Export system replaced

### Types ADDED in NewTypeMap:
- `_Microsoft.Android.TypeMaps.*_Proxy` (28 proxy classes) - Generated proxy wrappers
- `Android.Runtime.TypeMapAttributeTypeMap` - New attribute-based TypeMap
- `Android.Runtime.TypeMapTypeManager` - New type manager
- `Android.Runtime.ITypeMap` - TypeMap interface
- `Java.Interop.JavaPeerProxy` - Base proxy class
- `Java.Interop.IAndroidCallableWrapper` - Wrapper interface
- `Java.Lang.Runnable` - Now included in linked output

**Net increase: 31 types (mostly proxy classes in _Microsoft.Android.TypeMaps.dll)**

## Size Comparison

| Artifact | NET10 Mono | NET10 CoreCLR | NET11 NewTypeMap |
|----------|------------|---------------|------------------|
| classes.dex classes | 36 | 345 | 330 |
| DLL types | 228 | 236 | 267 |
| APK size | 3.4M | 8.6M | 8.6M |
