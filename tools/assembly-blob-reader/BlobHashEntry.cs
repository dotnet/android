using System;
using System.IO;

namespace Xamarin.Android.AssemblyBlobReader
{
	class BlobHashEntry
	{
		public bool Is32Bit        { get; }

		public ulong Hash          { get; }
		public uint MappingIndex   { get; }
		public uint LocalBlobIndex { get; }
		public uint BlobID         { get; }

		internal BlobHashEntry (BinaryReader reader, bool is32Bit)
		{
			Is32Bit = is32Bit;

			Hash = reader.ReadUInt64 ();
			MappingIndex = reader.ReadUInt32 ();
			LocalBlobIndex = reader.ReadUInt32 ();
			BlobID = reader.ReadUInt32 ();
		}
	}
}
