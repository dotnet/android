using System;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Entry point for typemap initialization. The implementation is generated at build time
/// by the trimmable typemap generator, replacing this reference assembly.
/// </summary>
public static class TypeMapLoader
{
	/// <summary>
	/// Initializes the trimmable typemap by constructing the type mapping dictionaries
	/// and calling <c>TrimmableTypeMap.Initialize()</c>.
	/// </summary>
	public static void Initialize () => throw new NotImplementedException (
		"This is a reference assembly stub. The real implementation is generated at build time.");
}
