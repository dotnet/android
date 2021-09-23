using System;
using System.IO;

using Mono.Options;

namespace Xamarin.Android.AssemblyBlobReader
{
	class Program
	{
		static void Main (string[] args)
		{
			var explorer = new BlobExplorer (args[0]);

			string yesno = explorer.IsCompleteSet ? "yes" : "no";
			Console.WriteLine ($"Blob set '{explorer.BlobSetName}':");
			Console.WriteLine ($"  Is complete set? {yesno}");
			Console.WriteLine ($"  Number of blobs in the set: {explorer.NumberOfBlobs}");
			Console.WriteLine ();
			Console.WriteLine ("Assemblies:");

			string infoIndent = "    ";
			foreach (BlobAssembly assembly in explorer.Assemblies) {
				Console.WriteLine ($"  {assembly.RuntimeIndex}:");
				Console.Write ($"{infoIndent}Name: ");
				if (String.IsNullOrEmpty (assembly.Name)) {
					Console.WriteLine ("unknown");
				} else {
					Console.WriteLine (assembly.Name);
				}

				Console.Write ($"{infoIndent}Blob ID: {assembly.Blob.BlobID} (");
				if (String.IsNullOrEmpty (assembly.Blob.Arch)) {
					Console.Write ("shared");
				} else {
					Console.Write (assembly.Blob.Arch);
				}
				Console.WriteLine (")");

				Console.Write ($"{infoIndent}Hashes: 32-bit == ");
				WriteHashValue (assembly.Hash32);

				Console.Write ("; 64-bit == ");
				WriteHashValue (assembly.Hash64);
				Console.WriteLine ();

				Console.WriteLine ($"{infoIndent}Assembly image: offset == {assembly.DataOffset}; size == {assembly.DataSize}");
				WriteOptionalDataLine ("Debug data", assembly.DebugDataOffset, assembly.DebugDataOffset);
				WriteOptionalDataLine ("Config file", assembly.ConfigDataOffset, assembly.ConfigDataSize);

				Console.WriteLine ();
			}

			void WriteOptionalDataLine (string label, uint offset, uint size)
			{
				Console.Write ($"{infoIndent}{label}: ");
				if (offset == 0) {
					Console.WriteLine ("absent");
				} else {
					Console.WriteLine ("offset == {offset}; size == {size}");
				}
			}

			void WriteHashValue (ulong hash)
			{
				if (hash == 0) {
					Console.Write ("unknown");
				} else {
					Console.Write ($"0x{hash:x}");
				}
			}
		}

		static void WriteAssemblySegment (string label, uint offset, uint size)
		{
			if (offset == 0) {
				Console.Write ($"no {label}");
				return;
			}

			Console.Write ($"{label} starts at {offset}, {size} bytes");
		}
	}
}
