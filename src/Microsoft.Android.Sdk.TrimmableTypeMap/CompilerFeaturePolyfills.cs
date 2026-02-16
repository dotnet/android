// Polyfills for C# language features on netstandard2.0

// Required for init-only setters
namespace System.Runtime.CompilerServices
{
	static class IsExternalInit { }

	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	sealed class RequiredMemberAttribute : Attribute { }

	[AttributeUsage (AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	sealed class CompilerFeatureRequiredAttribute (string featureName) : Attribute
	{
		public string FeatureName { get; } = featureName;
		public bool IsOptional { get; init; }
	}
}

namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage (AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
	sealed class SetsRequiredMembersAttribute : Attribute { }
}
