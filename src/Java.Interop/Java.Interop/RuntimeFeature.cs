using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop
{
	static class RuntimeFeature
	{
		const bool JniRemappingEnabledByDefault = true;
		const string FeatureSwitchPrefix = "Java.Interop.RuntimeFeature.";

		[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (JniRemapping)}")]
		internal static bool JniRemapping { get; } =
			AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (JniRemapping)}", out bool isEnabled)
				? isEnabled
				: JniRemappingEnabledByDefault;
	}
}
