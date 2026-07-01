using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public readonly record struct AssemblyInput (string Name, string Path, PEReader Reader);
