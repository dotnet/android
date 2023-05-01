using System;
using System.IO;
using Mono.Cecil;

public class Remap
{
	public static int Main (String[] args) {
		if (args.Length < 4) {
			Console.WriteLine ("Usage: <input assembly filename> <output assembly filename> <source assembly ref> <target assembly>");
			return 1;
		}
		string in_aname = args [0];
		string out_aname = args [1];
		string ref1 = args [2];
		string target_filename = args [3];

		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (target_filename));
		var rp = new ReaderParameters () {
			AssemblyResolver = resolver,
		};
		var target = AssemblyDefinition.ReadAssembly (target_filename, rp);
		var ad = AssemblyDefinition.ReadAssembly (in_aname, rp);
		bool found = false;
		var arefs = ad.MainModule.AssemblyReferences;
		for (int i = 0; i < arefs.Count; ++i) {
			var aref = arefs [i];
			if (aref.Name == ref1) {
				arefs [i] = target.Name;
				found = true;
				break;
			}
		}
		if (!found) {
			Console.Error.WriteLine (string.Format ("remap-assembly-ref.exe: warning: Assembly reference '{0}' not found in file '{1}'.", ref1, in_aname));
		}

		ad.Write (out_aname);

		return 0;
	}
}
