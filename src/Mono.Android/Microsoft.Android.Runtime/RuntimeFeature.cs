using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

static class RuntimeFeature
{
	const bool ManagedTypeMapEnabledByDefault = false;
	const bool IsMonoRuntimeEnabledByDefault = true;
	const bool IsCoreClrRuntimeEnabledByDefault = false;
	const bool IsNativeAotRuntimeEnabledByDefault = false;
	const bool IsAssignableFromCheckEnabledByDefault = true;
	const bool StartupHookSupportEnabledByDefault = true;
	const bool TrimmableTypeMapEnabledByDefault = false;
	const bool ObjectReferenceLoggingEnabledByDefault = false;
	const bool LegacyJniRegistrationEnabledByDefault = false;

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

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsNativeAotRuntime)}")]
	internal static bool IsNativeAotRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsNativeAotRuntime)}", out bool isEnabled) ? isEnabled : IsNativeAotRuntimeEnabledByDefault;

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

	// When enabled (together with the trimmable typemap), re-enables the legacy, reflection-based
	// JNI native method registration (NativeMethodRegistrar / Java.Interop.ManagedPeer). This is
	// required to support legacy precompiled Java Callable Wrappers (from binding jars/aars) whose
	// static initializers call `mono.android.Runtime.register(...)`, and `ManagedPeer`. Disabled by
	// default so the reflection-based path can be trimmed away when it is not needed.
	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (LegacyJniRegistration)}")]
	internal static bool LegacyJniRegistration { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (LegacyJniRegistration)}", out bool isEnabled) ? isEnabled : LegacyJniRegistrationEnabledByDefault;
}
