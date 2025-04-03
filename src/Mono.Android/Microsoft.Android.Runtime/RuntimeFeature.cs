using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

static class RuntimeFeature
{
	const string FeatureSwitchPrefix = "Microsoft.Android.Runtime.RuntimeFeature.";

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}")]
	internal static bool ManagedTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}", out bool isEnabled) ? isEnabled : false;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (XaHttpClientHandlerTypeEnvironmentVariable)}")]
	[FeatureGuard (typeof (RequiresUnreferencedCodeAttribute))]
	internal static bool XaHttpClientHandlerTypeEnvironmentVariable { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (XaHttpClientHandlerTypeEnvironmentVariable)}", out bool isEnabled) ? isEnabled : false;
}
