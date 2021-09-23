using System;
using System.Globalization;

namespace Xamarin.Android.AssemblyBlobReader
{
	class BlobManifestEntry
	{
		// Fields are:
		//  Hash 32 | Hash 64 | Blob ID | Blob idx | Name
		const int NumberOfFields = 5;
		const int Hash32FieldIndex = 0;
		const int Hash64FieldIndex = 1;
		const int BlobIDFieldIndex = 2;
		const int BlobIndexFieldIndex = 3;
		const int NameFieldIndex = 4;

		public uint Hash32 { get; }
		public ulong Hash64 { get; }
		public uint BlobID { get; }
		public uint IndexInBlob { get; }
		public string Name { get; }

		public BlobManifestEntry (string[] fields)
		{
			if (fields.Length != NumberOfFields) {
				throw new ArgumentOutOfRangeException (nameof (fields), "Invalid number of fields");
			}

			Hash32 = GetUInt32 (fields[Hash32FieldIndex]);
			Hash64 = GetUInt64 (fields[Hash64FieldIndex]);
			BlobID = GetUInt32 (fields[BlobIDFieldIndex]);
			IndexInBlob = GetUInt32 (fields[BlobIndexFieldIndex]);
			Name = fields[NameFieldIndex].Trim ();
		}

		uint GetUInt32 (string value)
		{
			if (UInt32.TryParse (PrepHexValue (value), NumberStyles.HexNumber, null, out uint hash)) {
				return hash;
			}

			return 0;
		}

		ulong GetUInt64 (string value)
		{
			if (UInt64.TryParse (PrepHexValue (value), NumberStyles.HexNumber, null, out ulong hash)) {
				return hash;
			}

			return 0;
		}

		string PrepHexValue (string value)
		{
			if (value.StartsWith ("0x", StringComparison.Ordinal)) {
				return value.Substring (2);
			}

			return value;
		}
	}
}
