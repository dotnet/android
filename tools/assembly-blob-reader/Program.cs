using System;
using System.IO;

using Mono.Options;

namespace Xamarin.Android.AssemblyBlobReader
{
	class Program
	{
		static void ShowBlobInfo (string blobPath)
		{
			var explorer = new BlobExplorer (blobPath);

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

		static int Main (string[] args)
		{
			if (args.Length == 0) {
				Console.Error.WriteLine ("Usage: read-blob BLOB_PATH [BLOB_PATH ...]");
				Console.Error.WriteLine ();
				Console.Error.WriteLine (@"  where each BLOB_PATH can point to:
    * aab file
    * apk file
    * index blob file (e.g. base_assemblies.blob)
    * arch blob file (e.g. base_assemblies.arm64_v8a.blob)
    * blob manifest file (e.g. base_assemblies.manifest)
    * blob base name (e.g. base or base_assemblies)

  In each case the whole set of blobs and manifests will be read (if available). Search for the
  various members of the blob set (common/main blob, arch blobs, manifest) is based on this naming
  convention:

     {BASE_NAME}[.ARCH_NAME].{blob|manifest}

  Whichever file is referenced in `BLOB_PATH`, the BASE_NAME component is extracted and all the found files are read.
  If `BLOB_PATH` points to an aab or an apk, BASE_NAME will always be `assemblies`

");
				return 1;
			}

			bool first = true;
			foreach (string path in args) {
				ShowBlobInfo (path);
				if (first) {
					first = false;
					continue;
				}

				Console.WriteLine ();
				Console.WriteLine ("***********************************");
				Console.WriteLine ();
			}

			return 0;
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
