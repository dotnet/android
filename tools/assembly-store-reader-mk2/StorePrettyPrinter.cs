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

		var assemblies = new List<AssemblyStoreItem> (explorer.Assemblies);
		assemblies.Sort ((AssemblyStoreItem a, AssemblyStoreItem b) => a.Name.CompareTo (b.Name));

		Console.WriteLine ("Assemblies:");
		var line = new StringBuilder ();
		foreach (AssemblyStoreItem assembly in assemblies) {
			line.Clear ();
			line.Append ("  ");
			line.AppendLine (assembly.Name);
			line.Append ("    PE image data: ");
			FormatOffsetAndSize (line, assembly.DataOffset, assembly.DataSize);
			line.AppendLine ();
			line.Append ("       Debug data: ");
			FormatOffsetAndSize (line, assembly.DebugOffset, assembly.DebugSize);
			line.AppendLine ();
			line.Append ("      Config data: ");
			FormatOffsetAndSize (line, assembly.ConfigOffset, assembly.ConfigSize);
			line.AppendLine ();
			line.Append ("      Name hashes: ");
			FormatHashes (line, assembly.Hashes);
			line.AppendLine ();
			Console.WriteLine (line.ToString ());
		}
		Console.WriteLine ();
	}

	static void FormatOffsetAndSize (StringBuilder sb, uint offset, uint size)
	{
		if (offset == 0) {
			FormatNone (sb);
			return;
		}

		sb.Append ("offset ");
		sb.Append (offset);
		sb.Append (", size ");
		sb.Append (size);
	}

	static void FormatHashes (StringBuilder sb, IList<ulong> hashes)
	{
		if (hashes.Count == 0) {
			FormatNone (sb);
			return;
		}

		bool first = true;
		foreach (ulong hash in hashes) {
			if (first) {
				first = false;
			} else {
				sb.Append (", ");
			}
			sb.Append ($"0x{hash:x}");
		}
	}

	static void FormatNone (StringBuilder sb)
	{
		sb.Append ("none");
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
