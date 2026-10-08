#nullable enable

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
	const int AsciiComparisonChunkSize = 16;
	const uint HeaderSize = 56;
	const uint TypeStride = 12;
	const uint IndexStride = 16;
	const uint MethodStride = 32;
	const uint FieldStride = 28;

	readonly ref struct NativeJniRemappingHeader
	{
		readonly ReadOnlySpan<byte> data;

		public NativeJniRemappingHeader (ReadOnlySpan<byte> data)
		{
			this.data = data.Slice (0, (int)HeaderSize);
		}

		public uint type_replacement_count => Read (0);
		public uint reverse_type_replacement_count => Read (4);
		public uint method_replacement_index_count => Read (8);
		public uint field_replacement_index_count => Read (12);
		public uint type_replacements => Read (16);
		public uint reverse_type_replacements => Read (20);
		public uint method_replacement_index => Read (24);
		public uint field_replacement_index => Read (28);
		public uint methods => Read (32);
		public uint fields => Read (36);
		public uint strings => Read (40);
		public uint strings_length => Read (44);
		public uint method_count => Read (48);
		public uint field_count => Read (52);

		// Android's supported ABIs are little-endian.
		uint Read (int offset) => BitConverter.ToUInt32 (data.Slice (offset, sizeof (uint)));

		public uint GetCount (int section) => section switch {
			0 => type_replacement_count,
			1 => reverse_type_replacement_count,
			2 => method_replacement_index_count,
			3 => field_replacement_index_count,
			4 => method_count,
			5 => field_count,
			_ => throw new ArgumentOutOfRangeException (nameof (section)),
		};

		public uint GetOffset (int section) => section switch {
			0 => type_replacements,
			1 => reverse_type_replacements,
			2 => method_replacement_index,
			3 => field_replacement_index,
			4 => methods,
			5 => fields,
			_ => throw new ArgumentOutOfRangeException (nameof (section)),
		};
	}

	unsafe struct NativeJniRemappingString
	{
		public uint  length;
		public byte* str;
	}

	unsafe struct NativeJniRemappingReplacementMethod
	{
		public byte* target_type;
		public byte* target_name;
		public byte* target_signature;
		public byte  is_static;
	}

	unsafe struct NativeJniRemappingReplacementField
	{
		public byte* target_type;
		public byte* target_name;
		public byte* target_signature;
	}

	unsafe struct NativeJniRemappingIndexMethodEntry
	{
		public NativeJniRemappingString            name;
		public NativeJniRemappingString            signature;
		public uint                               replacement;
	}

	unsafe struct NativeJniRemappingIndexTypeEntry
	{
		public NativeJniRemappingString            name;
		public uint                                method_count;
		public uint                                methods;
	}

	unsafe struct NativeJniRemappingTypeReplacementEntry
	{
		public NativeJniRemappingString name;
		public byte*                    replacement;
	}

	unsafe struct NativeJniRemappingIndexFieldEntry
	{
		public NativeJniRemappingString           name;
		public NativeJniRemappingString           signature;
		public uint                              replacement;
	}

	unsafe struct NativeJniRemappingIndexFieldTypeEntry
	{
		public NativeJniRemappingString          name;
		public uint                              field_count;
		public uint                              fields;
	}

	readonly struct NativeBinaryBlobPayload
	{
		public readonly IntPtr data;
		public readonly uint size;
	}

	static unsafe byte* table;
	static uint size;
	static bool isInUse;
	static readonly ConcurrentDictionary<string, string> reverseTypes = new (StringComparer.Ordinal);

	static NativeJniRemappingHeader Header => new (Data);

	static ReadOnlySpan<byte> Data {
		get {
			unsafe {
				// SAFETY: Initialize validates size; the native host retains the mapped or decoded body for the process lifetime.
				return new ReadOnlySpan<byte> (table, checked ((int)size));
			}
		}
	}

	/// <safety>
	/// data must address a live native BinaryBlobPayload descriptor whose body remains readable
	/// for its declared size throughout the process lifetime.
	/// </safety>
	internal static unsafe void Initialize (IntPtr data)
	{
		reverseTypes.Clear ();
		if (!RuntimeFeature.JniRemapping) {
			table = null;
			isInUse = false;
			return;
		}

		if (data == IntPtr.Zero)
			throw new InvalidDataException ("Invalid native JNI remapping table.");

		NativeBinaryBlobPayload payload;
		unsafe {
			// SAFETY: both native hosts pass a process-lifetime descriptor, not a body or temporary stack address.
			payload = *(NativeBinaryBlobPayload*)data;
		}
		if (payload.data == IntPtr.Zero || payload.size < HeaderSize || payload.size > 256 * 1024 * 1024)
			throw new InvalidDataException ("Invalid native JNI remapping table.");

		table = (byte*)payload.data;
		size = payload.size;
		var header = Header;
		uint strings = header.strings;
		uint stringsLength = header.strings_length;
		if (strings < HeaderSize || !Contains (strings, stringsLength) ||
				stringsLength == 0 || Data [(int)strings] != 0)
			throw new InvalidDataException ("Invalid JNI remapping string section.");
		for (int i = 0; i < 6; i++) {
			uint stride = i < 2 ? TypeStride : i < 4 ? IndexStride : i == 4 ? MethodStride : FieldStride;
			uint offset = header.GetOffset (i);
			uint count = header.GetCount (i);
			if (!Contains (offset, (ulong)count * stride) || offset < HeaderSize ||
					(ulong)offset + (ulong)count * stride > strings)
				throw new InvalidDataException ("JNI remapping table index is outside the declared data.");
		}
		isInUse = header.type_replacement_count != 0 || header.reverse_type_replacement_count != 0 ||
			header.method_replacement_index_count != 0 || header.field_replacement_index_count != 0;
	}

	static bool Contains (uint offset, ulong length) => offset <= size && length <= size - offset;

	static uint Read (uint offset)
	{
		if (!Contains (offset, 4))
			throw new InvalidDataException ("JNI remapping table access exceeds its bounds.");
		return BitConverter.ToUInt32 (Data.Slice ((int)offset, sizeof (uint)));
	}

	static unsafe NativeJniRemappingString ReadString (uint offset) => new () {
		str = table + Read (offset),
		length = Read (offset + 4),
	};

	static unsafe NativeJniRemappingTypeReplacementEntry ReadType (uint offset) => new () {
		name = ReadString (offset),
		replacement = CString (Read (offset + 8)),
	};

	static NativeJniRemappingIndexTypeEntry ReadMethodType (uint offset)
		=> new () { name = ReadString (offset), method_count = Read (offset + 12), methods = Read (offset + 8) };

	static NativeJniRemappingIndexFieldTypeEntry ReadFieldType (uint offset)
		=> new () { name = ReadString (offset), field_count = Read (offset + 12), fields = Read (offset + 8) };

	static NativeJniRemappingIndexMethodEntry ReadMethod (uint offset)
		=> new () { name = ReadString (offset), signature = ReadString (offset + 8), replacement = offset + 16 };

	static NativeJniRemappingIndexFieldEntry ReadField (uint offset)
		=> new () { name = ReadString (offset), signature = ReadString (offset + 8), replacement = offset + 16 };

	static unsafe NativeJniRemappingReplacementMethod ReadReplacementMethod (uint offset)
	{
		uint isStatic = Read (offset + 12);
		if (isStatic > 1)
			throw new InvalidDataException ("JNI remapping method has an invalid instance-to-static flag.");
		return new () {
			target_type = CString (Read (offset)),
			target_name = CString (Read (offset + 4)),
			target_signature = CString (Read (offset + 8)),
			is_static = (byte)isStatic,
		};
	}

	static unsafe NativeJniRemappingReplacementField ReadReplacementField (uint offset) => new () {
		target_type = CString (Read (offset)),
		target_name = CString (Read (offset + 4)),
		target_signature = CString (Read (offset + 8)),
	};

	static uint Entry (int section, uint position, uint stride)
	{
		if (position >= Header.GetCount (section))
			throw new InvalidDataException ("JNI remapping entry exceeds its bounds.");
		uint offset = checked (Header.GetOffset (section) + position * stride);
		if (!Contains (offset, stride))
			throw new InvalidDataException ("JNI remapping entry exceeds its bounds.");
		return offset;
	}

	static unsafe void ValidateString (NativeJniRemappingString value)
	{
		uint offset = checked ((uint)(value.str - table));
		uint start = Header.strings;
		uint end = checked (start + Header.strings_length);
		if (offset < start || value.length > int.MaxValue || offset >= end ||
				value.length >= end - offset || Data [checked ((int)(offset + value.length))] != 0)
			throw new InvalidDataException ("JNI remapping string exceeds its bounds or is not NUL terminated.");
	}

	static unsafe byte* CString (uint offset)
	{
		if (offset == 0)
			return null;
		uint start = Header.strings;
		uint end = checked (start + Header.strings_length);
		if (offset < start || offset >= end)
			throw new InvalidDataException ("JNI remapping string offset exceeds its bounds.");
		if (Data.Slice ((int)offset, checked ((int)(end - offset))).IndexOf ((byte)0) < 0)
			throw new InvalidDataException ("JNI remapping string is not NUL terminated.");
		return table + offset;
	}

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

	internal static unsafe string? GetReplacementType (string? jniSimpleReference)
	{
		IntPtr replacement = GetReplacementTypeUtf8 (jniSimpleReference);
		return replacement == IntPtr.Zero ? null : Marshal.PtrToStringUTF8 (replacement);
	}

	internal static unsafe IntPtr GetReplacementTypeUtf8 (string? jniSimpleReference)
	{
		if (!RuntimeFeature.JniRemapping || jniSimpleReference is null || !isInUse || jniSimpleReference.Length == 0)
			return IntPtr.Zero;

		return (IntPtr)LookupType (0, jniSimpleReference);
	}

	internal static unsafe string? GetReverseType (string? jniSimpleReference)
	{
		if (!RuntimeFeature.JniRemapping || jniSimpleReference is null || !isInUse || jniSimpleReference.Length == 0)
			return null;

		string replacement = reverseTypes.GetOrAdd (jniSimpleReference, static source => LookupReverseType (source));
		return string.Equals (replacement, jniSimpleReference, StringComparison.Ordinal) ? null : replacement;
	}

	static unsafe string LookupReverseType (string jniSimpleReference)
	{
		byte* replacement = LookupType (1, jniSimpleReference);
		return replacement == null
			? jniSimpleReference
			: Marshal.PtrToStringUTF8 ((IntPtr)replacement) ?? jniSimpleReference;
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

	internal static unsafe JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		=> GetReplacementMethodInfo (jniSourceType.AsSpan (), IntPtr.Zero, jniMethodName, jniMethodSignature);

	internal static unsafe JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (IntPtr jniSourceTypeUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
	{
		if (jniSourceTypeUtf8 == IntPtr.Zero)
			throw new ArgumentNullException (nameof (jniSourceTypeUtf8));
		return GetReplacementMethodInfo (default, jniSourceTypeUtf8, jniMethodName, jniMethodSignature);
	}

	static unsafe JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
		ReadOnlySpan<char> jniSourceType,
		IntPtr jniSourceTypeUtf8,
		ReadOnlySpan<char> jniMethodName,
		ReadOnlySpan<char> jniMethodSignature)
	{
		if (!RuntimeFeature.JniRemapping || !isInUse)
			return null;

		byte* matchedSignature;
		NativeJniRemappingReplacementMethod? result = jniSourceTypeUtf8 == IntPtr.Zero
			? LookupMethod (jniSourceType, jniMethodName, jniMethodSignature, out matchedSignature)
			: LookupMethod (GetNullTerminatedUtf8Span (jniSourceTypeUtf8), jniMethodName, jniMethodSignature, out matchedSignature);

		if (result is not { } method)
			return null;
		if (method.target_type == null || method.target_name == null) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			throw new InvalidDataException (
				$"JNI remapping entry for `{sourceType}.{jniMethodName}{jniMethodSignature}` has invalid target information.");
		}

		int? paramCount = null;
		bool isStatic = method.is_static != 0;
		string? targetSignature = null;
		if (isStatic) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			string sourceSignature = jniMethodSignature.ToString ();
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (sourceSignature) + 1;
			targetSignature = method.target_signature == null
				? $"(L{sourceType};" + sourceSignature.Substring ("(".Length)
				: Marshal.PtrToStringUTF8 ((IntPtr)method.target_signature);
		}

		var ret = new JniRuntime.ReplacementMethodInfo {
			TargetJniTypeUtf8               = (IntPtr)method.target_type,
			TargetJniMethodNameUtf8         = (IntPtr)method.target_name,
			TargetJniMethodSignature        = targetSignature,
			TargetJniMethodSignatureUtf8    = isStatic
				? IntPtr.Zero
				: (IntPtr)(method.target_signature == null ? matchedSignature : method.target_signature),
			TargetJniMethodParameterCount   = paramCount,
			TargetJniMethodInstanceToStatic = isStatic,
		};

		if (Logger.LogAssembly) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			string targetType = Marshal.PtrToStringUTF8 ((IntPtr)method.target_type) ?? "";
			string targetName = Marshal.PtrToStringUTF8 ((IntPtr)method.target_name) ?? "";
			string effectiveTargetSignature = targetSignature ??
				(matchedSignature == null ? jniMethodSignature.ToString () : Marshal.PtrToStringUTF8 ((IntPtr)matchedSignature) ?? "");
			var message = $"Remapping method `{sourceType}.{jniMethodName}{jniMethodSignature}` to " +
				$"`{targetType}.{targetName}{effectiveTargetSignature}`; " +
				$"param-count: {paramCount}; instance-to-static? {isStatic}";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return ret;
	}

	internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (string jniSourceType, string jniFieldName, string jniFieldSignature)
		=> GetReplacementFieldInfo (jniSourceType, jniFieldName.AsSpan (), jniFieldSignature.AsSpan ());

	internal static unsafe JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (
		string jniSourceType,
		ReadOnlySpan<char> jniFieldName,
		ReadOnlySpan<char> jniFieldSignature)
	{
		if (!RuntimeFeature.JniRemapping || !isInUse)
			return null;

		NativeJniRemappingReplacementField? result = LookupField (
			jniSourceType,
			jniFieldName,
			jniFieldSignature);
		if (result is not { } field)
			return null;
		if (field.target_type == null || field.target_name == null) {
			throw new InvalidDataException (
				$"JNI remapping entry for `{jniSourceType}.{jniFieldName}:{jniFieldSignature}` has invalid target information.");
		}

		string targetType = Marshal.PtrToStringUTF8 ((IntPtr)field.target_type) ?? "";
		string targetName = Marshal.PtrToStringUTF8 ((IntPtr)field.target_name) ?? "";
		string targetSignature = field.target_signature == null
			? jniFieldSignature.ToString ()
			: Marshal.PtrToStringUTF8 ((IntPtr)field.target_signature) ?? "";

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

	static unsafe bool Equal (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		if (value.length == (uint)key.Length)
			ValidateString (value);
		return value.length == (uint)key.Length &&
			new ReadOnlySpan<byte> (value.str, key.Length).SequenceEqual (key);
	}

	static unsafe bool Equal (NativeJniRemappingString value, ReadOnlySpan<char> key, bool keyIsAscii)
	{
		ValidateString (value);
		ReadOnlySpan<byte> utf8 = new ReadOnlySpan<byte> (value.str, checked ((int)value.length));
		return keyIsAscii
			? Ascii.Equals (utf8, key)
			: CompareUtf8ToUtf16 (utf8, key) == 0;
	}

	static unsafe int Compare (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		ValidateString (value);
		return new ReadOnlySpan<byte> (value.str, checked ((int)value.length)).SequenceCompareTo (key);
	}

	static unsafe int Compare (NativeJniRemappingString value, ReadOnlySpan<char> key, bool keyIsAscii)
	{
		ValidateString (value);
		ReadOnlySpan<byte> utf8 = new ReadOnlySpan<byte> (value.str, checked ((int)value.length));
		return keyIsAscii ? CompareUtf8ToAscii (utf8, key) : CompareUtf8ToUtf16 (utf8, key);
	}

	static int CompareUtf8ToAscii (ReadOnlySpan<byte> utf8, ReadOnlySpan<char> ascii)
	{
		int commonLength = Math.Min (utf8.Length, ascii.Length);
		int offset = 0;
		while (commonLength - offset >= AsciiComparisonChunkSize) {
			ReadOnlySpan<byte> utf8Chunk = utf8.Slice (offset, AsciiComparisonChunkSize);
			ReadOnlySpan<char> asciiChunk = ascii.Slice (offset, AsciiComparisonChunkSize);
			if (!Ascii.Equals (utf8Chunk, asciiChunk)) {
				for (int i = 0; i < AsciiComparisonChunkSize; i++) {
					int result = utf8Chunk [i].CompareTo ((byte)asciiChunk [i]);
					if (result != 0)
						return result;
				}
			}
			offset += AsciiComparisonChunkSize;
		}

		ReadOnlySpan<byte> utf8Tail = utf8.Slice (offset, commonLength - offset);
		ReadOnlySpan<char> asciiTail = ascii.Slice (offset, commonLength - offset);
		if (!Ascii.Equals (utf8Tail, asciiTail)) {
			for (int i = 0; i < utf8Tail.Length; i++) {
				int result = utf8Tail [i].CompareTo ((byte)asciiTail [i]);
				if (result != 0)
					return result;
			}
		}
		return utf8.Length.CompareTo (ascii.Length);
	}

	static int CompareUtf8ToUtf16 (ReadOnlySpan<byte> utf8, ReadOnlySpan<char> utf16)
	{
		// Generated table strings and runtime JNI names are well-formed Unicode. Replacement behavior
		// below only keeps the comparator deterministic if malformed input reaches this internal API.
		while (!utf8.IsEmpty && !utf16.IsEmpty) {
			while (!utf8.IsEmpty && !utf16.IsEmpty && utf8 [0] < 0x80 && utf16 [0] < 0x80) {
				int result = utf8 [0].CompareTo ((byte)utf16 [0]);
				if (result != 0)
					return result;
				utf8 = utf8.Slice (1);
				utf16 = utf16.Slice (1);
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

			int runeComparison = utf8Rune.Value.CompareTo (utf16Rune.Value);
			if (runeComparison != 0)
				return runeComparison;

			utf8 = utf8.Slice (utf8Consumed);
			utf16 = utf16.Slice (utf16Consumed);
		}

		if (utf8.IsEmpty)
			return utf16.IsEmpty ? 0 : -1;
		return 1;
	}

	static uint LowerBoundByName (int section, uint start, uint count, uint stride, ReadOnlySpan<byte> key)
	{
		uint left = start;
		uint right = start + count;
		while (left < right) {
			uint middle = left + (right - left) / 2;
			var entryName = ReadString (Entry (section, middle, stride));
			if (Compare (entryName, key) < 0) {
				left = middle + 1;
			} else {
				right = middle;
			}
		}
		return left;
	}

	static uint LowerBoundByName (int section, uint start, uint count, uint stride, ReadOnlySpan<char> key, bool keyIsAscii)
	{
		uint left = start;
		uint right = start + count;
		while (left < right) {
			uint middle = left + (right - left) / 2;
			var entryName = ReadString (Entry (section, middle, stride));
			if (Compare (entryName, key, keyIsAscii) < 0) {
				left = middle + 1;
			} else {
				right = middle;
			}
		}
		return left;
	}

	static unsafe byte* LookupType (int section, ReadOnlySpan<char> key)
	{
		bool keyIsAscii = Ascii.IsValid (key);
		uint index = LowerBoundByName (section, 0, Header.GetCount (section), TypeStride, key, keyIsAscii);
		if (index >= Header.GetCount (section))
			return null;
		uint offset = Entry (section, index, TypeStride);
		if (!Equal (ReadString (offset), key, keyIsAscii))
			return null;
		return ReadType (offset).replacement;
	}

	static unsafe NativeJniRemappingReplacementMethod? LookupMethod (
		ReadOnlySpan<byte> sourceType, ReadOnlySpan<char> name, ReadOnlySpan<char> signature, out byte* matchedSignature)
	{
		matchedSignature = null;
		uint typeIndex = LowerBoundByName (2, 0, Header.method_replacement_index_count, IndexStride, sourceType);
		if (typeIndex >= Header.method_replacement_index_count)
			return null;
		var type = ReadMethodType (Entry (2, typeIndex, IndexStride));
		return Equal (type.name, sourceType) ? LookupMethod (type, name, signature, out matchedSignature) : null;
	}

	static unsafe NativeJniRemappingReplacementMethod? LookupMethod (
		ReadOnlySpan<char> sourceType, ReadOnlySpan<char> name, ReadOnlySpan<char> signature, out byte* matchedSignature)
	{
		matchedSignature = null;
		bool sourceTypeIsAscii = Ascii.IsValid (sourceType);
		uint typeIndex = LowerBoundByName (2, 0, Header.method_replacement_index_count, IndexStride, sourceType, sourceTypeIsAscii);
		if (typeIndex >= Header.method_replacement_index_count)
			return null;
		var type = ReadMethodType (Entry (2, typeIndex, IndexStride));
		return Equal (type.name, sourceType, sourceTypeIsAscii)
			? LookupMethod (type, name, signature, out matchedSignature) : null;
	}

	static unsafe NativeJniRemappingReplacementMethod? LookupMethod (
		NativeJniRemappingIndexTypeEntry type, ReadOnlySpan<char> name, ReadOnlySpan<char> signature, out byte* matchedSignature)
	{
		matchedSignature = null;
		if (type.methods > Header.method_count || type.method_count > Header.method_count - type.methods)
			throw new InvalidDataException ("JNI remapping method group exceeds its table.");
		uint end = type.methods + type.method_count;
		bool nameIsAscii = Ascii.IsValid (name);
		uint first = LowerBoundByName (4, type.methods, type.method_count, MethodStride, name, nameIsAscii);
		if (first >= end || !Equal (ReadMethod (Entry (4, first, MethodStride)).name, name, nameIsAscii))
			return null;

		uint last = first + 1;
		while (last < end && Equal (ReadMethod (Entry (4, last, MethodStride)).name, name, nameIsAscii))
			last++;

		if (signature.Length > 0) {
			bool signatureIsAscii = Ascii.IsValid (signature);
			for (uint i = first; i < last; i++) {
				var entry = ReadMethod (Entry (4, i, MethodStride));
				if (entry.signature.length != 0 && Equal (entry.signature, signature, signatureIsAscii)) {
					matchedSignature = entry.signature.str;
					return ReadReplacementMethod (entry.replacement);
				}
			}

			int closeParenthesis = signature.Length - 1;
			while (closeParenthesis >= 0 && signature [closeParenthesis] != ')')
				closeParenthesis--;
			int prefixLength = closeParenthesis + 1;
			if (prefixLength > 0 && prefixLength != signature.Length) {
				ReadOnlySpan<char> signaturePrefix = signature.Slice (0, prefixLength);
				bool signaturePrefixIsAscii = signatureIsAscii || Ascii.IsValid (signaturePrefix);
				for (uint i = first; i < last; i++) {
					var entry = ReadMethod (Entry (4, i, MethodStride));
					if (entry.signature.length != 0 && Equal (entry.signature, signaturePrefix, signaturePrefixIsAscii))
						return ReadReplacementMethod (entry.replacement);
				}
			}
		}

		for (uint i = first; i < last; i++) {
			var entry = ReadMethod (Entry (4, i, MethodStride));
			if (entry.signature.length == 0)
				return ReadReplacementMethod (entry.replacement);
		}
		return null;
	}

	static NativeJniRemappingReplacementField? LookupField (
		ReadOnlySpan<char> sourceType, ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
	{
		bool sourceTypeIsAscii = Ascii.IsValid (sourceType);
		uint typeIndex = LowerBoundByName (3, 0, Header.field_replacement_index_count, IndexStride, sourceType, sourceTypeIsAscii);
		if (typeIndex >= Header.field_replacement_index_count)
			return null;
		var type = ReadFieldType (Entry (3, typeIndex, IndexStride));
		if (!Equal (type.name, sourceType, sourceTypeIsAscii))
			return null;
		if (type.fields > Header.field_count || type.field_count > Header.field_count - type.fields)
			throw new InvalidDataException ("JNI remapping field group exceeds its table.");

		uint end = type.fields + type.field_count;
		bool nameIsAscii = Ascii.IsValid (name);
		uint first = LowerBoundByName (5, type.fields, type.field_count, FieldStride, name, nameIsAscii);
		if (first >= end || !Equal (ReadField (Entry (5, first, FieldStride)).name, name, nameIsAscii))
			return null;

		uint last = first + 1;
		while (last < end && Equal (ReadField (Entry (5, last, FieldStride)).name, name, nameIsAscii))
			last++;

		bool signatureIsAscii = Ascii.IsValid (signature);
		for (uint i = first; i < last; i++) {
			var entry = ReadField (Entry (5, i, FieldStride));
			if (entry.signature.length != 0 && Equal (entry.signature, signature, signatureIsAscii))
				return ReadReplacementField (entry.replacement);
		}
		for (uint i = first; i < last; i++) {
			var entry = ReadField (Entry (5, i, FieldStride));
			if (entry.signature.length == 0)
				return ReadReplacementField (entry.replacement);
		}
		return null;
	}

	/// <safety>
	/// value must address a live NUL-terminated UTF-8 string for the duration of the lookup.
	/// </safety>
	static unsafe ReadOnlySpan<byte> GetNullTerminatedUtf8Span (IntPtr value)
	{
		unsafe {
			// SAFETY: the JNI type-name caller supplies a live NUL-terminated native string.
			return MemoryMarshal.CreateReadOnlySpanFromNullTerminated ((byte*)value);
		}
	}
}
