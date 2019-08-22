using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Linker.Steps;
using Xamarin.Android.Tools;

namespace MonoDroid.Tuner
{
	/// <summary>
	/// A subclass of OutputStep that overrides CopyAssembly and timestamps files properly with Files.CopyIfChanged
	/// * original source from: https://github.com/mono/linker/blob/aa65acca5e41fbc1f8f597799381b853049704ff/src/linker/Linker.Steps/OutputStep.cs#L198-L231
	/// </summary>
	class OutputStepWithTimestamps : OutputStep
	{
		static FileInfo GetOriginalAssemblyFileInfo (AssemblyDefinition assembly)
		{
			return new FileInfo (assembly.MainModule.FileName);
		}

		protected override void CopyAssembly (AssemblyDefinition assembly, string directory)
		{
			// Special case.  When an assembly has embedded pdbs, link symbols is not enabled, and the assembly's action is copy,
			// we want to match the behavior of assemblies with the other symbol types and end up with an assembly that does not have symbols.
			// In order to do that, we can't simply copy files.  We need to write the assembly without symbols
			if (assembly.MainModule.HasSymbols && !Context.LinkSymbols && assembly.MainModule.SymbolReader is EmbeddedPortablePdbReader) {
				WriteAssembly (assembly, directory, new WriterParameters ());
				return;
			}

			FileInfo fi = GetOriginalAssemblyFileInfo (assembly);
			string target = Path.GetFullPath (Path.Combine (directory, fi.Name));
			string source = fi.FullName;
			if (source == target)
				return;

			Files.CopyIfChanged (source, target);

			if (!Context.LinkSymbols)
				return;

			var mdb = source + ".mdb";
			if (File.Exists (mdb))
				Files.CopyIfChanged (mdb, target + ".mdb");

			var pdb = Path.ChangeExtension (source, "pdb");
			if (File.Exists (pdb))
				Files.CopyIfChanged (pdb, Path.ChangeExtension (target, "pdb"));
		}
	}
}
