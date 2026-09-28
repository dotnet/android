#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Android.Runtime;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

static class JniRemappingAssetWriter
{
	static readonly UTF8Encoding StrictUtf8 = new (false, true);

	public static byte [] Write (JniRemappingEntries entries)
	{
		ArgumentNullException.ThrowIfNull (entries);

		var types = SortTypes (entries.TypeReplacements);
		var reverseTypes = SortTypes (entries.ReverseTypeReplacements);
		var methods = SortMethods (entries.MethodReplacements);
		var fields = SortFields (entries.FieldReplacements);
		int tableLength = checked (
			checked (types.Count + reverseTypes.Count) * JniRemappingAsset.TypeEntrySize +
			methods.Count * JniRemappingAsset.MethodEntrySize +
			fields.Count * JniRemappingAsset.FieldEntrySize);
		int poolOffset = checked (JniRemappingAsset.HeaderSize + tableLength);
		var table = new byte [tableLength];
		using var pool = new MemoryStream ();
		var interned = new Dictionary<string, JniRemappingAsset.StringRef> (StringComparer.Ordinal);

		JniRemappingAsset.StringRef Intern (string value)
		{
			if (value.Length == 0)
				return default;
			if (value.IndexOf ('\0') >= 0)
				throw new InvalidDataException ("JNI remapping strings cannot contain NUL characters.");
			if (interned.TryGetValue (value, out var existing))
				return existing;
			byte [] encoded = StrictUtf8.GetBytes (value);
			var result = new JniRemappingAsset.StringRef (
				checked ((uint)(poolOffset + pool.Length)), checked ((uint)encoded.Length));
			pool.Write (encoded);
			pool.WriteByte (0);
			interned.Add (value, result);
			return result;
		}

		JniRemappingAsset.StringRef InternOptional (string? value)
			=> value is null ? new JniRemappingAsset.StringRef (JniRemappingAsset.MissingStringOffset, 0) : Intern (value);

		void WriteString (int position, JniRemappingAsset.StringRef value)
		{
			JniRemappingAsset.WriteUInt32 (table, position, value.Offset);
			JniRemappingAsset.WriteUInt32 (table, position + 4, value.Length);
		}

		int cursor = 0;
		foreach (var mapping in types) {
			WriteString (cursor, Intern (mapping.Entry.From));
			WriteString (cursor + 8, Intern (mapping.Entry.To));
			cursor += JniRemappingAsset.TypeEntrySize;
		}
		foreach (var mapping in reverseTypes) {
			WriteString (cursor, Intern (mapping.Entry.From));
			WriteString (cursor + 8, Intern (mapping.Entry.To));
			cursor += JniRemappingAsset.TypeEntrySize;
		}
		foreach (var mapping in methods) {
			WriteString (cursor, Intern (mapping.Entry.SourceType));
			WriteString (cursor + 8, Intern (mapping.Entry.SourceMethod));
			WriteString (cursor + 16, Intern (mapping.Entry.SourceMethodSignature));
			WriteString (cursor + 24, Intern (mapping.Entry.TargetType));
			WriteString (cursor + 32, Intern (mapping.Entry.TargetMethod));
			WriteString (cursor + 40, InternOptional (mapping.Entry.TargetMethodSignature));
			JniRemappingAsset.WriteUInt32 (table, cursor + 48, mapping.Entry.TargetIsStatic ? 1u : 0u);
			cursor += JniRemappingAsset.MethodEntrySize;
		}
		foreach (var mapping in fields) {
			WriteString (cursor, Intern (mapping.Entry.SourceType));
			WriteString (cursor + 8, Intern (mapping.Entry.SourceField));
			WriteString (cursor + 16, Intern (mapping.Entry.SourceFieldSignature));
			WriteString (cursor + 24, Intern (mapping.Entry.TargetType));
			WriteString (cursor + 32, Intern (mapping.Entry.TargetField));
			WriteString (cursor + 40, InternOptional (mapping.Entry.TargetFieldSignature));
			cursor += JniRemappingAsset.FieldEntrySize;
		}

		int poolLength = checked ((int)pool.Length);
		var asset = new byte [checked (poolOffset + poolLength)];
		JniRemappingAsset.WriteUInt32 (asset, 0, JniRemappingAsset.Magic);
		JniRemappingAsset.WriteUInt32 (asset, 4, JniRemappingAsset.Version);
		JniRemappingAsset.WriteUInt32 (asset, 8, JniRemappingAsset.HeaderSize);
		JniRemappingAsset.WriteUInt32 (asset, 12, checked ((uint)asset.Length));

		int sectionOffset = JniRemappingAsset.HeaderSize;
		WriteSection (16, types.Count, JniRemappingAsset.TypeEntrySize);
		WriteSection (24, reverseTypes.Count, JniRemappingAsset.TypeEntrySize);
		WriteSection (32, methods.Count, JniRemappingAsset.MethodEntrySize);
		WriteSection (40, fields.Count, JniRemappingAsset.FieldEntrySize);
		JniRemappingAsset.WriteUInt32 (asset, 48, checked ((uint)poolOffset));
		JniRemappingAsset.WriteUInt32 (asset, 52, checked ((uint)poolLength));
		Buffer.BlockCopy (table, 0, asset, JniRemappingAsset.HeaderSize, tableLength);
		Buffer.BlockCopy (pool.GetBuffer (), 0, asset, poolOffset, poolLength);
		_ = new JniRemappingAsset (asset);
		return asset;

		void WriteSection (int headerOffset, int count, int entrySize)
		{
			JniRemappingAsset.WriteUInt32 (asset, headerOffset, checked ((uint)sectionOffset));
			JniRemappingAsset.WriteUInt32 (asset, headerOffset + 4, checked ((uint)count));
			sectionOffset = checked (sectionOffset + count * entrySize);
		}
	}

	static List<(byte [] Key, JniRemappingTypeReplacement Entry)> SortTypes (List<JniRemappingTypeReplacement> entries)
	{
		var sorted = new List<(byte [], JniRemappingTypeReplacement)> (entries.Count);
		foreach (var entry in entries)
			sorted.Add ((StrictUtf8.GetBytes (entry.From), entry));
		sorted.Sort ((left, right) => JniRemappingAsset.CompareUtf8 (left.Item1, right.Item1));
		for (int i = 1; i < sorted.Count; i++) {
			if (JniRemappingAsset.CompareUtf8 (sorted [i - 1].Item1, sorted [i].Item1) == 0)
				throw new InvalidDataException ($"Duplicate JNI remapping type entry '{sorted [i].Item2.From}'.");
		}
		return sorted;
	}

	static List<(byte [] Type, byte [] Name, byte [] Signature, JniRemappingMethodReplacement Entry)> SortMethods (
		List<JniRemappingMethodReplacement> entries)
	{
		var sorted = new List<(byte [], byte [], byte [], JniRemappingMethodReplacement)> (entries.Count);
		foreach (var entry in entries) {
			sorted.Add ((
				StrictUtf8.GetBytes (entry.SourceType),
				StrictUtf8.GetBytes (entry.SourceMethod),
				StrictUtf8.GetBytes (entry.SourceMethodSignature),
				entry));
		}
		sorted.Sort ((left, right) => {
			int result = JniRemappingAsset.CompareUtf8 (left.Item1, right.Item1);
			if (result == 0)
				result = JniRemappingAsset.CompareUtf8 (left.Item2, right.Item2);
			if (result == 0)
				result = SignatureSpecificity (left.Item3).CompareTo (SignatureSpecificity (right.Item3));
			if (result == 0)
				result = JniRemappingAsset.CompareUtf8 (left.Item3, right.Item3);
			return result;
		});
		for (int i = 1; i < sorted.Count; i++) {
			var previous = sorted [i - 1];
			var current = sorted [i];
			if (JniRemappingAsset.CompareUtf8 (previous.Item1, current.Item1) == 0 &&
					JniRemappingAsset.CompareUtf8 (previous.Item2, current.Item2) == 0 &&
					JniRemappingAsset.CompareUtf8 (previous.Item3, current.Item3) == 0)
				throw new InvalidDataException ($"Duplicate JNI remapping method '{current.Item4.SourceType}.{current.Item4.SourceMethod}{current.Item4.SourceMethodSignature}'.");
		}
		return sorted;
	}

	static List<(byte [] Type, byte [] Name, byte [] Signature, JniRemappingFieldReplacement Entry)> SortFields (
		List<JniRemappingFieldReplacement> entries)
	{
		var sorted = new List<(byte [], byte [], byte [], JniRemappingFieldReplacement)> (entries.Count);
		foreach (var entry in entries) {
			sorted.Add ((
				StrictUtf8.GetBytes (entry.SourceType),
				StrictUtf8.GetBytes (entry.SourceField),
				StrictUtf8.GetBytes (entry.SourceFieldSignature),
				entry));
		}
		sorted.Sort ((left, right) => {
			int result = JniRemappingAsset.CompareUtf8 (left.Item1, right.Item1);
			if (result == 0)
				result = JniRemappingAsset.CompareUtf8 (left.Item2, right.Item2);
			return result == 0 ? JniRemappingAsset.CompareUtf8 (left.Item3, right.Item3) : result;
		});
		for (int i = 1; i < sorted.Count; i++) {
			var previous = sorted [i - 1];
			var current = sorted [i];
			if (JniRemappingAsset.CompareUtf8 (previous.Item1, current.Item1) == 0 &&
					JniRemappingAsset.CompareUtf8 (previous.Item2, current.Item2) == 0 &&
					JniRemappingAsset.CompareUtf8 (previous.Item3, current.Item3) == 0)
				throw new InvalidDataException ($"Duplicate JNI remapping field '{current.Item4.SourceType}.{current.Item4.SourceField}:{current.Item4.SourceFieldSignature}'.");
		}
		return sorted;
	}

	static int SignatureSpecificity (ReadOnlySpan<byte> signature)
		=> signature.IsEmpty ? 2 : signature [signature.Length - 1] == (byte)')' ? 1 : 0;
}
