using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

class StorePrettyPrinter
{
	AssemblyStoreExplorer explorer;

	public StorePrettyPrinter (AssemblyStoreExplorer storeExplorer)
	{
		explorer = storeExplorer;
	}

	public void Show ()
	{
		Console.WriteLine ($"Store: {explorer.StorePath}");
		Console.WriteLine ($"  Target architecture: {GetTargetArch (explorer)} ({GetBitness (explorer.Is64Bit)}-bit)");
		Console.WriteLine ($"  Assembly count: {explorer.AssemblyCount}");
		Console.WriteLine ($"  Index entry count: {explorer.IndexEntryCount}");
		Console.WriteLine ();

		if (explorer.Assemblies == null || explorer.Assemblies.Count == 0) {
			Console.WriteLine ("NO ASSEMBLIES!");
			return;
		}
		Console.WriteLine ("Assemblies:");
		var line = new StringBuilder ();
		foreach (AssemblyStoreItem assembly in explorer.Assemblies) {
			line.Clear ();
			line.Append ("  ");
			line.Append (assembly.Name);
			line.Append (' ');
			line.Append (FormatOffsetAndSize (assembly.DataOffset, assembly.DataSize));
			line.Append (' ');
			line.Append (FormatOffsetAndSize (assembly.DebugOffset, assembly.DebugSize));
			line.Append (' ');
			line.Append (FormatOffsetAndSize (assembly.ConfigOffset, assembly.ConfigSize));
			line.Append (" [");
			line.Append (FormatHashes (assembly.Hashes));
			line.Append (']');
			Console.WriteLine (line.ToString ());
		}
		Console.WriteLine ();
	}

	static string FormatOffsetAndSize (uint offset, uint size)
	{
		if (offset == 0) {
			return "none";
		}

		return $"{offset} ({size})";
	}

	static string FormatHashes (IList<ulong> hashes)
	{
		if (hashes.Count == 0) {
			return "none";
		}

		var ret = new List<string> ();
		foreach (ulong hash in hashes) {
			ret.Add ($"0x{hash:x}");
		}

		return String.Join (',', ret);
	}

	static string GetBitness (bool is64bit) => is64bit ? "64" : "32";

	static string GetTargetArch (AssemblyStoreExplorer storeExplorer)
	{
		if (storeExplorer.TargetArch == null) {
			return "ABI agnostic";
		}

		return storeExplorer.TargetArch switch {
			AndroidTargetArch.Arm64  => "Arm64",
			AndroidTargetArch.Arm    => "Arm32",
			AndroidTargetArch.X86_64 => "x64",
			AndroidTargetArch.X86    => "x86",
			_ => throw new NotSupportedException ($"Unsupported target architecture {storeExplorer.TargetArch}")
		};
	}
}
