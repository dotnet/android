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
