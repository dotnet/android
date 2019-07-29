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
	const string PublicKey = "024004800094000620002400525341310400101079159977d2d03a8e6bea7a2e74e8d1afcc93e8851974952bb480a12c9134474d462447c37ee68c080536fcf3c3fbe2ff9c979ce998475e56e8ce82dd5bf35dc1e93bf2eeecf874b2477c5081dbea7447fddafa277b22de47d6ffea449674a4f9fccf84d1506989380284dbdd35f46cdff12a1bd78e4ef065d016df";

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
