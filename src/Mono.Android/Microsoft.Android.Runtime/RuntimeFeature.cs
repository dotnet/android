using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

static class RuntimeFeature
{
	const bool IsCoreClrRuntimeEnabledByDefault = false;
	const bool IsNativeAotRuntimeEnabledByDefault = false;
	const bool IsAssignableFromCheckEnabledByDefault = true;
	const bool StartupNoGCRegionEnabledByDefault = true;
	const bool StartupHookSupportEnabledByDefault = true;
	const bool TrimmableTypeMapEnabledByDefault = false;
	const bool UseTypeMapAttributesForJavaDictionaryValueTypeLookupsEnabledByDefault = false;
	const bool ObjectReferenceLoggingEnabledByDefault = false;
	const bool GCBridgeLoggingEnabledByDefault = true;
	const bool ManagedToJavaUsesAssemblyFullNameEnabledByDefault = false;

	const string FeatureSwitchPrefix = "Microsoft.Android.Runtime.RuntimeFeature.";
	const string EventSourceSupportSwitch = "System.Diagnostics.Tracing.EventSource.IsSupported";
	const string StartupHookProviderSwitch = "System.StartupHookProvider.IsSupported";

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsCoreClrRuntime)}")]
	internal static bool IsCoreClrRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsCoreClrRuntime)}", out bool isEnabled) ? isEnabled : IsCoreClrRuntimeEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsNativeAotRuntime)}")]
	internal static bool IsNativeAotRuntime { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsNativeAotRuntime)}", out bool isEnabled) ? isEnabled : IsNativeAotRuntimeEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (IsAssignableFromCheck)}")]
	internal static bool IsAssignableFromCheck { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (IsAssignableFromCheck)}", out bool isEnabled) ? isEnabled : IsAssignableFromCheckEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (StartupNoGCRegion)}")]
	internal static bool StartupNoGCRegion { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (StartupNoGCRegion)}", out bool isEnabled) ? isEnabled : StartupNoGCRegionEnabledByDefault;

	[FeatureSwitchDefinition (StartupHookProviderSwitch)]
	[FeatureGuard (typeof (RequiresUnreferencedCodeAttribute))]
	internal static bool StartupHookSupport { get; } =
		AppContext.TryGetSwitch (StartupHookProviderSwitch, out bool isEnabled) ? isEnabled : StartupHookSupportEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (TrimmableTypeMap)}")]
	internal static bool TrimmableTypeMap { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (TrimmableTypeMap)}", out bool isEnabled) ? isEnabled : TrimmableTypeMapEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (UseTypeMapAttributesForJavaDictionaryValueTypeLookups)}")]
	internal static bool UseTypeMapAttributesForJavaDictionaryValueTypeLookups { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (UseTypeMapAttributesForJavaDictionaryValueTypeLookups)}", out bool isEnabled) ? isEnabled : UseTypeMapAttributesForJavaDictionaryValueTypeLookupsEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ObjectReferenceLogging)}")]
	internal static bool ObjectReferenceLogging { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ObjectReferenceLogging)}", out bool isEnabled) ? isEnabled : ObjectReferenceLoggingEnabledByDefault;

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (GCBridgeLogging)}")]
	internal static bool GCBridgeLogging { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (GCBridgeLogging)}", out bool isEnabled) ? isEnabled : GCBridgeLoggingEnabledByDefault;

	[FeatureSwitchDefinition (EventSourceSupportSwitch)]
	internal static bool EventSourceSupport { get; } =
		!AppContext.TryGetSwitch (EventSourceSupportSwitch, out bool isEnabled) || isEnabled;

	// Enabled for Debug builds, whose string-based typemaps support Fast Deployment without embedding assembly MVIDs.
	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (ManagedToJavaUsesAssemblyFullName)}")]
	internal static bool ManagedToJavaUsesAssemblyFullName { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (ManagedToJavaUsesAssemblyFullName)}", out bool isEnabled) ? isEnabled : ManagedToJavaUsesAssemblyFullNameEnabledByDefault;
}
