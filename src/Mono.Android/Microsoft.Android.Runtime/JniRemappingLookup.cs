#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class JniRemappingLookup
{
#pragma warning disable CS0649 // Field 'JniRemappingLookup.JniRemappingReplacementMethod.target_type' is never assigned to, and will always have its default value null
	struct JniRemappingReplacementMethod
	{
		public string? target_type;
		public string? target_name;
		public bool    is_static;
	}
#pragma warning restore CS0649

	internal static IReadOnlyList<string> GetStaticMethodFallbackTypes (string jniSimpleReference, bool useReplacementTypes)
	{
		int slash = jniSimpleReference.LastIndexOf ('/');
		var desugarType = slash > 0
			? $"{jniSimpleReference.Substring (0, slash + 1)}Desugar{jniSimpleReference.Substring (slash + 1)}"
			: $"Desugar{jniSimpleReference}";

		var typeWithPrefix = $"{desugarType}$_CC";
		var typeWithSuffix = $"{jniSimpleReference}$-CC";

		var replacements = new[] {
			useReplacementTypes ? GetReplacementType (typeWithPrefix) ?? typeWithPrefix : typeWithPrefix,
			useReplacementTypes ? GetReplacementType (typeWithSuffix) ?? typeWithSuffix : typeWithSuffix,
		};

		if (useReplacementTypes && Logger.LogAssembly) {
			var message = $"Remapping type `{jniSimpleReference}` to one of {{ `{replacements [0]}`, `{replacements [1]}` }}";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return replacements;
	}

	internal static string? GetReplacementType (string? jniSimpleReference)
	{
		if (jniSimpleReference is null || !JNIEnvInit.jniRemappingInUse) {
			return null;
		}

		IntPtr ret = RuntimeNativeMethods._monodroid_lookup_replacement_type (jniSimpleReference);
		if (ret == IntPtr.Zero) {
			return null;
		}

		return Marshal.PtrToStringAnsi (ret);
	}

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, string jniMethodName, string jniMethodSignature)
	{
		if (!JNIEnvInit.jniRemappingInUse) {
			return null;
		}

		IntPtr retInfo = RuntimeNativeMethods._monodroid_lookup_replacement_method_info (jniSourceType, jniMethodName, jniMethodSignature);
		if (retInfo == IntPtr.Zero) {
			return null;
		}

		var method = Marshal.PtrToStructure<JniRemappingReplacementMethod> (retInfo);
		var targetType = method.target_type ?? throw new InvalidOperationException (
			$"JNI remapping entry for `{jniSourceType}.{jniMethodName}{jniMethodSignature}` is missing a target type.");
		var targetName = method.target_name ?? throw new InvalidOperationException (
			$"JNI remapping entry for `{jniSourceType}.{jniMethodName}{jniMethodSignature}` is missing a target method name.");
		var newSignature = jniMethodSignature;

		int? paramCount = null;
		if (method.is_static) {
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (jniMethodSignature) + 1;
			newSignature = $"(L{jniSourceType};" + jniMethodSignature.Substring ("(".Length);
		}

		if (Logger.LogAssembly) {
			var message = $"Remapping method `{jniSourceType}.{jniMethodName}{jniMethodSignature}` to " +
				$"`{targetType}.{targetName}{newSignature}`; " +
				$"param-count: {paramCount}; instance-to-static? {method.is_static}";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return new JniRuntime.ReplacementMethodInfo {
				SourceJniType                   = jniSourceType,
				SourceJniMethodName             = jniMethodName,
				SourceJniMethodSignature        = jniMethodSignature,
				TargetJniType                   = targetType,
				TargetJniMethodName             = targetName,
				TargetJniMethodSignature        = newSignature,
				TargetJniMethodParameterCount   = paramCount,
				TargetJniMethodInstanceToStatic = method.is_static,
		};
	}
}
