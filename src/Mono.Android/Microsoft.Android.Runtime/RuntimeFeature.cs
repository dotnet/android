using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

static class RuntimeFeature
{
	const bool ManagedTypeMapEnabledByDefault = false;
	const bool IsMonoRuntimeEnabledByDefault = true;
	const bool IsCoreClrRuntimeEnabledByDefault = false;

	const string FeatureSwitchPrefix = "Microsoft.Android.Runtime.RuntimeFeature.";

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}")]
	internal static bool ManagedTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}", out bool isEnabled) ? isEnabled : ManagedTypeMapEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsMonoRuntime)}")]
	internal static bool IsMonoRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsMonoRuntime)}", out bool isEnabled) ? isEnabled : IsMonoRuntimeEnabledByDefault;
		
	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsCoreClrRuntime)}")]
	internal static bool IsCoreClrRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsCoreClrRuntime)}", out bool isEnabled) ? isEnabled : IsCoreClrRuntimeEnabledByDefault;
}
