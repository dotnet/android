#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// A single <c>FieldRVA</c> row together with the size and contents of the mapped data it
	/// points at, plus - when the field is structurally one of the trimmable typemap generator's
	/// null-terminated UTF-8 JNI data fields - its decoded string value.
	/// </summary>
	sealed class FieldRvaEntry
	{
		public FieldDefinitionHandle Field { get; }
		public int RelativeVirtualAddress { get; }
		public byte [] Data { get; }

		/// <summary>
		/// Non-nil when the field's type is a <c>&lt;PrivateImplementationDetails&gt;/__utf8_N</c>
		/// explicit-layout value type, i.e. the field holds a null-terminated UTF-8 JNI name or
		/// signature emitted by <c>Microsoft.Android.Sdk.TrimmableTypeMap</c>.
		/// </summary>
		public TypeDefinitionHandle Utf8SizedType { get; }

		public string? Utf8Value { get; }

		public bool IsUtf8Datum => Utf8Value != null;

		public FieldRvaEntry (FieldDefinitionHandle field, int rva, byte [] data, TypeDefinitionHandle utf8SizedType, string? utf8Value)
		{
			Field = field;
			RelativeVirtualAddress = rva;
			Data = data;
			Utf8SizedType = utf8SizedType;
			Utf8Value = utf8Value;
		}
	}

	/// <summary>
	/// Reads every <c>FieldRVA</c> row of an assembly, resolving the byte length of each mapped
	/// data block from the field's signature so the block can be copied - or replaced - when the
	/// assembly is rebuilt.
	/// </summary>
	sealed class FieldRvaTable
	{
		public const string PrivateImplementationDetailsTypeName = "<PrivateImplementationDetails>";
		public const string Utf8FieldNamePrefix = "__utf8_";

		readonly List<FieldRvaEntry> entries = new ();
		readonly Dictionary<FieldDefinitionHandle, FieldRvaEntry> byField = new ();

		public IReadOnlyList<FieldRvaEntry> Entries => entries;

		/// <summary>
		/// Returns the mapped-data entry for <paramref name="field"/>, or null when the field has
		/// no <c>FieldRVA</c> row.
		/// </summary>
		public FieldRvaEntry? Get (FieldDefinitionHandle field) => byField.TryGetValue (field, out FieldRvaEntry? entry) ? entry : null;

		/// <summary>
		/// Reads every mapped field, in <c>Field</c> row order - which is the order the
		/// <c>FieldRVA</c> table is required to be sorted in.
		/// </summary>
		public static FieldRvaTable Read (PEReader peReader, MetadataReader reader)
		{
			var table = new FieldRvaTable ();

			foreach (FieldDefinitionHandle field in reader.FieldDefinitions) {
				FieldDefinition fieldDef = reader.GetFieldDefinition (field);
				int rva = fieldDef.GetRelativeVirtualAddress ();
				if (rva == 0) {
					continue;
				}

				int size = ComputeFieldDataSize (reader, fieldDef, out TypeDefinitionHandle valueType);
				byte [] data = ReadMappedData (peReader, rva, size, reader.GetString (fieldDef.Name));

				string? utf8Value = TryDecodeUtf8Datum (reader, fieldDef, valueType, data);
				var entry = new FieldRvaEntry (field, rva, data, utf8Value != null ? valueType : default, utf8Value);
				table.entries.Add (entry);
				table.byField [field] = entry;
			}

			if (table.entries.Count != reader.GetTableRowCount (TableIndex.FieldRva)) {
				throw new JniRewriteException ("The FieldRVA table has rows whose fields do not declare an RVA; the assembly is malformed.");
			}

			return table;
		}

		static byte [] ReadMappedData (PEReader peReader, int rva, int size, string fieldName)
		{
			PEMemoryBlock block = peReader.GetSectionData (rva);
			if (block.Length < size) {
				throw new JniRewriteException ($"FieldRVA data for field '{fieldName}' (RVA 0x{rva:X}, {size} byte(s)) extends past the end of its PE section.");
			}
			return block.GetReader (0, size).ReadBytes (size);
		}

		static string? TryDecodeUtf8Datum (MetadataReader reader, FieldDefinition fieldDef, TypeDefinitionHandle valueType, byte [] data)
		{
			const FieldAttributes required = FieldAttributes.Static | FieldAttributes.HasFieldRVA;
			if ((fieldDef.Attributes & required) != required) {
				return null;
			}
			if (valueType.IsNil || !reader.GetString (fieldDef.Name).StartsWith (Utf8FieldNamePrefix, StringComparison.Ordinal)) {
				return null;
			}

			// The datum's type must be the generator's `<PrivateImplementationDetails>/__utf8_N`
			// explicit-layout value type whose size is exactly the field's byte count.
			TypeDefinition sized = reader.GetTypeDefinition (valueType);
			if ((sized.Attributes & TypeAttributes.ExplicitLayout) == 0) {
				return null;
			}
			if (reader.GetString (sized.Name) != Utf8FieldNamePrefix + data.Length.ToString (System.Globalization.CultureInfo.InvariantCulture)) {
				return null;
			}

			TypeDefinitionHandle enclosing = sized.GetDeclaringType ();
			if (enclosing.IsNil || reader.GetString (reader.GetTypeDefinition (enclosing).Name) != PrivateImplementationDetailsTypeName) {
				return null;
			}

			// Rewriting a JNI value to a shorter string preserves the original field type and
			// zero-fills its remaining bytes so metadata tokens do not move. Accept that padding,
			// but reject non-zero data after the first C-string terminator.
			int terminator = Array.IndexOf (data, (byte) 0);
			if (terminator < 0) {
				return null;
			}
			for (int i = terminator + 1; i < data.Length; i++) {
				if (data [i] != 0) {
					return null;
				}
			}

			try {
				return new UTF8Encoding (encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
					.GetString (data, 0, terminator);
			} catch (ArgumentException) {
				return null;
			}
		}

		/// <summary>
		/// Computes the number of bytes a mapped field occupies, from its field signature.
		/// </summary>
		static int ComputeFieldDataSize (MetadataReader reader, FieldDefinition fieldDef, out TypeDefinitionHandle valueType)
		{
			valueType = default;

			BlobReader blob = reader.GetBlobReader (fieldDef.Signature);
			SignatureHeader header = blob.ReadSignatureHeader ();
			if (header.Kind != SignatureKind.Field) {
				throw new JniRewriteException ($"Field '{reader.GetString (fieldDef.Name)}' has a FieldRVA row but its signature is not a field signature.");
			}

			int code = ReadTypeCodeSkippingModifiers (ref blob);
			if (code == (int) SignatureTypeKind.ValueType) {
				EntityHandle handle = blob.ReadTypeHandle ();
				if (handle.Kind != HandleKind.TypeDefinition) {
					throw new JniRewriteException ($"Field '{reader.GetString (fieldDef.Name)}' maps data whose value type lives in another assembly; its size cannot be determined.");
				}
				valueType = (TypeDefinitionHandle) handle;
				return ComputeTypeSize (reader, valueType, reader.GetString (fieldDef.Name));
			}

			int primitiveSize = GetPrimitiveSize (code);
			if (primitiveSize > 0) {
				return primitiveSize;
			}

			throw new JniRewriteException ($"Field '{reader.GetString (fieldDef.Name)}' maps data of an unsupported type (signature element type 0x{code:X2}).");
		}

		static int ReadTypeCodeSkippingModifiers (ref BlobReader blob)
		{
			const int CModReqd = 0x1F;
			const int CModOpt = 0x20;

			int code = blob.ReadCompressedInteger ();
			while (code == CModReqd || code == CModOpt) {
				blob.ReadTypeHandle ();
				code = blob.ReadCompressedInteger ();
			}
			return code;
		}

		static int GetPrimitiveSize (int code)
			=> code switch {
				(int) SignatureTypeCode.Boolean or (int) SignatureTypeCode.SByte or (int) SignatureTypeCode.Byte => 1,
				(int) SignatureTypeCode.Char or (int) SignatureTypeCode.Int16 or (int) SignatureTypeCode.UInt16 => 2,
				(int) SignatureTypeCode.Int32 or (int) SignatureTypeCode.UInt32 or (int) SignatureTypeCode.Single => 4,
				(int) SignatureTypeCode.Int64 or (int) SignatureTypeCode.UInt64 or (int) SignatureTypeCode.Double => 8,
				_ => 0,
			};

		/// <summary>
		/// Determines the byte size of a value type used as FieldRVA-mapped data, from its
		/// explicit <c>ClassLayout</c> row alone. Summing up instance field sizes is deliberately
		/// not attempted as a fallback: without an explicit layout the CLR is free to reorder,
		/// pad, or otherwise size the type differently than a naive sum would predict, and
		/// silently under- or over-sizing the mapped data block risks reading truncated or
		/// out-of-bounds bytes. A type with no explicit layout size is therefore a hard error.
		/// </summary>
		static int ComputeTypeSize (MetadataReader reader, TypeDefinitionHandle handle, string fieldName)
		{
			TypeDefinition type = reader.GetTypeDefinition (handle);
			TypeLayout layout = type.GetLayout ();
			if (layout.IsDefault || layout.Size <= 0) {
				throw new JniRewriteException ($"Field '{fieldName}' maps data whose value type has no explicit ClassLayout size; its size cannot be determined safely.");
			}

			return layout.Size;
		}
	}
}
