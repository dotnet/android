// Some enums we import from Mono.Android, like Android.Content.PM.LaunchMode, contain this
// attribute which does not exist in netstandard2.0, thus we'll include our own private copy
// that gets removed at compile time via [Conditional ("NEVER")].

#if !NET
namespace System.Runtime.Versioning
{
	[System.Diagnostics.Conditional ("NEVER")]
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
	sealed class SupportedOSPlatformAttribute : Attribute
	{
		public SupportedOSPlatformAttribute (string platformName) { }
	}
}
#endif
