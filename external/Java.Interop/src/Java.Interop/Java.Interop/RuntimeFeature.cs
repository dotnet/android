using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop
{
	static class RuntimeFeature
	{
		const bool ManagedPeerNativeRegistrationEnabledByDefault = true;
		const string FeatureSwitchPrefix = "Java.Interop.RuntimeFeature.";

		[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedPeerNativeRegistration)}")]
		internal static bool ManagedPeerNativeRegistration =>
			AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedPeerNativeRegistration)}", out bool isEnabled)
			? isEnabled
			: ManagedPeerNativeRegistrationEnabledByDefault;
	}
}
