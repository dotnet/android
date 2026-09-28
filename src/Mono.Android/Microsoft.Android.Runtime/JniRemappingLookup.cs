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

static class JniRemappingLookup
{
	const int AsciiComparisonChunkSize = 16;

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
		public NativeJniRemappingReplacementMethod replacement;
	}

	unsafe struct NativeJniRemappingIndexTypeEntry
	{
		public NativeJniRemappingString            name;
		public uint                                method_count;
		public NativeJniRemappingIndexMethodEntry* methods;
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
		public NativeJniRemappingReplacementField replacement;
	}

	unsafe struct NativeJniRemappingIndexFieldTypeEntry
	{
		public NativeJniRemappingString          name;
		public uint                              field_count;
		public NativeJniRemappingIndexFieldEntry* fields;
	}

	unsafe struct NativeJniRemappingData
	{
		public NativeJniRemappingTypeReplacementEntry* type_replacements;
		public NativeJniRemappingTypeReplacementEntry* reverse_type_replacements;
		public NativeJniRemappingIndexTypeEntry*        method_replacement_index;
		public NativeJniRemappingIndexFieldTypeEntry*   field_replacement_index;
		public uint                                     type_replacement_count;
		public uint                                     reverse_type_replacement_count;
		public uint                                     method_replacement_index_count;
		public uint                                     field_replacement_index_count;
	}

	static unsafe NativeJniRemappingData* nativeData;
	static bool isInUse;
	static readonly ConcurrentDictionary<string, string> reverseTypes = new (StringComparer.Ordinal);

	internal static unsafe void Initialize (IntPtr data)
	{
		reverseTypes.Clear ();
		if (data == IntPtr.Zero) {
			isInUse = false;
			return;
		}

		nativeData = (NativeJniRemappingData*)data;
		isInUse =
			nativeData->type_replacement_count > 0 ||
			nativeData->reverse_type_replacement_count > 0 ||
			nativeData->method_replacement_index_count > 0 ||
			nativeData->field_replacement_index_count > 0;
	}

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

	internal static unsafe string? GetReplacementType (string? jniSimpleReference)
	{
		IntPtr replacement = GetReplacementTypeUtf8 (jniSimpleReference);
		return replacement == IntPtr.Zero ? null : Marshal.PtrToStringUTF8 (replacement);
	}

	internal static unsafe IntPtr GetReplacementTypeUtf8 (string? jniSimpleReference)
	{
		if (jniSimpleReference is null || !isInUse || jniSimpleReference.Length == 0)
			return IntPtr.Zero;

		NativeJniRemappingData* data = nativeData;
		if (data == null)
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");

		return (IntPtr)LookupType (data->type_replacements, data->type_replacement_count, jniSimpleReference);
	}

	internal static unsafe string? GetReverseType (string? jniSimpleReference)
	{
		if (jniSimpleReference is null || !isInUse || jniSimpleReference.Length == 0)
			return null;

		string replacement = reverseTypes.GetOrAdd (jniSimpleReference, static source => LookupReverseType (source));
		return string.Equals (replacement, jniSimpleReference, StringComparison.Ordinal) ? null : replacement;
	}

	static unsafe string LookupReverseType (string jniSimpleReference)
	{
		NativeJniRemappingData* data = nativeData;
		if (data == null)
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");

		byte* replacement = LookupType (
			data->reverse_type_replacements,
			data->reverse_type_replacement_count,
			jniSimpleReference);
		return replacement == null
			? jniSimpleReference
			: Marshal.PtrToStringUTF8 ((IntPtr)replacement) ?? jniSimpleReference;
	}

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, string jniMethodName, string jniMethodSignature)
		=> GetReplacementMethodInfo (jniSourceType, jniMethodName.AsSpan (), jniMethodSignature.AsSpan ());

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
		if (!isInUse)
			return null;

		NativeJniRemappingData* data = nativeData;
		if (data == null)
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");

		byte* matchedSignature;
		NativeJniRemappingReplacementMethod* method = jniSourceTypeUtf8 == IntPtr.Zero
			? LookupMethod (data, jniSourceType, jniMethodName, jniMethodSignature, out matchedSignature)
			: LookupMethod (data, GetNullTerminatedUtf8Span (jniSourceTypeUtf8), jniMethodName, jniMethodSignature, out matchedSignature);

		if (method == null)
			return null;
		if (method->target_type == null || method->target_name == null) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			throw new InvalidOperationException (
				$"JNI remapping entry for `{sourceType}.{jniMethodName}{jniMethodSignature}` is missing target information.");
		}

		int? paramCount = null;
		bool isStatic = method->is_static != 0;
		string? targetSignature = null;
		if (isStatic) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			string sourceSignature = jniMethodSignature.ToString ();
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (sourceSignature) + 1;
			targetSignature = method->target_signature == null
				? $"(L{sourceType};" + sourceSignature.Substring ("(".Length)
				: Marshal.PtrToStringUTF8 ((IntPtr)method->target_signature);
		}

		var ret = new JniRuntime.ReplacementMethodInfo {
			TargetJniTypeUtf8               = (IntPtr)method->target_type,
			TargetJniMethodNameUtf8         = (IntPtr)method->target_name,
			TargetJniMethodSignature        = targetSignature,
			TargetJniMethodSignatureUtf8    = isStatic
				? IntPtr.Zero
				: (IntPtr)(method->target_signature == null ? matchedSignature : method->target_signature),
			TargetJniMethodParameterCount   = paramCount,
			TargetJniMethodInstanceToStatic = isStatic,
		};

		if (Logger.LogAssembly) {
			string sourceType = GetSourceTypeForDiagnostics (jniSourceType, jniSourceTypeUtf8);
			string targetType = Marshal.PtrToStringUTF8 ((IntPtr)method->target_type) ?? "";
			string targetName = Marshal.PtrToStringUTF8 ((IntPtr)method->target_name) ?? "";
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
		if (!isInUse)
			return null;

		NativeJniRemappingData* data = nativeData;
		if (data == null)
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");

		NativeJniRemappingReplacementField* field = LookupField (
			data,
			jniSourceType,
			jniFieldName,
			jniFieldSignature);
		if (field == null)
			return null;
		if (field->target_type == null || field->target_name == null) {
			throw new InvalidOperationException (
				$"JNI remapping entry for `{jniSourceType}.{jniFieldName}:{jniFieldSignature}` is missing target information.");
		}

		string targetType = Marshal.PtrToStringUTF8 ((IntPtr)field->target_type) ?? "";
		string targetName = Marshal.PtrToStringUTF8 ((IntPtr)field->target_name) ?? "";
		string targetSignature = field->target_signature == null
			? jniFieldSignature.ToString ()
			: Marshal.PtrToStringUTF8 ((IntPtr)field->target_signature) ?? "";

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
		return value.length == (uint)key.Length &&
			new ReadOnlySpan<byte> (value.str, key.Length).SequenceEqual (key);
	}

	static unsafe bool Equal (NativeJniRemappingString value, ReadOnlySpan<char> key, bool keyIsAscii)
	{
		ReadOnlySpan<byte> utf8 = new ReadOnlySpan<byte> (value.str, checked ((int)value.length));
		return keyIsAscii
			? Ascii.Equals (utf8, key)
			: CompareUtf8ToUtf16 (utf8, key) == 0;
	}

	static unsafe int Compare (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		return new ReadOnlySpan<byte> (value.str, checked ((int)value.length)).SequenceCompareTo (key);
	}

	static unsafe int Compare (NativeJniRemappingString value, ReadOnlySpan<char> key, bool keyIsAscii)
	{
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

	static unsafe int LowerBoundByName (void* entries, uint count, int entrySize, ReadOnlySpan<byte> key)
	{
		int left = 0;
		int right = checked ((int)count);
		while (left < right) {
			int middle = left + ((right - left) / 2);
			var name = *(NativeJniRemappingString*)((byte*)entries + (middle * entrySize));
			if (Compare (name, key) < 0) {
				left = middle + 1;
			} else {
				right = middle;
			}
		}
		return left;
	}

	static unsafe int LowerBoundByName (void* entries, uint count, int entrySize, ReadOnlySpan<char> key, bool keyIsAscii)
	{
		int left = 0;
		int right = checked ((int)count);
		while (left < right) {
			int middle = left + ((right - left) / 2);
			var name = *(NativeJniRemappingString*)((byte*)entries + (middle * entrySize));
			if (Compare (name, key, keyIsAscii) < 0) {
				left = middle + 1;
			} else {
				right = middle;
			}
		}
		return left;
	}

	static unsafe byte* LookupType (NativeJniRemappingTypeReplacementEntry* entries, uint count, ReadOnlySpan<char> key)
	{
		bool keyIsAscii = Ascii.IsValid (key);
		int index = LowerBoundByName (entries, count, sizeof (NativeJniRemappingTypeReplacementEntry), key, keyIsAscii);
		if (index >= checked ((int)count) || !Equal (entries [index].name, key, keyIsAscii))
			return null;
		return entries [index].replacement;
	}

	static unsafe byte* LookupType (NativeJniRemappingTypeReplacementEntry* entries, uint count, ReadOnlySpan<byte> key)
	{
		int index = LowerBoundByName (entries, count, sizeof (NativeJniRemappingTypeReplacementEntry), key);
		if (index >= checked ((int)count) || !Equal (entries [index].name, key))
			return null;
		return entries [index].replacement;
	}

	static unsafe NativeJniRemappingReplacementMethod* LookupMethod (
		NativeJniRemappingData* data,
		ReadOnlySpan<byte> sourceType,
		ReadOnlySpan<char> name,
		ReadOnlySpan<char> signature,
		out byte* matchedSignature)
	{
		matchedSignature = null;
		int typeIndex = LowerBoundByName (
			data->method_replacement_index,
			data->method_replacement_index_count,
			sizeof (NativeJniRemappingIndexTypeEntry),
			sourceType
		);
		if (typeIndex >= checked ((int)data->method_replacement_index_count) ||
				!Equal (data->method_replacement_index [typeIndex].name, sourceType))
			return null;

		NativeJniRemappingIndexTypeEntry* type = &data->method_replacement_index [typeIndex];
		return LookupMethod (type, name, signature, out matchedSignature);
	}

	static unsafe NativeJniRemappingReplacementMethod* LookupMethod (
		NativeJniRemappingData* data,
		ReadOnlySpan<char> sourceType,
		ReadOnlySpan<char> name,
		ReadOnlySpan<char> signature,
		out byte* matchedSignature)
	{
		matchedSignature = null;
		bool sourceTypeIsAscii = Ascii.IsValid (sourceType);
		int typeIndex = LowerBoundByName (
			data->method_replacement_index,
			data->method_replacement_index_count,
			sizeof (NativeJniRemappingIndexTypeEntry),
			sourceType,
			sourceTypeIsAscii
		);
		if (typeIndex >= checked ((int)data->method_replacement_index_count) ||
				!Equal (data->method_replacement_index [typeIndex].name, sourceType, sourceTypeIsAscii))
			return null;

		NativeJniRemappingIndexTypeEntry* type = &data->method_replacement_index [typeIndex];
		return LookupMethod (type, name, signature, out matchedSignature);
	}

	static unsafe NativeJniRemappingReplacementMethod* LookupMethod (
		NativeJniRemappingIndexTypeEntry* type,
		ReadOnlySpan<char> name,
		ReadOnlySpan<char> signature,
		out byte* matchedSignature)
	{
		matchedSignature = null;
		bool nameIsAscii = Ascii.IsValid (name);
		int first = LowerBoundByName (type->methods, type->method_count, sizeof (NativeJniRemappingIndexMethodEntry), name, nameIsAscii);
		int count = checked ((int)type->method_count);
		if (first >= count || !Equal (type->methods [first].name, name, nameIsAscii))
			return null;

		int last = first + 1;
		while (last < count && Equal (type->methods [last].name, name, nameIsAscii))
			last++;

		if (signature.Length > 0) {
			bool signatureIsAscii = Ascii.IsValid (signature);
			for (int i = first; i < last; i++) {
				NativeJniRemappingIndexMethodEntry* entry = &type->methods [i];
				if (entry->signature.length != 0 && Equal (entry->signature, signature, signatureIsAscii)) {
					matchedSignature = entry->signature.str;
					return &entry->replacement;
				}
			}

			int closeParenthesis = signature.Length - 1;
			while (closeParenthesis >= 0 && signature [closeParenthesis] != ')')
				closeParenthesis--;
			int prefixLength = closeParenthesis + 1;
			if (prefixLength > 0 && prefixLength != signature.Length) {
				ReadOnlySpan<char> signaturePrefix = signature.Slice (0, prefixLength);
				bool signaturePrefixIsAscii = signatureIsAscii || Ascii.IsValid (signaturePrefix);
				for (int i = first; i < last; i++) {
					NativeJniRemappingIndexMethodEntry* entry = &type->methods [i];
					if (entry->signature.length != 0 && Equal (entry->signature, signaturePrefix, signaturePrefixIsAscii))
						return &entry->replacement;
				}
			}
		}

		for (int i = first; i < last; i++) {
			NativeJniRemappingIndexMethodEntry* entry = &type->methods [i];
			if (entry->signature.length == 0) {
				return &entry->replacement;
			}
		}
		return null;
	}

	static unsafe NativeJniRemappingReplacementField* LookupField (
		NativeJniRemappingData* data,
		ReadOnlySpan<char> sourceType,
		ReadOnlySpan<char> name,
		ReadOnlySpan<char> signature)
	{
		bool sourceTypeIsAscii = Ascii.IsValid (sourceType);
		int typeIndex = LowerBoundByName (
			data->field_replacement_index,
			data->field_replacement_index_count,
			sizeof (NativeJniRemappingIndexFieldTypeEntry),
			sourceType,
			sourceTypeIsAscii);
		if (typeIndex >= checked ((int)data->field_replacement_index_count) ||
				!Equal (data->field_replacement_index [typeIndex].name, sourceType, sourceTypeIsAscii))
			return null;

		NativeJniRemappingIndexFieldTypeEntry* type = &data->field_replacement_index [typeIndex];
		bool nameIsAscii = Ascii.IsValid (name);
		int first = LowerBoundByName (
			type->fields,
			type->field_count,
			sizeof (NativeJniRemappingIndexFieldEntry),
			name,
			nameIsAscii);
		int count = checked ((int)type->field_count);
		if (first >= count || !Equal (type->fields [first].name, name, nameIsAscii))
			return null;

		int last = first + 1;
		while (last < count && Equal (type->fields [last].name, name, nameIsAscii))
			last++;

		bool signatureIsAscii = Ascii.IsValid (signature);
		for (int i = first; i < last; i++) {
			NativeJniRemappingIndexFieldEntry* entry = &type->fields [i];
			if (entry->signature.length != 0 && Equal (entry->signature, signature, signatureIsAscii))
				return &entry->replacement;
		}
		for (int i = first; i < last; i++) {
			NativeJniRemappingIndexFieldEntry* entry = &type->fields [i];
			if (entry->signature.length == 0)
				return &entry->replacement;
		}
		return null;
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
