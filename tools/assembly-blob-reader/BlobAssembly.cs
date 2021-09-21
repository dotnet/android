using System;
using System.IO;

namespace Xamarin.Android.AssemblyBlobReader
{
	class BlobAssembly
	{
		public uint DataOffset       { get; }
		public uint DataSize         { get; }
		public uint DebugDataOffset  { get; }
		public uint DebugDataSize    { get; }
		public uint ConfigDataOffset { get; }
		public uint ConfigDataSize   { get; }

		internal BlobAssembly (BinaryReader reader)
		{
			DataOffset = reader.ReadUInt32 ();
			DataSize = reader.ReadUInt32 ();
			DebugDataOffset = reader.ReadUInt32 ();
			DebugDataSize = reader.ReadUInt32 ();
			ConfigDataOffset = reader.ReadUInt32 ();
			ConfigDataSize = reader.ReadUInt32 ();
		}
	}
}
