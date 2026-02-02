using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

static class RuntimeFeature
{
	const bool IsMonoRuntimeEnabledByDefault = true;
	const bool IsCoreClrRuntimeEnabledByDefault = false;
	const bool IsAssignableFromCheckEnabledByDefault = true;
	const bool StartupHookSupportEnabledByDefault = true;
	const bool IsDynamicTypeRegistrationEnabledByDefault = true;
	const bool UseTrimmableTypeMapEnabledByDefault = false;

	const string FeatureSwitchPrefix = "Microsoft.Android.Runtime.RuntimeFeature.";
	const string StartupHookProviderSwitch = "System.StartupHookProvider.IsSupported";

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

	/// <summary>
	/// When false, dynamic type registration via RegisterJniNatives is not supported.
	/// Trimmable TypeMap sets this to false since all types are registered at build time.
	/// </summary>
	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsDynamicTypeRegistration)}")]
	[FeatureGuard (typeof (RequiresUnreferencedCodeAttribute))]
	internal static bool IsDynamicTypeRegistration { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsDynamicTypeRegistration)}", out bool isEnabled) ? isEnabled : IsDynamicTypeRegistrationEnabledByDefault;

	/// <summary>
	/// When true, use the TrimmableTypeMap instead of LlvmIrTypeMap.
	/// TrimmableTypeMap is AOT and trimming safe.
	/// </summary>
	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (UseTrimmableTypeMap)}")]
	internal static bool UseTrimmableTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (UseTrimmableTypeMap)}", out bool isEnabled) ? isEnabled : UseTrimmableTypeMapEnabledByDefault;
}
