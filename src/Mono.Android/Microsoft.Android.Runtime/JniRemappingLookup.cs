#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class JniRemappingLookup
{
	const int StackallocThreshold = 512;

	// Keep these unmanaged layouts in sync with the JNI remapping structures in
	// src/native/clr/include/xamarin-app.hh.
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

	unsafe struct NativeJniRemappingReplacementField
	{
		public byte* target_type;
		public byte* target_name;
		public byte* target_signature;
	}

	unsafe struct NativeJniRemappingIndexFieldEntry
	{
		public NativeJniRemappingString           name;
		public NativeJniRemappingString           signature;
		public NativeJniRemappingReplacementField replacement;
	}

	unsafe struct NativeJniRemappingIndexFieldTypeEntry
	{
		public NativeJniRemappingString           name;
		public uint                               field_count;
		public NativeJniRemappingIndexFieldEntry* fields;
	}

	unsafe struct NativeJniRemappingTypeReplacementEntry
	{
		public NativeJniRemappingString name;
		public byte*                    replacement;
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

	internal static unsafe void Initialize (IntPtr data)
	{
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
		// Desugared companion names are derived before R8 renames the interface and companions.
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
		if (jniSimpleReference is null || !isInUse) {
			return null;
		}

		return LookupReplacementType (jniSimpleReference, reverse: false);
	}

	/// <summary>
	/// Maps a JNI type name as it exists in the packaged application back onto the name the managed
	/// code declares. Used by Java-to-managed lookups.
	/// </summary>
	internal static string? GetReverseType (string? jniSimpleReference)
	{
		if (jniSimpleReference is null || !isInUse) {
			return null;
		}

		return LookupReplacementType (jniSimpleReference, reverse: true);
	}

	static unsafe string? LookupReplacementType (string jniSimpleReference, bool reverse)
	{
		if (jniSimpleReference.Length == 0) {
			return null;
		}

		NativeJniRemappingData* data = nativeData;
		if (data == null) {
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");
		}

		int byteCount = Encoding.UTF8.GetByteCount (jniSimpleReference);
		byte[]? rented = null;
		try {
			if (byteCount > StackallocThreshold) {
				rented = ArrayPool<byte>.Shared.Rent (byteCount);
			}

			Span<byte> key = rented == null
				? stackalloc byte [byteCount]
				: rented.AsSpan (0, byteCount);
			Encoding.UTF8.GetBytes (jniSimpleReference, key);

			byte* replacement = reverse
				? LookupType (data->reverse_type_replacements, data->reverse_type_replacement_count, key)
				: LookupType (data->type_replacements, data->type_replacement_count, key);
			return replacement == null ? null : Marshal.PtrToStringUTF8 ((IntPtr)replacement);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return (rented);
			}
		}
	}

	internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, string jniMethodName, string jniMethodSignature)
		=> GetReplacementMethodInfo (jniSourceType, jniMethodName.AsSpan (), jniMethodSignature.AsSpan ());

	internal static unsafe JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (string jniSourceType, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
	{
		if (!isInUse) {
			return null;
		}

		return GetReplacementMethodInfoManaged (jniSourceType, jniMethodName, jniMethodSignature);
	}

	static unsafe JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoManaged (string jniSourceType, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
	{
		NativeJniRemappingData* data = nativeData;
		if (data == null) {
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");
		}

		int sourceTypeLength = Encoding.UTF8.GetByteCount (jniSourceType);
		int nameLength = Encoding.UTF8.GetByteCount (jniMethodName);
		int signatureLength = Encoding.UTF8.GetByteCount (jniMethodSignature);
		byte[]? rentedSourceType = null;
		byte[]? rentedName = null;
		byte[]? rentedSignature = null;
		NativeJniRemappingReplacementMethod* method;
		try {
			if (sourceTypeLength > StackallocThreshold)
				rentedSourceType = ArrayPool<byte>.Shared.Rent (sourceTypeLength);
			if (nameLength > StackallocThreshold)
				rentedName = ArrayPool<byte>.Shared.Rent (nameLength);
			if (signatureLength > StackallocThreshold)
				rentedSignature = ArrayPool<byte>.Shared.Rent (signatureLength);

			Span<byte> sourceTypeBuffer = rentedSourceType == null
				? stackalloc byte [sourceTypeLength]
				: rentedSourceType.AsSpan (0, sourceTypeLength);
			Span<byte> nameBuffer = rentedName == null
				? stackalloc byte [nameLength]
				: rentedName.AsSpan (0, nameLength);
			Span<byte> signatureBuffer = rentedSignature == null
				? stackalloc byte [signatureLength]
				: rentedSignature.AsSpan (0, signatureLength);
			Encoding.UTF8.GetBytes (jniSourceType, sourceTypeBuffer);
			Encoding.UTF8.GetBytes (jniMethodName, nameBuffer);
			Encoding.UTF8.GetBytes (jniMethodSignature, signatureBuffer);

			method = LookupMethod (data, sourceTypeBuffer, nameBuffer, signatureBuffer);
		} finally {
			if (rentedSourceType != null)
				ArrayPool<byte>.Shared.Return (rentedSourceType);
			if (rentedName != null)
				ArrayPool<byte>.Shared.Return (rentedName);
			if (rentedSignature != null)
				ArrayPool<byte>.Shared.Return (rentedSignature);
		}

		if (method == null) {
			return null;
		}

		if (method->target_type == null) {
			throw new InvalidOperationException (
				$"JNI remapping entry for `{jniSourceType}.{jniMethodName}{jniMethodSignature}` is missing a target type.");
		}
		if (method->target_name == null) {
			throw new InvalidOperationException (
				$"JNI remapping entry for `{jniSourceType}.{jniMethodName}{jniMethodSignature}` is missing a target method name.");
		}
		var sourceSignature = jniMethodSignature.ToString ();
		string? fallbackTargetSignature = method->target_signature == null ? sourceSignature : null;

		int? paramCount = null;
		bool isStatic = method->is_static != 0;
		if (isStatic) {
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (sourceSignature) + 1;
			if (method->target_signature == null) {
				fallbackTargetSignature = $"(L{jniSourceType};" + sourceSignature.Substring ("(".Length);
			}
		}

		var ret = new JniRuntime.ReplacementMethodInfo {
			SourceJniType                   = jniSourceType,
			SourceJniMethodName             = jniMethodName.ToString (),
			SourceJniMethodSignature        = sourceSignature,
			TargetJniTypeUtf8               = (IntPtr)method->target_type,
			TargetJniMethodNameUtf8         = (IntPtr)method->target_name,
			TargetJniMethodParameterCount   = paramCount,
			TargetJniMethodInstanceToStatic = isStatic,
		};
		if (method->target_signature != null) {
			ret.TargetJniMethodSignatureUtf8 = (IntPtr)method->target_signature;
		} else {
			ret.TargetJniMethodSignature = fallbackTargetSignature;
		}

		if (Logger.LogAssembly) {
			var message = $"Remapping method `{jniSourceType}.{jniMethodName}{jniMethodSignature}` to " +
				$"`{ret.TargetJniType}.{ret.TargetJniMethodName}{ret.TargetJniMethodSignature}`; " +
				$"param-count: {paramCount}; instance-to-static? {isStatic}";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return ret;
	}

	internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (string jniSourceType, string jniFieldName, string jniFieldSignature)
		=> GetReplacementFieldInfo (jniSourceType, jniFieldName.AsSpan (), jniFieldSignature.AsSpan ());

	internal static unsafe JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (string jniSourceType, ReadOnlySpan<char> jniFieldName, ReadOnlySpan<char> jniFieldSignature)
	{
		if (!isInUse) {
			return null;
		}

		return GetReplacementFieldInfoManaged (jniSourceType, jniFieldName, jniFieldSignature);
	}

	static unsafe JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfoManaged (string jniSourceType, ReadOnlySpan<char> jniFieldName, ReadOnlySpan<char> jniFieldSignature)
	{
		NativeJniRemappingData* data = nativeData;
		if (data == null) {
			throw new InvalidOperationException ("JNI remapping data has not been initialized.");
		}

		int sourceTypeLength = Encoding.UTF8.GetByteCount (jniSourceType);
		int nameLength = Encoding.UTF8.GetByteCount (jniFieldName);
		int signatureLength = Encoding.UTF8.GetByteCount (jniFieldSignature);
		byte[]? rentedSourceType = null;
		byte[]? rentedName = null;
		byte[]? rentedSignature = null;
		NativeJniRemappingReplacementField* field;
		try {
			if (sourceTypeLength > StackallocThreshold)
				rentedSourceType = ArrayPool<byte>.Shared.Rent (sourceTypeLength);
			if (nameLength > StackallocThreshold)
				rentedName = ArrayPool<byte>.Shared.Rent (nameLength);
			if (signatureLength > StackallocThreshold)
				rentedSignature = ArrayPool<byte>.Shared.Rent (signatureLength);

			Span<byte> sourceTypeBuffer = rentedSourceType == null
				? stackalloc byte [sourceTypeLength]
				: rentedSourceType.AsSpan (0, sourceTypeLength);
			Span<byte> nameBuffer = rentedName == null
				? stackalloc byte [nameLength]
				: rentedName.AsSpan (0, nameLength);
			Span<byte> signatureBuffer = rentedSignature == null
				? stackalloc byte [signatureLength]
				: rentedSignature.AsSpan (0, signatureLength);
			Encoding.UTF8.GetBytes (jniSourceType, sourceTypeBuffer);
			Encoding.UTF8.GetBytes (jniFieldName, nameBuffer);
			Encoding.UTF8.GetBytes (jniFieldSignature, signatureBuffer);

			field = LookupField (data, sourceTypeBuffer, nameBuffer, signatureBuffer);
		} finally {
			if (rentedSourceType != null)
				ArrayPool<byte>.Shared.Return (rentedSourceType);
			if (rentedName != null)
				ArrayPool<byte>.Shared.Return (rentedName);
			if (rentedSignature != null)
				ArrayPool<byte>.Shared.Return (rentedSignature);
		}

		if (field == null) {
			return null;
		}

		if (field->target_type == null) {
			throw new InvalidOperationException (
				$"JNI remapping entry for `{jniSourceType}.{jniFieldName}` is missing a target type.");
		}
		if (field->target_name == null) {
			throw new InvalidOperationException (
				$"JNI remapping entry for `{jniSourceType}.{jniFieldName}` is missing a target field name.");
		}
		var sourceName = jniFieldName.ToString ();
		var sourceSignature = jniFieldSignature.ToString ();
		var ret = new JniRuntime.ReplacementFieldInfo {
			SourceJniType           = jniSourceType,
			SourceJniFieldName      = sourceName,
			SourceJniFieldSignature = sourceSignature,
			TargetJniTypeUtf8       = (IntPtr)field->target_type,
			TargetJniFieldNameUtf8  = (IntPtr)field->target_name,
		};
		if (field->target_signature != null) {
			ret.TargetJniFieldSignatureUtf8 = (IntPtr)field->target_signature;
		} else {
			ret.TargetJniFieldSignature = sourceSignature;
		}

		if (Logger.LogAssembly) {
			var message = $"Remapping field `{jniSourceType}.{jniFieldName}:{jniFieldSignature}` to " +
				$"`{ret.TargetJniType}.{ret.TargetJniFieldName}:{ret.TargetJniFieldSignature}`";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return ret;
	}

	static unsafe bool Equal (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		if (value.length != (uint)key.Length) {
			return false;
		}

		return new ReadOnlySpan<byte> (value.str, key.Length).SequenceEqual (key);
	}

	static unsafe int Compare (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		var nativeValue = new ReadOnlySpan<byte> (value.str, checked ((int)value.length));
		return nativeValue.SequenceCompareTo (key);
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

	static unsafe byte* LookupType (
		NativeJniRemappingTypeReplacementEntry* entries,
		uint count,
		ReadOnlySpan<byte> key)
	{
		int index = LowerBoundByName (entries, count, sizeof (NativeJniRemappingTypeReplacementEntry), key);
		if (index >= checked ((int)count) || !Equal (entries [index].name, key)) {
			return null;
		}

		return entries [index].replacement;
	}

	static unsafe NativeJniRemappingIndexTypeEntry* FindMethodType (
		NativeJniRemappingData* data,
		ReadOnlySpan<byte> sourceType)
	{
		uint entryCount = data->method_replacement_index_count;
		int index = LowerBoundByName (data->method_replacement_index, entryCount, sizeof (NativeJniRemappingIndexTypeEntry), sourceType);
		if (index >= checked ((int)entryCount) || !Equal (data->method_replacement_index [index].name, sourceType)) {
			return null;
		}

		return &data->method_replacement_index [index];
	}

	static unsafe NativeJniRemappingReplacementMethod* LookupMethod (
		NativeJniRemappingData* data,
		ReadOnlySpan<byte> sourceType,
		ReadOnlySpan<byte> name,
		ReadOnlySpan<byte> signature)
	{
		NativeJniRemappingIndexTypeEntry* type = FindMethodType (data, sourceType);
		if (type == null || type->method_count == 0 || type->methods == null) {
			return null;
		}

		int first = LowerBoundByName (type->methods, type->method_count, sizeof (NativeJniRemappingIndexMethodEntry), name);
		int count = checked ((int)type->method_count);
		if (first >= count || !Equal (type->methods [first].name, name)) {
			return null;
		}

		int last = first + 1;
		while (last < count && Equal (type->methods [last].name, name)) {
			last++;
		}

		if (signature.Length > 0) {
			for (int i = first; i < last; i++) {
				NativeJniRemappingIndexMethodEntry* entry = &type->methods [i];
				if (entry->signature.length != 0 && Equal (entry->signature, signature)) {
					return &entry->replacement;
				}
			}

			int closeParenthesis = signature.Length - 1;
			while (closeParenthesis >= 0 && signature [closeParenthesis] != (byte)')') {
				closeParenthesis--;
			}

			int prefixLength = closeParenthesis + 1;
			if (prefixLength > 0 && prefixLength != signature.Length) {
				ReadOnlySpan<byte> signaturePrefix = signature.Slice (0, prefixLength);
				for (int i = first; i < last; i++) {
					NativeJniRemappingIndexMethodEntry* entry = &type->methods [i];
					if (entry->signature.length != 0 && Equal (entry->signature, signaturePrefix)) {
						return &entry->replacement;
					}
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

	static unsafe NativeJniRemappingIndexFieldTypeEntry* FindFieldType (
		NativeJniRemappingData* data,
		ReadOnlySpan<byte> sourceType)
	{
		uint entryCount = data->field_replacement_index_count;
		int index = LowerBoundByName (data->field_replacement_index, entryCount, sizeof (NativeJniRemappingIndexFieldTypeEntry), sourceType);
		if (index >= checked ((int)entryCount) || !Equal (data->field_replacement_index [index].name, sourceType)) {
			return null;
		}

		return &data->field_replacement_index [index];
	}

	static unsafe NativeJniRemappingReplacementField* LookupField (
		NativeJniRemappingData* data,
		ReadOnlySpan<byte> sourceType,
		ReadOnlySpan<byte> name,
		ReadOnlySpan<byte> signature)
	{
		NativeJniRemappingIndexFieldTypeEntry* type = FindFieldType (data, sourceType);
		if (type == null || type->field_count == 0 || type->fields == null) {
			return null;
		}

		int first = LowerBoundByName (type->fields, type->field_count, sizeof (NativeJniRemappingIndexFieldEntry), name);
		int count = checked ((int)type->field_count);
		if (first >= count || !Equal (type->fields [first].name, name)) {
			return null;
		}

		int last = first + 1;
		while (last < count && Equal (type->fields [last].name, name)) {
			last++;
		}

		if (signature.Length > 0) {
			for (int i = first; i < last; i++) {
				NativeJniRemappingIndexFieldEntry* entry = &type->fields [i];
				if (entry->signature.length != 0 && Equal (entry->signature, signature)) {
					return &entry->replacement;
				}
			}
		}

		for (int i = first; i < last; i++) {
			NativeJniRemappingIndexFieldEntry* entry = &type->fields [i];
			if (entry->signature.length == 0) {
				return &entry->replacement;
			}
		}

		return null;
	}
}
