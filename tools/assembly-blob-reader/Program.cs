using System;
using System.IO;

using Mono.Options;

namespace Xamarin.Android.AssemblyBlobReader
{
	class Program
	{
		static void Main (string[] args)
		{
			using (var fs = File.OpenRead (args[0])) {
				var br = new BlobReader (fs);
				Console.WriteLine ($"Blob {args[0]} information:");
				Console.WriteLine ($"  Format version: {br.Version}");
				Console.WriteLine ($"  Local entry count: {br.LocalEntryCount}");
				Console.WriteLine ($"  Global entry count: {br.GlobalEntryCount}");
				Console.WriteLine ($"  Blob ID: {br.BlobID}");

				string yesno = br.HasGlobalIndex ? "yes" : "no";
				Console.WriteLine ($"  Contains global index: {yesno}");

				Console.WriteLine ();
				Console.WriteLine ("Assemblies in the blob:");

				for (int i = 0; i < br.Assemblies.Count; i++) {
					BlobAssembly assembly = br.Assemblies[i];
					Console.Write ($"  {i:d03}: ");
					WriteAssemblySegment ("data", assembly.DataOffset, assembly.DataSize);
					Console.Write ("; ");
					WriteAssemblySegment ("debug data", assembly.DebugDataOffset, assembly.DebugDataSize);
					Console.Write ("; ");
					WriteAssemblySegment ("config file", assembly.ConfigDataOffset, assembly.ConfigDataSize);
					Console.WriteLine ();
				}

				if (br.HasGlobalIndex) {
					Console.WriteLine ();
					Console.WriteLine ("Global index");
					Console.WriteLine ("  32-bit hash entries:");

					Console.WriteLine ("  64-bit hash entries:");
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
