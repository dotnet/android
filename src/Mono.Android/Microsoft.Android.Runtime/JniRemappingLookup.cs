#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class JniStaticMethodFallback
{
	internal static IReadOnlyList<string> GetTypes (string jniSimpleReference)
	{
		int slash = jniSimpleReference.LastIndexOf ('/');
		var desugarType = slash > 0
			? $"{jniSimpleReference.Substring (0, slash + 1)}Desugar{jniSimpleReference.Substring (slash + 1)}"
			: $"Desugar{jniSimpleReference}";

		return [$"{desugarType}$_CC", $"{jniSimpleReference}$-CC"];
	}
}

static class JniRemappingLookup
{
	static readonly ConcurrentDictionary<string, string> reverseTypes = new (StringComparer.Ordinal);
	static readonly object initializationLock = new ();
	static JniRemappingAsset? managedAsset;
	static IntPtr utf8Base;

	internal static void Initialize (JniRemappingAsset asset)
	{
		ArgumentNullException.ThrowIfNull (asset);
		if (asset.MappedAddress == IntPtr.Zero)
			throw new ArgumentException ("Runtime JNI remapping requires a retained read-only mapping.", nameof (asset));

		lock (initializationLock) {
			if (managedAsset is not null)
				throw new InvalidOperationException ("JNI remapping asset has already been initialized.");

			// Java.Interop can retain these UTF-8 pointers for the entire runtime lifetime.
			utf8Base = asset.MappedAddress;
			Volatile.Write (ref managedAsset, asset);
		}
	}

	static JniRemappingAsset GetAsset ()
		=> Volatile.Read (ref managedAsset) ?? throw new InvalidOperationException ("JNI remapping asset has not been initialized.");

	internal static IReadOnlyList<string> GetStaticMethodFallbackTypes (string jniSimpleReference, bool useReplacementTypes)
	{
		if (useReplacementTypes) {
			jniSimpleReference = GetReverseType (jniSimpleReference) ?? jniSimpleReference;
		}

		var fallbackTypes = JniStaticMethodFallback.GetTypes (jniSimpleReference);
		string typeWithPrefix = fallbackTypes [0];
		string typeWithSuffix = fallbackTypes [1];
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
		if (!RuntimeFeature.JniRemapping || jniSimpleReference is null || jniSimpleReference.Length == 0)
			return null;

		var asset = GetAsset ();
		var target = asset.FindReplacementType (jniSimpleReference);
		return target is { } value ? asset.ReadString (value) : null;
	}

	internal static IntPtr GetReplacementTypeUtf8 (string? jniSimpleReference)
	{
		if (!RuntimeFeature.JniRemapping || jniSimpleReference is null || jniSimpleReference.Length == 0)
			return IntPtr.Zero;

		var target = GetAsset ().FindReplacementType (jniSimpleReference);
		return target is { } value ? GetMappedUtf8Pointer (value) : IntPtr.Zero;
	}

	static unsafe IntPtr GetMappedUtf8Pointer (JniRemappingAsset.StringRef value)
	{
		GetAsset ().ReadBytes (value);
		return (IntPtr)((byte*)utf8Base + checked ((int)value.Offset));
	}

	internal static string? GetReverseType (string? jniSimpleReference)
	{
		if (!RuntimeFeature.JniRemapping || jniSimpleReference is null || jniSimpleReference.Length == 0)
			return null;
		if (GetAsset ().IsEmpty)
			return null;

		string replacement = reverseTypes.GetOrAdd (jniSimpleReference, static source => LookupReverseType (source));
		return string.Equals (replacement, jniSimpleReference, StringComparison.Ordinal) ? null : replacement;
	}

	static string LookupReverseType (string jniSimpleReference)
	{
		var asset = GetAsset ();
		var target = asset.FindReverseType (jniSimpleReference);
		return target is { } value ? asset.ReadString (value) : jniSimpleReference;
	}

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, string jniMethodName, string jniMethodSignature)
	{
		var replacement = GetReplacementMethodInfo (jniSourceType, jniMethodName.AsSpan (), jniMethodSignature.AsSpan ());
		if (replacement is not JniRuntime.ReplacementMethodInfo info)
			return null;

		info.SourceJniType = jniSourceType;
		info.SourceJniMethodName = jniMethodName;
		info.SourceJniMethodSignature = jniMethodSignature;
		info.TargetJniType = Marshal.PtrToStringUTF8 (info.TargetJniTypeUtf8)
			?? throw new InvalidOperationException ("JNI remapping target type is not valid UTF-8.");
		info.TargetJniMethodName = Marshal.PtrToStringUTF8 (info.TargetJniMethodNameUtf8)
			?? throw new InvalidOperationException ("JNI remapping target method name is not valid UTF-8.");
		info.TargetJniMethodSignature ??= info.TargetJniMethodSignatureUtf8 == IntPtr.Zero
			? jniMethodSignature
			: Marshal.PtrToStringUTF8 (info.TargetJniMethodSignatureUtf8)
				?? throw new InvalidOperationException ("JNI remapping target method signature is not valid UTF-8.");
		return info;
	}

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		=> GetReplacementMethodInfo (jniSourceType.AsSpan (), IntPtr.Zero, jniMethodName, jniMethodSignature);

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (IntPtr jniSourceTypeUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
	{
		if (jniSourceTypeUtf8 == IntPtr.Zero)
			throw new ArgumentNullException (nameof (jniSourceTypeUtf8));
		return GetReplacementMethodInfo (default, jniSourceTypeUtf8, jniMethodName, jniMethodSignature);
	}

	static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
		ReadOnlySpan<char> jniSourceType,
		IntPtr jniSourceTypeUtf8,
		ReadOnlySpan<char> jniMethodName,
		ReadOnlySpan<char> jniMethodSignature)
	{
		if (!RuntimeFeature.JniRemapping)
			return null;

		var asset = GetAsset ();
		var remapped = jniSourceTypeUtf8 == IntPtr.Zero
			? asset.FindMethod (jniSourceType, jniMethodName, jniMethodSignature)
			: asset.FindMethod (GetNullTerminatedUtf8Span (jniSourceTypeUtf8), jniMethodName, jniMethodSignature);
		if (remapped is not { } entry)
			return null;

		IntPtr targetType = GetMappedUtf8Pointer (entry.TargetType);
		IntPtr targetName = GetMappedUtf8Pointer (entry.TargetName);
		IntPtr targetSignatureUtf8 = entry.TargetSignature.IsMissing ? IntPtr.Zero : GetMappedUtf8Pointer (entry.TargetSignature);
		IntPtr matchedSignatureUtf8 = entry.MatchedSignature.Length == 0 ? IntPtr.Zero : GetMappedUtf8Pointer (entry.MatchedSignature);
		int? paramCount = null;
		string? targetSignature = null;
		if (entry.IsStatic) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			string sourceSignature = jniMethodSignature.ToString ();
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (sourceSignature) + 1;
			targetSignature = targetSignatureUtf8 == IntPtr.Zero
				? $"(L{sourceType};" + sourceSignature.Substring ("(".Length)
				: Marshal.PtrToStringUTF8 (targetSignatureUtf8);
		}

		var ret = new JniRuntime.ReplacementMethodInfo {
			TargetJniTypeUtf8               = targetType,
			TargetJniMethodNameUtf8         = targetName,
			TargetJniMethodSignature        = targetSignature,
			TargetJniMethodSignatureUtf8    = entry.IsStatic
				? IntPtr.Zero
				: targetSignatureUtf8 == IntPtr.Zero ? matchedSignatureUtf8 : targetSignatureUtf8,
			TargetJniMethodParameterCount   = paramCount,
			TargetJniMethodInstanceToStatic = entry.IsStatic,
		};

		if (Logger.LogAssembly) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			string targetTypeName = Marshal.PtrToStringUTF8 (targetType) ?? "";
			string targetMethodName = Marshal.PtrToStringUTF8 (targetName) ?? "";
			string effectiveTargetSignature = targetSignature ?? (ret.TargetJniMethodSignatureUtf8 == IntPtr.Zero
				? jniMethodSignature.ToString ()
				: Marshal.PtrToStringUTF8 (ret.TargetJniMethodSignatureUtf8) ?? "");
			var message = $"Remapping method `{sourceType}.{jniMethodName}{jniMethodSignature}` to " +
				$"`{targetTypeName}.{targetMethodName}{effectiveTargetSignature}`; " +
				$"param-count: {paramCount}; instance-to-static? {entry.IsStatic}";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return ret;
	}

	internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (string jniSourceType, string jniFieldName, string jniFieldSignature)
		=> GetReplacementFieldInfo (jniSourceType, jniFieldName.AsSpan (), jniFieldSignature.AsSpan ());

	internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (
		string jniSourceType,
		ReadOnlySpan<char> jniFieldName,
		ReadOnlySpan<char> jniFieldSignature)
	{
		if (!RuntimeFeature.JniRemapping)
			return null;

		var asset = GetAsset ();
		var remapped = asset.FindField (jniSourceType, jniFieldName, jniFieldSignature);
		if (remapped is not { } entry)
			return null;

		string targetType = asset.ReadString (entry.TargetType);
		string targetName = asset.ReadString (entry.TargetName);
		string targetSignature = entry.TargetSignature.IsMissing
			? jniFieldSignature.ToString ()
			: asset.ReadString (entry.TargetSignature);

		if (Logger.LogAssembly) {
			var message = $"Remapping field `{jniSourceType}.{jniFieldName}:{jniFieldSignature}` to " +
				$"`{targetType}.{targetName}:{targetSignature}`";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return new JniRuntime.ReplacementFieldInfo {
			SourceJniType = jniSourceType,
			SourceJniFieldName = jniFieldName.ToString (),
			SourceJniFieldSignature = jniFieldSignature.ToString (),
			TargetJniType = targetType,
			TargetJniFieldName = targetName,
			TargetJniFieldSignature = targetSignature,
		};
	}

	static string GetSourceTypeForDiagnostics (ReadOnlySpan<char> jniSourceType, IntPtr jniSourceTypeUtf8)
	{
		if (jniSourceTypeUtf8 == IntPtr.Zero)
			return jniSourceType.ToString ();
		return Marshal.PtrToStringUTF8 (jniSourceTypeUtf8) ?? "";
	}

	static unsafe ReadOnlySpan<byte> GetNullTerminatedUtf8Span (IntPtr value)
	{
		byte* start = (byte*)value;
		int length = 0;
		while (start [length] != 0)
			length++;
		return new ReadOnlySpan<byte> (start, length);
	}
}
