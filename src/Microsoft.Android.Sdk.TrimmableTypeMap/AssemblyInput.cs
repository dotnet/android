using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <param name="ScanForPeers">
/// When true (default) the assembly is scanned and its Java peers are emitted into a typemap.
/// When false the assembly is only indexed for base-type resolution (e.g. Mono.Android on the app
/// build, whose typemap is pre-generated at SDK build time, issue #10792) and no peers are emitted.
/// </param>
public readonly record struct AssemblyInput (string Name, string Path, PEReader Reader, bool ScanForPeers = true);
