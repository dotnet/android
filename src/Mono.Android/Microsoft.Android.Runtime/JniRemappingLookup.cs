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

	unsafe struct NativeJniRemappingString
	{
		public uint  length;
		public byte* str;
	}

	unsafe struct NativeJniRemappingReplacementMethod
	{
		public byte* target_type;
		public byte* target_name;
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

	unsafe struct NativeJniRemappingTypeReplacementEntry
	{
		public NativeJniRemappingString name;
		public byte*                    replacement;
	}

	unsafe struct NativeJniRemappingData
	{
		public NativeJniRemappingTypeReplacementEntry* type_replacements;
		public NativeJniRemappingIndexTypeEntry*        method_replacement_index;
		public uint                                     type_replacement_count;
		public uint                                     method_replacement_index_count;
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
		isInUse = nativeData->type_replacement_count > 0 || nativeData->method_replacement_index_count > 0;
	}

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

	internal static unsafe string? GetReplacementType (string? jniSimpleReference)
	{
		if (jniSimpleReference is null || !isInUse || jniSimpleReference.Length == 0) {
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

			byte* replacement = LookupType (data->type_replacements, data->type_replacement_count, key);
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
		if (method->target_type == null || method->target_name == null) {
			throw new InvalidOperationException (
				$"JNI remapping entry for `{jniSourceType}.{jniMethodName}{jniMethodSignature}` is missing target information.");
		}

		var sourceSignature = jniMethodSignature.ToString ();
		var targetSignature = sourceSignature;
		int? paramCount = null;
		bool isStatic = method->is_static != 0;
		if (isStatic) {
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (sourceSignature) + 1;
			targetSignature = $"(L{jniSourceType};" + sourceSignature.Substring ("(".Length);
		}

		var ret = new JniRuntime.ReplacementMethodInfo {
			SourceJniType                   = jniSourceType,
			SourceJniMethodName             = jniMethodName.ToString (),
			SourceJniMethodSignature        = sourceSignature,
			TargetJniTypeUtf8               = (IntPtr)method->target_type,
			TargetJniMethodNameUtf8         = (IntPtr)method->target_name,
			TargetJniMethodSignature        = targetSignature,
			TargetJniMethodParameterCount   = paramCount,
			TargetJniMethodInstanceToStatic = isStatic,
		};

		if (Logger.LogAssembly) {
			var message = $"Remapping method `{jniSourceType}.{jniMethodName}{jniMethodSignature}` to " +
				$"`{ret.TargetJniType}.{ret.TargetJniMethodName}{targetSignature}`; " +
				$"param-count: {paramCount}; instance-to-static? {isStatic}";
			Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
		}

		return ret;
	}

	static unsafe bool Equal (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		return value.length == (uint)key.Length &&
			new ReadOnlySpan<byte> (value.str, key.Length).SequenceEqual (key);
	}

	static unsafe int Compare (NativeJniRemappingString value, ReadOnlySpan<byte> key)
	{
		return new ReadOnlySpan<byte> (value.str, checked ((int)value.length)).SequenceCompareTo (key);
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

	static unsafe byte* LookupType (NativeJniRemappingTypeReplacementEntry* entries, uint count, ReadOnlySpan<byte> key)
	{
		int index = LowerBoundByName (entries, count, sizeof (NativeJniRemappingTypeReplacementEntry), key);
		if (index >= checked ((int)count) || !Equal (entries [index].name, key)) {
			return null;
		}
		return entries [index].replacement;
	}

	static unsafe NativeJniRemappingReplacementMethod* LookupMethod (
		NativeJniRemappingData* data,
		ReadOnlySpan<byte> sourceType,
		ReadOnlySpan<byte> name,
		ReadOnlySpan<byte> signature)
	{
		int typeIndex = LowerBoundByName (
			data->method_replacement_index,
			data->method_replacement_index_count,
			sizeof (NativeJniRemappingIndexTypeEntry),
			sourceType
		);
		if (typeIndex >= checked ((int)data->method_replacement_index_count) ||
				!Equal (data->method_replacement_index [typeIndex].name, sourceType)) {
			return null;
		}

		NativeJniRemappingIndexTypeEntry* type = &data->method_replacement_index [typeIndex];
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
}
