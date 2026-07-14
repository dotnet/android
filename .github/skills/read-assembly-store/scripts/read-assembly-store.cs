#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:project ../../../../tools/assembly-store-reader-mk2/AssemblyStore/AssemblyStore.csproj
#:package Mono.Options@6.12.0.148

using System;
using System.Collections.Generic;
using System.Text;

using Mono.Options;
using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

class App
{
	static readonly AndroidTargetArch[] supportedTargetArchitectures = [
		AndroidTargetArch.Arm,
		AndroidTargetArch.X86,
		AndroidTargetArch.Arm64,
		AndroidTargetArch.X86_64,
	];

	static int WriteErrorAndReturn (string message)
	{
		Console.Error.WriteLine (message);
		return 1;
	}

	static bool TryParseArchList (string values, out HashSet<AndroidTargetArch>? arches, out string? errorMessage)
	{
		if (String.IsNullOrEmpty (values)) {
			arches = null;
			errorMessage = null;
			return true;
		}

		var ret = new HashSet<AndroidTargetArch> ();
		foreach (string a in values.Split (',')) {
			string archName = a.Trim ();
			AndroidTargetArch arch = archName.ToLowerInvariant () switch {
				"aarch64" => AndroidTargetArch.Arm64,
				"arm32"   => AndroidTargetArch.Arm,
				"arm64"   => AndroidTargetArch.Arm64,
				"armv7a"  => AndroidTargetArch.Arm,
				"armv8a"  => AndroidTargetArch.Arm64,
				"x64"     => AndroidTargetArch.X86_64,
				_ => Enum.TryParse (archName, ignoreCase: true, out AndroidTargetArch parsed) ? parsed : AndroidTargetArch.None,
			};
			if (Array.IndexOf (supportedTargetArchitectures, arch) < 0) {
				arches = null;
				errorMessage = $"Unknown architecture name '{archName}'. Supported architectures: {GetArchNames ()}";
				return false;
			}
			ret.Add (arch);
		}

		arches = ret;
		errorMessage = null;
		return true;
	}

	static string GetArchNames ()
	{
		return String.Join (", ", supportedTargetArchitectures);
	}

	static int Main (string[] args)
	{
		HashSet<AndroidTargetArch>? arches = null;
		string? archError = null;
		bool showHelp = false;

		var options = new OptionSet {
			"Usage: read-assembly-store [OPTIONS] BLOB_PATH",
			"",
			"  where each BLOB_PATH can point to:",
			"    * aab file",
			"    * apk file",
			"    * index store file (e.g. base_assemblies.blob or libassembly-store.so)",
			"    * arch store file (e.g. base_assemblies.blob)",
			"    * store manifest file (e.g. base_assemblies.manifest)",
			"    * store base name (e.g. base or base_assemblies)",
			"",
			"  In each case the whole set of stores and manifests will be read (if available). Search for the",
			"  various members of the store set (common/main store, arch stores, manifest) is based on this naming",
			"  convention:",
			"",
			"     {BASE_NAME}[.ARCH_NAME].{blob|so|manifest}",
			"",
			"  Whichever file is referenced in `BLOB_PATH`, the BASE_NAME component is extracted and all the found files are read.",
			"  If `BLOB_PATH` points to an aab or an apk, BASE_NAME will always be `assemblies`",
			"",
			{"a|arch=", $"Limit listing of assemblies to these {{ARCHITECTURES}} only.  A comma-separated list of one or more of: {GetArchNames ()}", v => {
				if (!TryParseArchList (v, out arches, out string? errorMessage)) {
					archError = errorMessage;
				}
			}},
			"",
			{"?|h|help", "Show this help screen", v => showHelp = true},
		};

		List<string>? theRest = options.Parse (args);
		if (archError != null) {
			return WriteErrorAndReturn (archError);
		}
		if (theRest == null || theRest.Count == 0 || showHelp) {
			options.WriteOptionDescriptions (Console.Out);
			return showHelp ? 0 : 1;
		}

		string inputFile = theRest[0];
		(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (inputFile);
		if (explorers == null) {
			return WriteErrorAndReturn (errorMessage ?? "Unknown error");
		}

		foreach (AssemblyStoreExplorer store in explorers) {
			if (arches != null && store.TargetArch.HasValue && !arches.Contains (store.TargetArch.Value)) {
				continue;
			}

			var printer = new StorePrettyPrinter (store);
			printer.Show ();
		}

		return 0;
	}
}

class StorePrettyPrinter
{
	readonly AssemblyStoreExplorer explorer;

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
			line.Append (assembly.Name);
			if (assembly.Ignore) {
				line.AppendLine (" <IGNORED>");
			} else {
				line.AppendLine ();
				line.Append ("    PE image data: ");
				FormatOffsetAndSize (line, assembly.DataOffset, assembly.DataSize);
				line.AppendLine ();
				line.Append ("       Debug data: ");
				FormatOffsetAndSize (line, assembly.DebugOffset, assembly.DebugSize);
				line.AppendLine ();
				line.Append ("      Config data: ");
				FormatOffsetAndSize (line, assembly.ConfigOffset, assembly.ConfigSize);
				line.AppendLine ();
			}
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
