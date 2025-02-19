using System;
using System.Diagnostics.CodeAnalysis;

namespace Android.Runtime;

static class RuntimeFeature
{
	const string FeatureSwitchPrefix = "Android.Runtime.RuntimeFeature.";

	[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (UseReflectionForManagedToJava)}")]
	internal static bool UseReflectionForManagedToJava { get; } =
		AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (UseReflectionForManagedToJava)}", out bool isEnabled) ? isEnabled : false;
}
