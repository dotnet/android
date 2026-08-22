using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace Java.Interop
{
	static class RuntimeFeature
	{
		const bool ManagedPeerNativeRegistrationEnabledByDefault = true;
		const bool InteropEventSourceEnabledByDefault = false;
		const string FeatureSwitchPrefix = "Java.Interop.RuntimeFeature.";

		[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedPeerNativeRegistration)}")]
		[FeatureGuard (typeof (RequiresUnreferencedCodeAttribute))]
		internal static bool ManagedPeerNativeRegistration { get; } =
			AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedPeerNativeRegistration)}", out bool isEnabled)
				? isEnabled
				: ManagedPeerNativeRegistrationEnabledByDefault;

		[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (InteropEventSource)}")]
		internal static bool InteropEventSource { get; } =
			AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (InteropEventSource)}", out bool isEnabled)
				? isEnabled
				: InteropEventSourceEnabledByDefault;

		internal static bool IsInteropEventSourceEnabled (EventKeywords keywords)
		{
			return InteropEventSource && global::Java.Interop.InteropEventSource.IsEnabled (keywords);
		}
	}
}
