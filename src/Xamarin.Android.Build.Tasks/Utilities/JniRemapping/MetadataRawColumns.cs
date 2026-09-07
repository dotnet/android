#nullable enable

using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Reads the handful of metadata table columns that <see cref="MetadataReader"/> does not
	/// surface, straight from the table stream.
	/// </summary>
	static class MetadataRawColumns
	{
		/// <summary>
		/// The <c>ImplMap.MemberForwarded</c> coded index (ECMA-335 II.22.22). The public
		/// metadata APIs expose method imports but not the field imports that this column also
		/// permits.
		/// </summary>
		public static unsafe EntityHandle GetImplMapMemberForwarded (MetadataReader reader, int rowNumber)
		{
			int rowCount = reader.GetTableRowCount (TableIndex.ImplMap);
			if (rowNumber <= 0 || rowNumber > rowCount) {
				throw new ArgumentOutOfRangeException (nameof (rowNumber));
			}

			int fieldCount = reader.GetTableRowCount (TableIndex.Field);
			int methodCount = reader.GetTableRowCount (TableIndex.MethodDef);
			int codedIndexSize = Math.Max (fieldCount, methodCount) < (1 << 15) ? sizeof (ushort) : sizeof (uint);
			int rowSize = reader.GetTableRowSize (TableIndex.ImplMap);
			long rowOffset = reader.GetTableMetadataOffset (TableIndex.ImplMap) + (long) (rowNumber - 1) * rowSize;
			if (rowSize < sizeof (ushort) + codedIndexSize || rowOffset < 0 || rowOffset + rowSize > reader.MetadataLength) {
				throw new JniRewriteException ("The ImplMap table extends past the end of the metadata.");
			}

			var blob = new BlobReader (reader.MetadataPointer + (int) rowOffset + sizeof (ushort), codedIndexSize);
			uint codedIndex = codedIndexSize == sizeof (ushort) ? blob.ReadUInt16 () : blob.ReadUInt32 ();
			int rowId = checked ((int) (codedIndex >> 1));

			if ((codedIndex & 1) == 0) {
				if (rowId == 0 || rowId > fieldCount) {
					throw new JniRewriteException ("An ImplMap row references an invalid Field token.");
				}
				return MetadataTokens.FieldDefinitionHandle (rowId);
			}

			if (rowId == 0 || rowId > methodCount) {
				throw new JniRewriteException ("An ImplMap row references an invalid MethodDef token.");
			}
			return MetadataTokens.MethodDefinitionHandle (rowId);
		}

		/// <summary>
		/// The <c>ExportedType.TypeDefId</c> hint column (ECMA-335 II.22.14). It always occupies
		/// bytes 4..7 of the row, after the fixed-width <c>Flags</c> column.
		/// </summary>
		public static unsafe int GetExportedTypeDefinitionId (MetadataReader reader, int rowNumber)
		{
			int rowSize = reader.GetTableRowSize (TableIndex.ExportedType);
			long rowOffset = reader.GetTableMetadataOffset (TableIndex.ExportedType) + (long) (rowNumber - 1) * rowSize;
			if (rowSize < 8 || rowOffset < 0 || rowOffset + rowSize > reader.MetadataLength) {
				throw new JniRewriteException ("The ExportedType table extends past the end of the metadata.");
			}

			var blob = new BlobReader (reader.MetadataPointer + (int) rowOffset + sizeof (uint), sizeof (uint));
			return blob.ReadInt32 ();
		}
	}
}
