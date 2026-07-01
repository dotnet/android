using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop
{
	static class RuntimeFeature
	{
		const bool ManagedPeerNativeRegistrationEnabledByDefault = true;
		const string FeatureSwitchPrefix = "Java.Interop.RuntimeFeature.";

		[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedPeerNativeRegistration)}")]
		[FeatureGuard (typeof (RequiresUnreferencedCodeAttribute))]
		internal static bool ManagedPeerNativeRegistration { get; } =
			AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedPeerNativeRegistration)}", out bool isEnabled)
				? isEnabled
				: ManagedPeerNativeRegistrationEnabledByDefault;
	}
}
