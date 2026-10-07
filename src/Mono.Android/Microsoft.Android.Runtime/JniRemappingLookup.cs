#nullable enable

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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
	// These retain the native lookup's shape: strings use raw-table offsets and member groups use indexes, not ELF pointers.
	internal readonly record struct NativeJniRemappingString (uint Offset, uint Length);
	internal readonly record struct NativeJniRemappingTypeReplacementEntry (NativeJniRemappingString Name, uint Replacement);
	internal readonly record struct NativeJniRemappingIndexTypeEntry (NativeJniRemappingString Name, uint MethodCount, uint MethodStart);
	internal readonly record struct NativeJniRemappingIndexFieldTypeEntry (NativeJniRemappingString Name, uint FieldCount, uint FieldStart);
	internal readonly record struct NativeJniRemappingReplacementMethod (uint TargetType, uint TargetName, uint TargetSignature, uint IsStatic);
	internal readonly record struct NativeJniRemappingReplacementField (uint TargetType, uint TargetName, uint TargetSignature);
	internal readonly record struct NativeJniRemappingIndexMethodEntry (
		NativeJniRemappingString Name, NativeJniRemappingString Signature, NativeJniRemappingReplacementMethod Replacement);
	internal readonly record struct NativeJniRemappingIndexFieldEntry (
		NativeJniRemappingString Name, NativeJniRemappingString Signature, NativeJniRemappingReplacementField Replacement);

	static bool isInUse;
	static readonly ConcurrentDictionary<string, string> reverseTypes = new (StringComparer.Ordinal);

	internal static void Initialize ()
	{
		reverseTypes.Clear ();
		if (!RuntimeFeature.JniRemapping) {
			isInUse = false;
			return;
		}
		BinaryJniRemappingLookup.Initialize ();
		isInUse = true;
	}

	internal static IReadOnlyList<string> GetStaticMethodFallbackTypes (string jniSimpleReference, bool useReplacementTypes)
	{
		if (useReplacementTypes)
			jniSimpleReference = GetReverseType (jniSimpleReference) ?? jniSimpleReference;

		var fallbackTypes = JniStaticMethodFallback.GetTypes (jniSimpleReference);
		string typeWithPrefix = fallbackTypes [0];
		string typeWithSuffix = fallbackTypes [1];
		var replacements = new [] {
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
		IntPtr replacement = GetReplacementTypeUtf8 (jniSimpleReference);
		return replacement == IntPtr.Zero ? null : Marshal.PtrToStringUTF8 (replacement);
	}

	internal static unsafe IntPtr GetReplacementTypeUtf8 (string? jniSimpleReference)
	{
		if (!RuntimeFeature.JniRemapping || !isInUse || string.IsNullOrEmpty (jniSimpleReference))
			return IntPtr.Zero;
		return (IntPtr)BinaryJniRemappingLookup.LookupType (jniSimpleReference, reverse: false);
	}

	internal static string? GetReverseType (string? jniSimpleReference)
	{
		if (!RuntimeFeature.JniRemapping || !isInUse || string.IsNullOrEmpty (jniSimpleReference))
			return null;
		string replacement = reverseTypes.GetOrAdd (jniSimpleReference, static source => LookupReverseType (source));
		return string.Equals (replacement, jniSimpleReference, StringComparison.Ordinal) ? null : replacement;
	}

	static unsafe string LookupReverseType (string source)
	{
		byte* replacement = BinaryJniRemappingLookup.LookupType (source, reverse: true);
		return replacement == null ? source :
			Marshal.PtrToStringUTF8 ((IntPtr)replacement) ?? throw new InvalidOperationException ("Invalid JNI reverse type.");
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

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
		string jniSourceType, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		=> GetReplacementMethodInfo (jniSourceType.AsSpan (), default, jniMethodName, jniMethodSignature);

	internal static unsafe JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
		IntPtr jniSourceTypeUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
	{
		if (jniSourceTypeUtf8 == IntPtr.Zero)
			throw new ArgumentNullException (nameof (jniSourceTypeUtf8));
		byte* start = (byte*)jniSourceTypeUtf8;
		int length = 0;
		while (start [length] != 0)
			length++;
		return GetReplacementMethodInfo (default, new ReadOnlySpan<byte> (start, length), jniMethodName, jniMethodSignature);
	}

	static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
		ReadOnlySpan<char> source, ReadOnlySpan<byte> sourceUtf8,
		ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
		=> !RuntimeFeature.JniRemapping || !isInUse ? null :
			BinaryJniRemappingLookup.GetMethod (source, sourceUtf8, name, signature);

	internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (string jniSourceType, string jniFieldName, string jniFieldSignature)
		=> GetReplacementFieldInfo (jniSourceType, jniFieldName.AsSpan (), jniFieldSignature.AsSpan ());

	internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (
		string jniSourceType, ReadOnlySpan<char> jniFieldName, ReadOnlySpan<char> jniFieldSignature)
		=> !RuntimeFeature.JniRemapping || !isInUse ? null :
			BinaryJniRemappingLookup.GetField (jniSourceType, jniFieldName, jniFieldSignature);

	internal static int CompareUtf8ToUtf16 (ReadOnlySpan<byte> utf8, ReadOnlySpan<char> utf16)
	{
		while (!utf8.IsEmpty && !utf16.IsEmpty) {
			while (!utf8.IsEmpty && !utf16.IsEmpty && utf8 [0] < 0x80 && utf16 [0] < 0x80) {
				int result = utf8 [0].CompareTo ((byte)utf16 [0]);
				if (result != 0)
					return result;
				utf8 = utf8 [1..];
				utf16 = utf16 [1..];
			}
			if (utf8.IsEmpty || utf16.IsEmpty)
				break;

			OperationStatus utf8Status = Rune.DecodeFromUtf8 (utf8, out Rune utf8Rune, out int utf8Consumed);
			if (utf8Status != OperationStatus.Done) {
				utf8Rune = Rune.ReplacementChar;
				utf8Consumed = 1;
			}
			OperationStatus utf16Status = Rune.DecodeFromUtf16 (utf16, out Rune utf16Rune, out int utf16Consumed);
			if (utf16Status != OperationStatus.Done) {
				utf16Rune = Rune.ReplacementChar;
				utf16Consumed = 1;
			}
			int comparison = utf8Rune.Value.CompareTo (utf16Rune.Value);
			if (comparison != 0)
				return comparison;
			utf8 = utf8 [utf8Consumed..];
			utf16 = utf16 [utf16Consumed..];
		}
		return utf8.IsEmpty ? (utf16.IsEmpty ? 0 : -1) : 1;
	}
}
