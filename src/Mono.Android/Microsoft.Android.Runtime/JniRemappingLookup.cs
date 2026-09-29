#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class JniRemappingLookup
{
	static readonly ConcurrentDictionary<string, string> reverseTypes = new (StringComparer.Ordinal);
	static readonly object initializationLock = new ();
	static JniRemappingAsset? managedAsset;
	static GCHandle pinnedAsset;

	internal static void Initialize (ReadOnlySpan<byte> data)
	{
		lock (initializationLock) {
			if (managedAsset is not null)
				throw new InvalidOperationException ("JNI remapping asset has already been initialized.");

			var parsed = new JniRemappingAsset (data);
			reverseTypes.Clear ();
			// Java.Interop can retain UTF-8 pointers for the entire runtime lifetime.
			pinnedAsset = GCHandle.Alloc (parsed.Storage, GCHandleType.Pinned);
			Volatile.Write (ref managedAsset, parsed);
		}
	}

	static JniRemappingAsset GetAsset ()
		=> Volatile.Read (ref managedAsset) ?? throw new InvalidOperationException ("JNI remapping asset has not been initialized.");

	internal static IReadOnlyList<string> GetStaticMethodFallbackTypes (string jniSimpleReference, bool useReplacementTypes)
	{
		if (useReplacementTypes) {
			jniSimpleReference = GetReverseType (jniSimpleReference) ?? jniSimpleReference;
		}

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
		var asset = GetAsset ();
		if (jniSimpleReference is null || jniSimpleReference.Length == 0)
			return null;
		var target = asset.FindReplacementType (jniSimpleReference);
		return target is { } value ? asset.ReadString (value) : null;
	}

	internal static IntPtr GetReplacementTypeUtf8 (string? jniSimpleReference)
	{
		var asset = GetAsset ();
		if (jniSimpleReference is null || jniSimpleReference.Length == 0)
			return IntPtr.Zero;
		var target = asset.FindReplacementType (jniSimpleReference);
		return target is { } value ? GetPinnedUtf8Pointer (value) : IntPtr.Zero;
	}

	static unsafe IntPtr GetPinnedUtf8Pointer (JniRemappingAsset.StringRef value)
		=> (IntPtr)((byte*)pinnedAsset.AddrOfPinnedObject () + checked ((int)value.Offset));

	internal static string? GetReverseType (string? jniSimpleReference)
	{
		var asset = GetAsset ();
		if (jniSimpleReference is null || jniSimpleReference.Length == 0 || asset.IsEmpty)
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
		=> GetReplacementMethodInfo (jniSourceType, jniMethodName.AsSpan (), jniMethodSignature.AsSpan ());

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
		var asset = GetAsset ();
		var remapped = jniSourceTypeUtf8 == IntPtr.Zero
			? asset.FindMethod (jniSourceType, jniMethodName, jniMethodSignature)
			: asset.FindMethod (GetNullTerminatedUtf8Span (jniSourceTypeUtf8), jniMethodName, jniMethodSignature);
		if (remapped is not { } entry)
			return null;
		return CreateReplacementMethodInfo (
			GetPinnedUtf8Pointer (entry.TargetType),
			GetPinnedUtf8Pointer (entry.TargetName),
			entry.TargetSignature.IsMissing ? IntPtr.Zero : GetPinnedUtf8Pointer (entry.TargetSignature),
			entry.MatchedSignature.Length == 0 ? IntPtr.Zero : GetPinnedUtf8Pointer (entry.MatchedSignature),
			entry.IsStatic, jniSourceType, jniSourceTypeUtf8, jniMethodName, jniMethodSignature);
	}

	static JniRuntime.ReplacementMethodInfo CreateReplacementMethodInfo (
		IntPtr targetType, IntPtr targetName, IntPtr targetSignatureUtf8, IntPtr matchedSignatureUtf8,
		bool isStatic, ReadOnlySpan<char> jniSourceType, IntPtr jniSourceTypeUtf8,
		ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
	{
		int? paramCount = null;
		string? targetSignature = null;
		if (isStatic) {
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
			TargetJniMethodSignatureUtf8    = isStatic
				? IntPtr.Zero
				: targetSignatureUtf8 == IntPtr.Zero ? matchedSignatureUtf8 : targetSignatureUtf8,
			TargetJniMethodParameterCount   = paramCount,
			TargetJniMethodInstanceToStatic = isStatic,
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
				$"param-count: {paramCount}; instance-to-static? {isStatic}";
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
		var asset = GetAsset ();
		var remapped = asset.FindField (jniSourceType, jniFieldName, jniFieldSignature);
		if (remapped is not { } entry)
			return null;
		return CreateReplacementFieldInfo (
			jniSourceType, jniFieldName, jniFieldSignature,
			asset.ReadString (entry.TargetType), asset.ReadString (entry.TargetName),
			entry.TargetSignature.IsMissing ? null : asset.ReadString (entry.TargetSignature));
	}

	static JniRuntime.ReplacementFieldInfo CreateReplacementFieldInfo (
		string jniSourceType, ReadOnlySpan<char> jniFieldName, ReadOnlySpan<char> jniFieldSignature,
		string targetType, string targetName, string? targetSignature)
	{
		targetSignature ??= jniFieldSignature.ToString ();

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
