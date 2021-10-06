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

		public uint Hash32           { get; set; }
		public ulong Hash64          { get; set; }
		public string Name           { get; set; } = String.Empty;
		public uint RuntimeIndex     { get; set; }

		public BlobReader Blob       { get; }
		public string DllName        => MakeFileName ("dll");
		public string PdbName        => MakeFileName ("pdb");
		public string ConfigName     => MakeFileName ("dll.config");

		internal BlobAssembly (BinaryReader reader, BlobReader blob)
		{
			Blob = blob;

			DataOffset = reader.ReadUInt32 ();
			DataSize = reader.ReadUInt32 ();
			DebugDataOffset = reader.ReadUInt32 ();
			DebugDataSize = reader.ReadUInt32 ();
			ConfigDataOffset = reader.ReadUInt32 ();
			ConfigDataSize = reader.ReadUInt32 ();
		}

		public void ExtractImage (string outputDirPath, string? fileName = null)
		{
			Blob.ExtractAssemblyImage (this, MakeOutputFilePath (outputDirPath, "dll", fileName));
		}

		public void ExtractImage (Stream output)
		{
			Blob.ExtractAssemblyImage (this, output);
		}

		public void ExtractDebugData (string outputDirPath, string? fileName = null)
		{
			Blob.ExtractAssemblyDebugData (this, MakeOutputFilePath (outputDirPath, "pdb", fileName));
		}

		public void ExtractDebugData (Stream output)
		{
			Blob.ExtractAssemblyDebugData (this, output);
		}

		public void ExtractConfig (string outputDirPath, string? fileName = null)
		{
			Blob.ExtractAssemblyConfig (this, MakeOutputFilePath (outputDirPath, "dll.config", fileName));
		}

		public void ExtractConfig (Stream output)
		{
			Blob.ExtractAssemblyConfig (this, output);
		}

		string MakeOutputFilePath (string outputDirPath, string extension, string? fileName)
		{
			return Path.Combine (outputDirPath, MakeFileName (extension, fileName));
		}

		string MakeFileName (string extension, string? fileName = null)
		{
			if (String.IsNullOrEmpty (fileName)) {
				fileName = Name;

				if (String.IsNullOrEmpty (fileName)) {
					fileName = $"{Hash32:x}_{Hash64:x}";
				}

				fileName = $"{fileName}.{extension}";
			}

			return fileName!;
		}
	}
}
