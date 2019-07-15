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

	static readonly List<string> internalsVisibleTo = new List<string> {
		"Xamarin.Android.Cecil.Pdb, PublicKey=0024000004800000940000000602000000240000525341310004000011000000438ac2a5acfbf16cbd2b2b47a62762f273df9cb2795ceccdf77d10bf508e69e7a362ea7a45455bbf3ac955e1f2e2814f144e5d817efc4c6502cc012df310783348304e3ae38573c6d658c234025821fda87a0be8a0d504df564e2c93b2b878925f42503e9d54dfef9f9586d9e6f38a305769587b1de01f6c0410328b2c9733db",
		"Xamarin.Android.Cecil.Mdb, PublicKey=0024000004800000940000000602000000240000525341310004000011000000438ac2a5acfbf16cbd2b2b47a62762f273df9cb2795ceccdf77d10bf508e69e7a362ea7a45455bbf3ac955e1f2e2814f144e5d817efc4c6502cc012df310783348304e3ae38573c6d658c234025821fda87a0be8a0d504df564e2c93b2b878925f42503e9d54dfef9f9586d9e6f38a305769587b1de01f6c0410328b2c9733db"
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
