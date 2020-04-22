using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace apkdiff
{
	class DexDiff : EntryDiff
	{
		public override string Name { get { return "Davik executables"; } }

		public override void Compare (string file, string other, string padding)
		{
			using (var dex1 = new DexFile (file, padding)) {
				using (var dex2 = new DexFile (other, padding)) {

					if (dex1.linkSize != dex2.linkSize)
						ApkDescription.PrintDifference ("link section size", dex2.linkSize - dex1.linkSize, "", padding);

					if (dex1.stringIdsSize != dex2.stringIdsSize)
						ApkDescription.PrintDifference ("strings count", dex2.stringIdsSize - dex1.stringIdsSize, "", padding);

					if (dex1.typeIdsSize != dex2.typeIdsSize)
						ApkDescription.PrintDifference ("types count", dex2.typeIdsSize - dex1.typeIdsSize, "", padding);

					if (dex1.protoIdsSize != dex2.protoIdsSize)
						ApkDescription.PrintDifference ("prototypes count", dex2.protoIdsSize - dex1.protoIdsSize, "", padding);

					if (dex1.fieldIdsSize != dex2.fieldIdsSize)
						ApkDescription.PrintDifference ("fields count", dex2.fieldIdsSize - dex1.fieldIdsSize, "", padding);

					if (dex1.methodIdsSize != dex2.methodIdsSize)
						ApkDescription.PrintDifference ("methods count", dex2.methodIdsSize - dex1.methodIdsSize, "", padding);

					if (dex1.classDefsSize != dex2.classDefsSize)
						ApkDescription.PrintDifference ("classes count", dex2.classDefsSize - dex1.classDefsSize, "", padding);

					if (dex1.dataSize != dex2.dataSize)
						ApkDescription.PrintDifference ("data section size", dex2.dataSize - dex1.dataSize, "", padding);

				}
			}
		}
	}

	class DexFile : IDisposable
	{
		// header
		internal uint checksum;
		internal byte [] signature;
		internal uint fileSize;
		internal uint linkSize;
		internal uint linkOffset;
		internal uint mapOffset;
		internal uint stringIdsSize;
		internal uint stringIdsOffset;
		internal uint typeIdsSize;
		internal uint typeIdsOffset;
		internal uint protoIdsSize;
		internal uint protoIdsOffset;
		internal uint fieldIdsSize;
		internal uint fieldIdsOffset;
		internal uint methodIdsSize;
		internal uint methodIdsOffset;
		internal uint classDefsSize;
		internal uint classDefsOffset;
		internal uint dataSize;
		internal uint dataOffset;

		BinaryReader reader;
		FileStream stream;
		string padding;

		public DexFile (string file, string padding = null)
		{
			this.padding = padding;

			if (Program.Verbose)
				Console.WriteLine ($"{padding}Reading DEX file: {file}");

			stream = File.Open (file, FileMode.Open);
			reader = new BinaryReader (stream);

			ReadHeader ();
		}

		void ReadHeader ()
		{
			byte[] magicDEX = { 0x64, 0x65, 0x78, 0x0a, 0x30, 0x33, 0x39, 0x00 };

			var magicBytes = reader.ReadBytes (8);

			for (int i = 0; i < 8; i++) {
				if (Program.Verbose && i == 6)
					Console.WriteLine ($"{padding}DEX file version: {magicBytes [i]:X}");

				if (i != 6 && magicBytes [i] != magicDEX [i])
					throw new FileLoadException ("not DEX file");
			}

			checksum = reader.ReadUInt32 ();
			signature = reader.ReadBytes (20);
			fileSize = reader.ReadUInt32 ();

			var headerSize = reader.ReadUInt32 ();
			if (headerSize != 0x70)
				throw new FileLoadException ($"DEX header has wrong size: {headerSize:X} instead of 0x70");

			var endianTag = reader.ReadUInt32 ();
			if (endianTag != 0x12345678)
				throw new FileLoadException ($"DEX header wrong endianness: {endianTag:X} instead of 0x12345678");

			linkSize = reader.ReadUInt32 ();
			linkOffset = reader.ReadUInt32 ();
			mapOffset = reader.ReadUInt32 ();

			stringIdsSize = reader.ReadUInt32 ();
			stringIdsOffset = reader.ReadUInt32 ();

			typeIdsSize = reader.ReadUInt32 ();
			typeIdsOffset = reader.ReadUInt32 ();

			protoIdsSize = reader.ReadUInt32 ();
			protoIdsOffset = reader.ReadUInt32 ();

			fieldIdsSize = reader.ReadUInt32 ();
			fieldIdsOffset = reader.ReadUInt32 ();

			methodIdsSize = reader.ReadUInt32 ();
			methodIdsOffset = reader.ReadUInt32 ();

			classDefsSize = reader.ReadUInt32 ();
			classDefsOffset = reader.ReadUInt32 ();

			dataSize = reader.ReadUInt32 ();
			dataOffset = reader.ReadUInt32 ();
		}

		private bool disposedValue = false;

		protected virtual void Dispose (bool disposing)
		{
			if (!disposedValue) {
				if (disposing) {
					reader.Dispose ();
					stream.Dispose ();
				}

				disposedValue = true;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
		}
	}
}
