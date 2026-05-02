using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

static class RuntimeFeature
{
	const bool ManagedTypeMapEnabledByDefault = false;
	const bool IsMonoRuntimeEnabledByDefault = true;
	const bool IsCoreClrRuntimeEnabledByDefault = false;
	const bool IsAssignableFromCheckEnabledByDefault = true;
	const bool StartupHookSupportEnabledByDefault = true;
	const bool TrimmableTypeMapEnabledByDefault = false;
	const bool ObjectReferenceLoggingEnabledByDefault = false;

	const string FeatureSwitchPrefix = "Microsoft.Android.Runtime.RuntimeFeature.";
	const string StartupHookProviderSwitch = "System.StartupHookProvider.IsSupported";

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}")]
	internal static bool ManagedTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedTypeMap)}", out bool isEnabled) ? isEnabled : ManagedTypeMapEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsMonoRuntime)}")]
	internal static bool IsMonoRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsMonoRuntime)}", out bool isEnabled) ? isEnabled : IsMonoRuntimeEnabledByDefault;
		
	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsCoreClrRuntime)}")]
	internal static bool IsCoreClrRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsCoreClrRuntime)}", out bool isEnabled) ? isEnabled : IsCoreClrRuntimeEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsAssignableFromCheck)}")]
	internal static bool IsAssignableFromCheck { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsAssignableFromCheck)}", out bool isEnabled) ? isEnabled : IsAssignableFromCheckEnabledByDefault;

	[FeatureSwitchDefinition (StartupHookProviderSwitch)]
	[FeatureGuard (typeof (RequiresUnreferencedCodeAttribute))]
	internal static bool StartupHookSupport { get; } =
		AppContext.TryGetSwitch (StartupHookProviderSwitch, out bool isEnabled) ? isEnabled : StartupHookSupportEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (TrimmableTypeMap)}")]
	internal static bool TrimmableTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (TrimmableTypeMap)}", out bool isEnabled) ? isEnabled : TrimmableTypeMapEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ObjectReferenceLogging)}")]
	internal static bool ObjectReferenceLogging { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ObjectReferenceLogging)}", out bool isEnabled) ? isEnabled : ObjectReferenceLoggingEnabledByDefault;
}
