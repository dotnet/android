#### Deprecations, removals, and default configuration changes

- [Mono.Android.Export assembly removed](#monoandroidexport-assembly-removed)

### Mono.Android.Export assembly removed

The `Mono.Android.Export.dll` assembly is no longer included in the .NET for
Android targeting or runtime packs. Remove explicit references to this assembly
from application and library projects. Projects using `[Export]` or
`[ExportField]` do not need a replacement reference; the trimmable type map
generates their Java callbacks during the build.

The legacy runtime callback generator has also been removed. Applications must
use the trimmable type map for Java-to-managed callbacks.

With the trimmable type map, explicit `[Export]` and `[ExportField]` declarations
are Java-to-managed entry points, including when `TrimMode` is `full`. Their
declaring Java peer, exported callbacks, and constructors exposed by the Java
callable wrapper are preserved even when there are no managed callers. This
applies to both CoreCLR and NativeAOT, so Java source can instantiate the wrapper
and call its exports without additional managed preservation declarations.

This does not preserve the entire declaring assembly or unrelated managed
members. `[Register]` alone does not make an otherwise unreferenced peer an
entry point, and a derived peer does not become an entry point merely by
inheriting an exported method.
