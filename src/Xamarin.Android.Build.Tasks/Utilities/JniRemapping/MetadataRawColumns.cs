#nullable enable

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
		/// The <c>ExportedType.TypeDefId</c> hint column (ECMA-335 II.22.14). It always occupies
		/// bytes 4..7 of the row, after the fixed-width <c>Flags</c> column.
		/// </summary>
		public static unsafe int GetExportedTypeDefinitionId (MetadataReader reader, int rowNumber)
		{
			int rowSize = reader.GetTableRowSize (TableIndex.ExportedType);
			int rowOffset = reader.GetTableMetadataOffset (TableIndex.ExportedType) + (rowNumber - 1) * rowSize;
			if (rowSize < 8 || rowOffset + rowSize > reader.MetadataLength) {
				throw new JniRewriteException ("The ExportedType table extends past the end of the metadata.");
			}

			var blob = new BlobReader (reader.MetadataPointer + rowOffset + sizeof (uint), sizeof (uint));
			return blob.ReadInt32 ();
		}
	}
}
