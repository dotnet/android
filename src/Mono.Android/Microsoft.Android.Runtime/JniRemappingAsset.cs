#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Version 1 uses a 64-byte little-endian header: magic, version, header size, file size,
/// four (offset, count) pairs, (string-pool offset, length), and two reserved words.
/// Type entries contain two (offset, length) strings (16 bytes); method entries contain six
/// strings and a flags word (52 bytes); field entries contain six strings (48 bytes).
/// Strings are UTF-8 with a trailing NUL for Java.Interop's stable UTF-8 fast path.
/// </summary>
internal sealed class JniRemappingAsset
{
	internal const uint Magic = 0x524a4158; // "XAJR"
	internal const uint Version = 1;
	internal const int HeaderSize = 64;
	internal const int TypeEntrySize = 16;
	internal const int MethodEntrySize = 52;
	internal const int FieldEntrySize = 48;
	internal const uint MissingStringOffset = uint.MaxValue;

	const int AsciiComparisonChunkSize = 16;
	static readonly UTF8Encoding StrictUtf8 = new (false, true);

	readonly byte [] bytes;
	readonly int typeOffset;
	readonly int typeCount;
	readonly int reverseTypeOffset;
	readonly int reverseTypeCount;
	readonly int methodOffset;
	readonly int methodCount;
	readonly int fieldOffset;
	readonly int fieldCount;
	readonly int stringsOffset;
	readonly int stringsLength;

	internal readonly struct StringRef
	{
		public uint Offset { get; }
		public uint Length { get; }
		public bool IsMissing => Offset == MissingStringOffset;

		public StringRef (uint offset, uint length)
		{
			Offset = offset;
			Length = length;
		}
	}

	internal readonly struct MethodReplacement
	{
		public StringRef TargetType { get; }
		public StringRef TargetName { get; }
		public StringRef TargetSignature { get; }
		public StringRef MatchedSignature { get; }
		public bool IsStatic { get; }

		public MethodReplacement (StringRef targetType, StringRef targetName, StringRef targetSignature,
		                          StringRef matchedSignature, bool isStatic)
		{
			TargetType = targetType;
			TargetName = targetName;
			TargetSignature = targetSignature;
			MatchedSignature = matchedSignature;
			IsStatic = isStatic;
		}
	}

	internal readonly struct FieldReplacement
	{
		public StringRef TargetType { get; }
		public StringRef TargetName { get; }
		public StringRef TargetSignature { get; }

		public FieldReplacement (StringRef targetType, StringRef targetName, StringRef targetSignature)
		{
			TargetType = targetType;
			TargetName = targetName;
			TargetSignature = targetSignature;
		}
	}

	// Only the runtime pins this private copy. The bytes never contain process-specific pointers.
	internal byte [] Storage => bytes;
	internal bool IsEmpty => typeCount == 0 && reverseTypeCount == 0 && methodCount == 0 && fieldCount == 0;

	internal JniRemappingAsset (byte [] data) : this (RequireData (data))
	{
	}

	static ReadOnlySpan<byte> RequireData (byte [] data)
	{
		ArgumentNullException.ThrowIfNull (data);
		return data;
	}

	internal JniRemappingAsset (ReadOnlySpan<byte> data)
	{
		bytes = data.ToArray ();
		if (bytes.Length < HeaderSize)
			throw new InvalidDataException ("JNI remapping asset header is truncated.");
		if (ReadUInt32 (bytes, 0) != Magic)
			throw new InvalidDataException ("JNI remapping asset has an invalid magic number.");
		if (ReadUInt32 (bytes, 4) != Version)
			throw new InvalidDataException ($"Unsupported JNI remapping asset version {ReadUInt32 (bytes, 4)}.");
		if (ReadUInt32 (bytes, 8) != HeaderSize || ReadUInt32 (bytes, 12) != (uint)bytes.Length ||
				ReadUInt32 (bytes, 56) != 0 || ReadUInt32 (bytes, 60) != 0)
			throw new InvalidDataException ("JNI remapping asset header has an invalid size or flags.");

		int next = HeaderSize;
		(typeOffset, typeCount) = ReadSection (16, TypeEntrySize, ref next);
		(reverseTypeOffset, reverseTypeCount) = ReadSection (24, TypeEntrySize, ref next);
		(methodOffset, methodCount) = ReadSection (32, MethodEntrySize, ref next);
		(fieldOffset, fieldCount) = ReadSection (40, FieldEntrySize, ref next);

		uint poolOffset = ReadUInt32 (bytes, 48);
		uint poolLength = ReadUInt32 (bytes, 52);
		if (poolOffset != (uint)next || poolLength != (uint)(bytes.Length - next))
			throw new InvalidDataException ("JNI remapping asset string pool is out of bounds.");
		stringsOffset = next;
		stringsLength = (int)poolLength;

		ValidateTypes (typeOffset, typeCount);
		ValidateTypes (reverseTypeOffset, reverseTypeCount);
		ValidateMembers (methodOffset, methodCount, MethodEntrySize, methods: true);
		ValidateMembers (fieldOffset, fieldCount, FieldEntrySize, methods: false);
	}

	(int Offset, int Count) ReadSection (int headerOffset, int entrySize, ref int next)
	{
		uint offset = ReadUInt32 (bytes, headerOffset);
		uint count = ReadUInt32 (bytes, headerOffset + 4);
		long end = (long)offset + (long)count * entrySize;
		if (offset != (uint)next || end > bytes.Length)
			throw new InvalidDataException ("JNI remapping asset table is out of bounds.");
		int start = next;
		next = (int)end;
		return (start, (int)count);
	}

	void ValidateTypes (int offset, int count)
	{
		StringRef previous = default;
		for (int i = 0; i < count; i++) {
			int position = offset + i * TypeEntrySize;
			StringRef source = ReadStringRef (position);
			StringRef target = ReadStringRef (position + 8);
			ValidateString (source, required: true);
			ValidateString (target, required: true);
			if (i > 0 && CompareUtf8 (ReadBytes (previous), ReadBytes (source)) >= 0)
				throw new InvalidDataException ("JNI remapping type entries are unsorted or duplicated.");
			previous = source;
		}
	}

	void ValidateMembers (int offset, int count, int entrySize, bool methods)
	{
		StringRef previousType = default;
		StringRef previousName = default;
		StringRef previousSignature = default;
		for (int i = 0; i < count; i++) {
			int position = offset + i * entrySize;
			StringRef sourceType = ReadStringRef (position);
			StringRef sourceName = ReadStringRef (position + 8);
			StringRef sourceSignature = ReadStringRef (position + 16);
			ValidateString (sourceType, required: true);
			ValidateString (sourceName, required: true);
			ValidateString (sourceSignature, required: false);
			ValidateString (ReadStringRef (position + 24), required: true);
			ValidateString (ReadStringRef (position + 32), required: true);
			ValidateString (ReadStringRef (position + 40), required: true, optional: true);
			if (methods && (ReadUInt32 (bytes, position + 48) & ~1u) != 0)
				throw new InvalidDataException ("JNI remapping method entry has unsupported flags.");

			if (i > 0) {
				int comparison = CompareUtf8 (ReadBytes (previousType), ReadBytes (sourceType));
				if (comparison == 0)
					comparison = CompareUtf8 (ReadBytes (previousName), ReadBytes (sourceName));
				if (comparison == 0 && methods)
					comparison = SignatureSpecificity (ReadBytes (previousSignature)).CompareTo (SignatureSpecificity (ReadBytes (sourceSignature)));
				if (comparison == 0)
					comparison = CompareUtf8 (ReadBytes (previousSignature), ReadBytes (sourceSignature));
				if (comparison >= 0)
					throw new InvalidDataException ("JNI remapping member entries are unsorted or duplicated.");
			}
			previousType = sourceType;
			previousName = sourceName;
			previousSignature = sourceSignature;
		}
	}

	void ValidateString (StringRef value, bool required, bool optional = false)
	{
		if (value.IsMissing) {
			if (!optional || value.Length != 0)
				throw new InvalidDataException ("JNI remapping asset has an invalid optional string.");
			return;
		}
		if (value.Length == 0) {
			if (required || value.Offset != 0)
				throw new InvalidDataException ("JNI remapping asset has an invalid empty string.");
			return;
		}
		if (value.Offset < (uint)stringsOffset || value.Offset >= (uint)(stringsOffset + stringsLength) ||
				value.Length >= (uint)(stringsOffset + stringsLength) - value.Offset)
			throw new InvalidDataException ("JNI remapping asset string is out of bounds.");

		ReadOnlySpan<byte> utf8 = ReadBytes (value);
		if (utf8.IndexOf ((byte)0) >= 0 || bytes [(int)(value.Offset + value.Length)] != 0)
			throw new InvalidDataException ("JNI remapping asset string is not NUL terminated.");
		if (!Ascii.IsValid (utf8)) {
			try {
				StrictUtf8.GetCharCount (utf8);
			} catch (DecoderFallbackException ex) {
				throw new InvalidDataException ("JNI remapping asset contains invalid UTF-8.", ex);
			}
		}
	}

	internal StringRef? FindReplacementType (ReadOnlySpan<char> source)
		=> FindType (typeOffset, typeCount, source);

	internal StringRef? FindReverseType (ReadOnlySpan<char> source)
		=> FindType (reverseTypeOffset, reverseTypeCount, source);

	StringRef? FindType (int offset, int count, ReadOnlySpan<char> source)
	{
		bool ascii = Ascii.IsValid (source);
		int left = 0;
		int right = count;
		while (left < right) {
			int middle = left + (right - left) / 2;
			StringRef name = ReadStringRef (offset + middle * TypeEntrySize);
			if (Compare (ReadBytes (name), source, ascii) < 0)
				left = middle + 1;
			else
				right = middle;
		}
		if (left >= count || Compare (ReadBytes (ReadStringRef (offset + left * TypeEntrySize)), source, ascii) != 0)
			return null;
		return ReadStringRef (offset + left * TypeEntrySize + 8);
	}

	internal MethodReplacement? FindMethod (ReadOnlySpan<char> sourceType, ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
		=> FindMethod (sourceType, default, name, signature);

	internal MethodReplacement? FindMethod (ReadOnlySpan<byte> sourceTypeUtf8, ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
		=> FindMethod (default, sourceTypeUtf8, name, signature);

	MethodReplacement? FindMethod (ReadOnlySpan<char> sourceType, ReadOnlySpan<byte> sourceTypeUtf8,
	                               ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
	{
		int first = FindFirstMember (methodOffset, methodCount, MethodEntrySize, sourceType, sourceTypeUtf8, name);
		if (first < 0)
			return null;
		int last = FindEndOfMember (methodOffset, methodCount, MethodEntrySize, first);

		if (!signature.IsEmpty) {
			bool signatureIsAscii = Ascii.IsValid (signature);
			for (int i = first; i < last; i++) {
				int position = methodOffset + i * MethodEntrySize;
				StringRef entrySignature = ReadStringRef (position + 16);
				if (entrySignature.Length != 0 && Compare (ReadBytes (entrySignature), signature, signatureIsAscii) == 0)
					return ReadMethod (position, entrySignature);
			}

			int end = signature.LastIndexOf (')') + 1;
			if (end > 0 && end < signature.Length) {
				ReadOnlySpan<char> parameters = signature.Slice (0, end);
				bool parametersAreAscii = signatureIsAscii || Ascii.IsValid (parameters);
				for (int i = first; i < last; i++) {
					int position = methodOffset + i * MethodEntrySize;
					StringRef entrySignature = ReadStringRef (position + 16);
					if (entrySignature.Length != 0 && Compare (ReadBytes (entrySignature), parameters, parametersAreAscii) == 0)
						return ReadMethod (position, default);
				}
			}
		}

		for (int i = first; i < last; i++) {
			int position = methodOffset + i * MethodEntrySize;
			if (ReadStringRef (position + 16).Length == 0)
				return ReadMethod (position, default);
		}
		return null;
	}

	MethodReplacement ReadMethod (int position, StringRef matchedSignature)
	{
		return new MethodReplacement (
			ReadStringRef (position + 24),
			ReadStringRef (position + 32),
			ReadStringRef (position + 40),
			matchedSignature,
			(ReadUInt32 (bytes, position + 48) & 1) != 0
		);
	}

	internal FieldReplacement? FindField (ReadOnlySpan<char> sourceType, ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
	{
		int first = FindFirstMember (fieldOffset, fieldCount, FieldEntrySize, sourceType, default, name);
		if (first < 0)
			return null;
		int last = FindEndOfMember (fieldOffset, fieldCount, FieldEntrySize, first);
		bool ascii = Ascii.IsValid (signature);
		for (int i = first; i < last; i++) {
			int position = fieldOffset + i * FieldEntrySize;
			StringRef entrySignature = ReadStringRef (position + 16);
			if (entrySignature.Length != 0 && Compare (ReadBytes (entrySignature), signature, ascii) == 0)
				return ReadField (position);
		}
		for (int i = first; i < last; i++) {
			int position = fieldOffset + i * FieldEntrySize;
			if (ReadStringRef (position + 16).Length == 0)
				return ReadField (position);
		}
		return null;
	}

	FieldReplacement ReadField (int position)
		=> new (ReadStringRef (position + 24), ReadStringRef (position + 32), ReadStringRef (position + 40));

	int FindFirstMember (int offset, int count, int entrySize, ReadOnlySpan<char> sourceType,
	                     ReadOnlySpan<byte> sourceTypeUtf8, ReadOnlySpan<char> name)
	{
		bool typeIsAscii = Ascii.IsValid (sourceType);
		bool nameIsAscii = Ascii.IsValid (name);
		bool typeIsUtf8 = !sourceTypeUtf8.IsEmpty;
		int left = 0;
		int right = count;
		while (left < right) {
			int middle = left + (right - left) / 2;
			int comparison = CompareMember (offset + middle * entrySize, sourceType, sourceTypeUtf8, name,
				typeIsUtf8, typeIsAscii, nameIsAscii);
			if (comparison < 0)
				left = middle + 1;
			else
				right = middle;
		}
		return left < count && CompareMember (offset + left * entrySize, sourceType, sourceTypeUtf8, name,
			typeIsUtf8, typeIsAscii, nameIsAscii) == 0 ? left : -1;
	}

	int CompareMember (int position, ReadOnlySpan<char> sourceType, ReadOnlySpan<byte> sourceTypeUtf8,
	                   ReadOnlySpan<char> name, bool typeIsUtf8, bool typeIsAscii, bool nameIsAscii)
	{
		ReadOnlySpan<byte> type = ReadBytes (ReadStringRef (position));
		int comparison = typeIsUtf8
			? CompareUtf8 (type, sourceTypeUtf8)
			: Compare (type, sourceType, typeIsAscii);
		return comparison != 0
			? comparison
			: Compare (ReadBytes (ReadStringRef (position + 8)), name, nameIsAscii);
	}

	int FindEndOfMember (int offset, int count, int entrySize, int first)
	{
		StringRef type = ReadStringRef (offset + first * entrySize);
		StringRef name = ReadStringRef (offset + first * entrySize + 8);
		int last = first + 1;
		while (last < count) {
			int position = offset + last * entrySize;
			if (CompareUtf8 (ReadBytes (type), ReadBytes (ReadStringRef (position))) != 0 ||
					CompareUtf8 (ReadBytes (name), ReadBytes (ReadStringRef (position + 8))) != 0)
				break;
			last++;
		}
		return last;
	}

	internal string ReadString (StringRef value)
	{
		if (value.IsMissing)
			throw new ArgumentException ("The JNI remapping string is absent.", nameof (value));
		return Encoding.UTF8.GetString (bytes, (int)value.Offset, (int)value.Length);
	}

	internal ReadOnlySpan<byte> ReadBytes (StringRef value)
	{
		if (value.IsMissing)
			throw new ArgumentException ("The JNI remapping string is absent.", nameof (value));
		return bytes.AsSpan ((int)value.Offset, (int)value.Length);
	}

	StringRef ReadStringRef (int position)
		=> new (ReadUInt32 (bytes, position), ReadUInt32 (bytes, position + 4));

	internal static uint ReadUInt32 (ReadOnlySpan<byte> data, int offset)
		=> BinaryPrimitives.ReadUInt32LittleEndian (data.Slice (offset, sizeof (uint)));

	internal static void WriteUInt32 (Span<byte> data, int offset, uint value)
		=> BinaryPrimitives.WriteUInt32LittleEndian (data.Slice (offset, sizeof (uint)), value);

	internal static int CompareUtf8 (ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		=> left.SequenceCompareTo (right);

	static int SignatureSpecificity (ReadOnlySpan<byte> signature)
		=> signature.IsEmpty ? 2 : signature [signature.Length - 1] == (byte)')' ? 1 : 0;

	static int Compare (ReadOnlySpan<byte> utf8, ReadOnlySpan<char> utf16, bool ascii)
		=> ascii ? CompareUtf8ToAscii (utf8, utf16) : CompareUtf8ToUtf16 (utf8, utf16);

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
			int comparison = utf8Rune.Value.CompareTo (utf16Rune.Value);
			if (comparison != 0)
				return comparison;
			utf8 = utf8.Slice (utf8Consumed);
			utf16 = utf16.Slice (utf16Consumed);
		}
		if (utf8.IsEmpty)
			return utf16.IsEmpty ? 0 : -1;
		return 1;
	}
}
