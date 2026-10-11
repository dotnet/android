#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Android.Tasks;

// All offsets in the raw table (including string offsets) are relative to the raw body,
// not to the compression envelope or the containing ELF.
static class JniRemappingBinaryBlob
{
	public const string Symbol = "remapping_data";
	const uint Magic = 0x42424c42;
	const int HeaderSize = 56;
	const int EnvelopeSize = 16;
	const int MaximumRawBodySize = 256 * 1024 * 1024;
	static readonly UTF8Encoding Utf8 = new (false, true);

	sealed record TypeEntry (string Name, string Replacement);
	sealed record Member (string Type, string Name, string? Signature, string TargetType, string TargetName, string? TargetSignature, bool IsStatic);

	public static byte [] Create (string? xmlPath, bool compress)
	{
		if (string.IsNullOrWhiteSpace (xmlPath))
			return [];

		var types = new List<TypeEntry> ();
		var reverse = new List<TypeEntry> ();
		var methods = new List<Member> ();
		var fields = new List<Member> ();
		using (Stream input = File.OpenRead (xmlPath))
		using (var reader = XmlReader.Create (input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) {
			var root = XDocument.Load (reader).Root;
			if (root?.Name != "replacements") {
				throw new InvalidDataException ("Expected a replacements root element.");
			}
			if (root.Nodes ().OfType<XText> ().Any (text => !string.IsNullOrWhiteSpace (text.Value))) {
				throw new InvalidDataException ("Unexpected text in remapping XML.");
			}
			foreach (var element in root.Elements ()) {
				string Required (string name)
				{
					string? value = (string?)element.Attribute (name);
					if (string.IsNullOrEmpty (value) || value.IndexOf ('\0') >= 0) {
						throw new InvalidDataException ($"Missing or invalid {name} on {element.Name}.");
					}
					return value;
				}
				if (element.HasElements || element.Name.Namespace != XNamespace.None) {
					throw new InvalidDataException ("Nested or namespaced remapping entries are not supported.");
				}
				switch (element.Name.LocalName) {
				case "replace-type":
					types.Add (new (Required ("from"), Required ("to")));
					break;
				case "reverse-type":
					reverse.Add (new (Required ("from"), Required ("to")));
					break;
				case "replace-method": {
					string? flag = (string?)element.Attribute ("target-method-instance-to-static");
					bool isStatic = false;
					if (flag != null && !bool.TryParse (flag, out isStatic)) {
						throw new InvalidDataException ("Invalid target-method-instance-to-static flag.");
					}
					methods.Add (new (Required ("source-type"), Required ("source-method-name"),
						Optional (element, "source-method-signature"), Required ("target-type"), Required ("target-method-name"),
						Optional (element, "target-method-signature"), isStatic));
					break;
				}
				case "replace-field":
					fields.Add (new (Required ("source-type"), Required ("source-field-name"),
						Optional (element, "source-field-signature"), Required ("target-type"), Required ("target-field-name"),
						Optional (element, "target-field-signature"), false));
					break;
				default:
					throw new InvalidDataException ($"Unknown remapping element {element.Name}.");
				}
			}
		}

		if (types.Count == 0 && reverse.Count == 0 && methods.Count == 0 && fields.Count == 0) {
			return [];
		}

		types.Sort ((a, b) => Compare (a.Name, b.Name));
		reverse.Sort ((a, b) => Compare (a.Name, b.Name));
		// OrderBy preserves input order for equal keys; exact descriptors still precede
		// parameter-only descriptors and wildcards for the runtime's lookup passes.
		methods = methods.OrderBy (member => member, Comparer<Member>.Create (CompareMethods)).ToList ();
		fields = fields.OrderBy (member => member, Comparer<Member>.Create (CompareFields)).ToList ();
		RejectDuplicates (types.Select (e => e.Name));
		RejectDuplicates (reverse.Select (e => e.Name));
		RejectDuplicates (methods.Select (e => (e.Type, e.Name, e.Signature)));
		RejectDuplicates (fields.Select (e => (e.Type, e.Name, e.Signature)));

		var methodTypes = Group (methods);
		var fieldTypes = Group (fields);
		uint [] counts = [(uint)types.Count, (uint)reverse.Count, (uint)methodTypes.Count, (uint)fieldTypes.Count,
			(uint)methods.Count, (uint)fields.Count];
		uint [] offsets = new uint [7];
		uint cursor = HeaderSize;
		int [] sizes = [12, 12, 16, 16, 32, 28];
		for (int i = 0; i < sizes.Length; i++) {
			offsets [i] = cursor;
			cursor = checked (cursor + checked (counts [i] * (uint)sizes [i]));
		}
		offsets [6] = cursor;
		using var strings = new MemoryStream ();
		strings.WriteByte (0);
		var stringOffsets = new Dictionary<string, uint> (StringComparer.Ordinal);
		uint Add (string? value)
		{
			if (string.IsNullOrEmpty (value)) {
				return 0;
			}
			if (!stringOffsets.TryGetValue (value, out uint offset)) {
				offset = checked (cursor + (uint)strings.Position);
				stringOffsets.Add (value, offset);
				strings.Write (Utf8.GetBytes (value));
				strings.WriteByte (0);
			}
			return offset;
		}
		using var raw = new MemoryStream ();
		using (var writer = new BinaryWriter (raw, Utf8, leaveOpen: true)) {
			foreach (uint count in counts.Take (4)) {
				writer.Write (count);
			}
			foreach (uint offset in offsets) {
				writer.Write (offset);
			}
			writer.Write (0u); // String length, filled in after writing the tables.
			writer.Write (counts [4]);
			writer.Write (counts [5]);
			foreach (var entry in types.Concat (reverse)) {
				writer.Write (Add (entry.Name));
				writer.Write (checked ((uint)Utf8.GetByteCount (entry.Name)));
				writer.Write (Add (entry.Replacement));
			}
			foreach (var group in methodTypes.Concat (fieldTypes)) {
				writer.Write (Add (group.Key));
				writer.Write (checked ((uint)Utf8.GetByteCount (group.Key)));
				writer.Write (group.Start);
				writer.Write (group.Count);
			}
			void WriteMembers (IEnumerable<Member> members, bool isMethod)
			{
				foreach (var entry in members) {
					writer.Write (Add (entry.Name));
					writer.Write (checked ((uint)Utf8.GetByteCount (entry.Name)));
					writer.Write (Add (entry.Signature));
					writer.Write (checked ((uint)(entry.Signature is null ? 0 : Utf8.GetByteCount (entry.Signature))));
					writer.Write (Add (entry.TargetType));
					writer.Write (Add (entry.TargetName));
					writer.Write (Add (entry.TargetSignature));
					if (isMethod) {
						writer.Write (entry.IsStatic ? 1u : 0u);
					}
				}
			}
			WriteMembers (methods, true);
			WriteMembers (fields, false);
			if (raw.Position != cursor) {
				throw new InvalidDataException ("Remapping table size mismatch.");
			}
			writer.Write (strings.ToArray ());
			raw.Position = 44;
			writer.Write (checked ((uint)strings.Length));
		}
		EnsureRawBodySize (raw.Length);
		byte [] body = raw.ToArray ();
		byte [] stored = body;
		if (compress) {
			long max = ZstandardEncoder.GetMaxCompressedLength (body.Length);
			if (max > int.MaxValue) {
				throw new InvalidDataException ("Remapping data exceeds the compression limit.");
			}
			stored = new byte [(int)max];
			if (!ZstandardEncoder.TryCompress (body, stored, out int length, 22, 0)) {
				throw new InvalidDataException ("Could not compress remapping data.");
			}
			Array.Resize (ref stored, length);
		}
		using var envelope = new MemoryStream ();
		using (var writer = new BinaryWriter (envelope, Utf8, leaveOpen: true)) {
			writer.Write (Magic);
			writer.Write ((ushort)1);
			writer.Write ((ushort)(compress ? 1 : 0));
			writer.Write (checked ((uint)stored.Length));
			writer.Write (checked ((uint)body.Length));
			writer.Write (stored);
		}
		byte [] result = envelope.ToArray ();
		Validate (result);
		return result;
	}

	internal static void EnsureRawBodySize (long length)
	{
		if (length > MaximumRawBodySize) {
			throw new InvalidDataException ("Remapping raw table exceeds the 256 MiB runtime limit.");
		}
	}

	static string? Optional (XElement element, string name)
	{
		string? value = (string?)element.Attribute (name);
		if (value?.IndexOf ('\0') >= 0) {
			throw new InvalidDataException ($"Invalid {name}.");
		}
		return string.IsNullOrEmpty (value) ? null : value;
	}

	static int Compare (string a, string b)
	{
		// XML and strict UTF-8 decoding provide well-formed strings. UTF-8 byte order
		// matches scalar value order, unlike UTF-16 ordinal order for supplementary characters.
		var left = a.EnumerateRunes ();
		var right = b.EnumerateRunes ();
		while (true) {
			bool hasLeft = left.MoveNext ();
			bool hasRight = right.MoveNext ();
			if (!hasLeft || !hasRight)
				return hasLeft.CompareTo (hasRight);
			int result = left.Current.Value.CompareTo (right.Current.Value);
			if (result != 0)
				return result;
		}
	}
	static int Specificity (string? signature) => signature == null ? 2 : signature.EndsWith (')') ? 1 : 0;
	static int CompareMethods (Member a, Member b)
	{
		int c = Compare (a.Type, b.Type);
		if (c == 0) c = Compare (a.Name, b.Name);
		if (c == 0) c = Specificity (a.Signature).CompareTo (Specificity (b.Signature));
		return c == 0 ? Compare (a.Signature ?? "", b.Signature ?? "") : c;
	}
	static int CompareFields (Member a, Member b)
	{
		int c = Compare (a.Type, b.Type);
		if (c == 0) c = Compare (a.Name, b.Name);
		return c == 0 ? Compare (a.Signature ?? "", b.Signature ?? "") : c;
	}
	static List<(string Key, uint Start, uint Count)> Group (List<Member> members)
	{
		var result = new List<(string, uint, uint)> ();
		uint index = 0;
		foreach (var member in members) {
			if (result.Count == 0 || result [^1].Item1 != member.Type) {
				result.Add ((member.Type, index, 1));
			} else {
				var previous = result [^1];
				result [^1] = (previous.Item1, previous.Item2, checked (previous.Item3 + 1));
			}
			index = checked (index + 1);
		}
		return result;
	}
	static void RejectDuplicates<T> (IEnumerable<T> keys)
	{
		var seen = new HashSet<T> ();
		foreach (var key in keys) {
			if (!seen.Add (key)) {
				throw new InvalidDataException ($"Ambiguous remapping entry: {key}.");
			}
		}
	}

	public static void Validate (byte [] blob)
	{
		ArgumentNullException.ThrowIfNull (blob);
		if (blob.Length < EnvelopeSize) throw new InvalidDataException ("Truncated blob envelope.");
		using var input = new BinaryReader (new MemoryStream (blob), Utf8);
		if (input.ReadUInt32 () != Magic || input.ReadUInt16 () != 1) throw new InvalidDataException ("Invalid blob magic or version.");
		ushort flags = input.ReadUInt16 ();
		uint storedLength = input.ReadUInt32 ();
		uint rawLength = input.ReadUInt32 ();
		EnsureRawBodySize (rawLength);
		if (flags > 1 || storedLength != blob.Length - EnvelopeSize || rawLength < HeaderSize) {
			throw new InvalidDataException ("Invalid blob envelope lengths or flags.");
		}
		byte [] body;
		if (flags == 0) {
			if (storedLength != rawLength) throw new InvalidDataException ("Uncompressed blob length mismatch.");
			body = input.ReadBytes ((int)rawLength);
		} else {
			body = new byte [(int)rawLength];
			if (!ZstandardDecoder.TryDecompress (blob.AsSpan (EnvelopeSize), body, out int written) || written != body.Length) {
				throw new InvalidDataException ("Invalid compressed blob.");
			}
		}
		ValidateBody (body);
	}

	static void ValidateBody (byte [] body)
	{
		using var reader = new BinaryReader (new MemoryStream (body), Utf8);
		uint [] counts = new uint [6];
		for (int i = 0; i < 4; i++) counts [i] = reader.ReadUInt32 ();
		uint [] offsets = new uint [7];
		for (int i = 0; i < offsets.Length; i++) offsets [i] = reader.ReadUInt32 ();
		uint stringSize = reader.ReadUInt32 ();
		counts [4] = reader.ReadUInt32 ();
		counts [5] = reader.ReadUInt32 ();
		ulong end = HeaderSize;
		int [] strides = [12, 12, 16, 16, 32, 28];
		for (int i = 0; i < strides.Length; i++) {
			if (offsets [i] != end) throw new InvalidDataException ("Invalid table offset.");
			end += (ulong)counts [i] * (uint)strides [i];
		}
		if (offsets [6] != end || end + stringSize != (ulong)body.Length || stringSize == 0 || body [(int)end] != 0) {
			throw new InvalidDataException ("Invalid string section.");
		}
		string ReadString (uint offset, uint? length = null, bool optional = false)
		{
			if (optional && offset == 0 && length.GetValueOrDefault () == 0) return "";
			if (offset <= offsets [6] || (ulong)offset >= end + stringSize || body [offset - 1] != 0) {
				throw new InvalidDataException ("Invalid string offset.");
			}
			int finish = Array.IndexOf (body, (byte)0, (int)offset, checked ((int)(end + stringSize - offset)));
			if (finish < 0 || length.HasValue && finish - offset != length.Value) throw new InvalidDataException ("Invalid string length or terminator.");
			try {
				string value = Utf8.GetString (body, (int)offset, finish - (int)offset);
				if (value.Length == 0) throw new InvalidDataException ("Empty required string.");
				return value;
			} catch (DecoderFallbackException ex) {
				throw new InvalidDataException ("Invalid UTF-8 string.", ex);
			}
		}
		for (int i = 0; i < 4; i++) {
			string? last = null;
			uint next = 0;
			uint total = i == 2 ? counts [4] : counts [5];
			for (uint j = 0; j < counts [i]; j++) {
				reader.BaseStream.Position = checked ((long)(offsets [i] + j * (uint)strides [i]));
				string name = ReadString (reader.ReadUInt32 (), reader.ReadUInt32 ());
				if (last != null && Compare (last, name) >= 0) throw new InvalidDataException ("Unsorted or duplicate type index.");
				last = name;
				if (i < 2) {
					ReadString (reader.ReadUInt32 ());
				} else {
					uint start = reader.ReadUInt32 ();
					uint count = reader.ReadUInt32 ();
					if (count == 0 || start != next || (ulong)start + count > total) throw new InvalidDataException ("Invalid member range.");
					next = checked (next + count);
					string? lastName = null;
					string? lastSignature = null;
					int lastSpecificity = -1;
					for (uint k = start; k < next; k++) {
						reader.BaseStream.Position = checked ((long)(offsets [i + 2] + k * (uint)strides [i + 2]));
						string memberName = ReadString (reader.ReadUInt32 (), reader.ReadUInt32 ());
						uint signatureOffset = reader.ReadUInt32 ();
						uint signatureLength = reader.ReadUInt32 ();
						string signature = ReadString (signatureOffset, signatureLength, optional: true);
						ReadString (reader.ReadUInt32 ());
						ReadString (reader.ReadUInt32 ());
						ReadString (reader.ReadUInt32 (), optional: true);
						int specificity = i == 2 ? Specificity (signatureOffset == 0 ? null : signature) : 0;
						if (lastName != null && (Compare (lastName, memberName) > 0 ||
							Compare (lastName, memberName) == 0 && (lastSpecificity > specificity ||
								lastSpecificity == specificity && Compare (lastSignature ?? "", signature) >= 0))) {
							throw new InvalidDataException ("Unsorted or duplicate member.");
						}
						if (i == 2 && reader.ReadUInt32 () > 1) throw new InvalidDataException ("Invalid method flags.");
						lastName = memberName;
						lastSignature = signature;
						lastSpecificity = specificity;
					}
				}
			}
			if (i >= 2 && next != total) throw new InvalidDataException ("Unreferenced member entries.");
		}
	}
}
