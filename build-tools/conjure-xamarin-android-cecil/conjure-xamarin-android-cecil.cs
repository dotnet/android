using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Cecil;

public class ConjureXamarinAndroidCecil
{
	const string BaseNameReplacement = "Xamarin.Android.Cecil";
	const string CecilAssemblyName = BaseNameReplacement;
	const string CecilMdbAssemblyName = BaseNameReplacement + ".Mdb";
	const string PublicKey =
		"00240000048000009400000006020000" +
		"00240000525341310004000001000100" +
		"79159977D2D03A8E6BEA7A2E74E8D1AF" +
		"CC93E8851974952BB480A12C9134474D" +
		"04062447C37E0E68C080536FCF3C3FBE" +
		"2FF9C979CE998475E506E8CE82DD5B0F" +
		"350DC10E93BF2EEECF874B24770C5081" +
		"DBEA7447FDDAFA277B22DE47D6FFEA44" +
		"9674A4F9FCCF84D15069089380284DBD" +
		"D35F46CDFF12A1BD78E4EF0065D016DF";

	static readonly List<string> internalsVisibleTo = new List<string> {
		$"Xamarin.Android.Cecil.Pdb, PublicKey={PublicKey}",
		$"Xamarin.Android.Cecil.Mdb, PublicKey={PublicKey}"
	};

	public static int Main (string[] args)
	{
		if (args.Length < 2) {
			Console.WriteLine ("Usage: <input directory> <output directory>");
			Console.WriteLine ("  <input directory> must have Mono.Cecil.dll and Mono.Cecil.Mdb.dll assemblies");
			return 1;
		}

		string inputDir = args [0];
		string inputFilePath = Path.Combine (inputDir, "Mono.Cecil.dll");
		string outputDirPath = args [1];

		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (inputFilePath));
		var rp = new ReaderParameters () {
			AssemblyResolver = resolver,
			ReadSymbols = true
		};
		var monoCecil = AssemblyDefinition.ReadAssembly (inputFilePath, rp);
		monoCecil.Name.Name = CecilAssemblyName;

		var ivtCtor = monoCecil.MainModule.ImportReference (typeof (System.Runtime.CompilerServices.InternalsVisibleToAttribute).GetConstructor (new []{typeof(string)}));
		foreach (string ivtParam in internalsVisibleTo) {
			var ca = new CustomAttribute (ivtCtor);
			ca.ConstructorArguments.Add (new CustomAttributeArgument (monoCecil.MainModule.TypeSystem.String, ivtParam));
			monoCecil.CustomAttributes.Add (ca);
		}

		var wp = new WriterParameters {
			WriteSymbols = true
		};

		monoCecil.Write (Path.Combine (outputDirPath, $"{CecilAssemblyName}.dll"), wp);

		inputFilePath = Path.Combine (inputDir, "Mono.Cecil.Mdb.dll");
		var monoCecilMdb = AssemblyDefinition.ReadAssembly (inputFilePath, rp);
		monoCecilMdb.Name.Name = CecilMdbAssemblyName;

		AssemblyNameReference monoCecilRef = monoCecilMdb.MainModule.AssemblyReferences.Single (r => String.Compare ("Mono.Cecil", r.Name, StringComparison.Ordinal) == 0);
		monoCecilRef.Name = CecilAssemblyName;
		monoCecilMdb.Write (Path.Combine (outputDirPath, $"{CecilMdbAssemblyName}.dll"), wp);

		return 0;
	}
}
