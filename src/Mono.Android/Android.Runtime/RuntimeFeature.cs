using System;
using System.Diagnostics.CodeAnalysis;

namespace Android.Runtime;

static class RuntimeFeature
{
	const string FeatureSwitchPrefix = "Android.Runtime.RuntimeFeature.";

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}")]
	internal static bool ManagedTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}", out bool isEnabled) ? isEnabled : false;
}
