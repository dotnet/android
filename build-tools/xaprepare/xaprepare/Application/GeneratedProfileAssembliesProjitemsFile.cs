using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	class GeneratedProfileAssembliesProjitemsFile : GeneratedFile
	{
		const string FileTop = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- This is a GENERATED FILE -->
<!-- See build-tools/xaprepare/xaprepare/Application/GeneratedProfileAssembliesProjitemsFile.cs -->
<Project DefaultTargets=""Build"" ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
";

		const string FileBottom = @"
</Project>
";

		public GeneratedProfileAssembliesProjitemsFile (string outputPath)
			: base (outputPath)
		{}

		public override void Generate (Context context)
		{
			var runtimes = new Runtimes ();

			IEnumerable<BclFile> facadeAssemblies = runtimes.BclFilesToInstall.Where (f => f.Type == BclFileType.FacadeAssembly);
			IEnumerable<BclFile> profileAssemblies = runtimes.BclFilesToInstall.Where (f => f.Type == BclFileType.ProfileAssembly);
			IEnumerable<TestAssembly> testAssemblies = Runtimes.TestAssemblies;

			EnsureNoDiscrepancies (facadeAssemblies, profileAssemblies, testAssemblies.Where (ta => ta.TestType != TestAssemblyType.Reference && ta.TestType != TestAssemblyType.TestRunner));

			using (var fs = File.Open (OutputPath, FileMode.Create)) {
				using (var sw = new StreamWriter (fs)) {
					GenerateFile (sw, facadeAssemblies, profileAssemblies, testAssemblies);
				}
			}
		}

		void EnsureNoDiscrepancies (IEnumerable<BclFile> facadeAssemblies, IEnumerable<BclFile> profileAssemblies, IEnumerable<TestAssembly> testAssemblies)
		{
			bool failed = false;

			// We compare against the *installed* locations since we will not always need to download and/or build the
			// Mono Archive (when the XA bundle is present) so we can't rely on the *source* locations of those
			// assemblies to be present.
			failed |= FileSetsDiffer (facadeAssemblies, Configurables.Paths.InstallBCLFrameworkFacadesDir, "Façade", new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "nunitlite.dll" });
			failed |= FileSetsDiffer (profileAssemblies, Configurables.Paths.InstallBCLFrameworkDir, "Profile");
			failed |= FileSetsDiffer (testAssemblies, Configurables.Paths.BCLTestsDestDir, "Test");

			if (failed)
				throw new InvalidOperationException ("Profile assembly discrepancies found. Please examine 'build-tools/xaprepare/xaprepare/ConfigAndData/Runtimes.cs' to make sure all assemblies listed above are included");
		}

		bool FileSetsDiffer (IEnumerable<TestAssembly> assemblies, string directoryPath, string batchName, HashSet<string> ignoreFiles = null)
		{
			List<string> tests = FilesFromDir (directoryPath, ignoreFiles).ToList ();
			tests.AddRange (
				FilesFromDir (
					directoryPath,
					globPattern: "*.resources.dll",
					stripPath: false,
					searchSubdirs: true
				).Select (f => Utilities.GetRelativePath (directoryPath, f))
			);

			return FileSetsDiffer (ToStringSet (assemblies), tests, batchName);
		}

		bool FileSetsDiffer (IEnumerable<BclFile> assemblies, string directoryPath, string batchName, HashSet<string> ignoreFiles = null)
		{
			return FileSetsDiffer (ToStringSet (assemblies), FilesFromDir (directoryPath, ignoreFiles), batchName);
		}

		bool FileSetsDiffer (IEnumerable<string> set1, IEnumerable<string> set2, string batchName)
		{
			List<string> diff = set1.Except (set2).ToList ();

			if (diff.Count == 0)
				return false;

			Log.ErrorLine ($"{batchName} assemblies found on disk but missing from xaprepare:");
			foreach (string asm in diff) {
				Log.StatusLine ($"    {Context.Instance.Characters.Bullet} {asm}", ConsoleColor.Cyan);
			}

			return true;
		}

		IEnumerable<string> FilesFromDir (string directoryPath, HashSet<string> ignoreFiles = null, string globPattern = "*.dll", bool stripPath = true, bool searchSubdirs = false)
		{
			IEnumerable<string> files = Directory.EnumerateFiles (
				directoryPath,
				globPattern,
				searchSubdirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
			).Select (f => stripPath ? Path.GetFileName (f) : f);

			if (ignoreFiles == null || ignoreFiles.Count == 0)
				return files;

			return files.Where (f => !ignoreFiles.Contains (f));
		}

		IEnumerable<string> ToStringSet (IEnumerable<BclFile> files)
		{
			return files.Select (bcf => bcf.Name);
		}

		IEnumerable<string> ToStringSet (IEnumerable<TestAssembly> files)
		{
			return files.Select (ta => ta.Name);
		}

		void GenerateFile (StreamWriter sw, IEnumerable<BclFile> facadeAssemblies, IEnumerable<BclFile> profileAssemblies, IEnumerable<TestAssembly> testAssemblies)
		{
			sw.Write (FileTop);

			WriteGroup (sw, "MonoFacadeAssembly", facadeAssemblies);
			WriteGroup (sw, "MonoProfileAssembly", profileAssemblies);
			WriteGroup (sw, testAssemblies);

			sw.Write (FileBottom);
		}

		void WriteGroup (StreamWriter sw, string itemName, IEnumerable<BclFile> files)
		{
			StartGroup (sw);
			foreach (BclFile bcf in files) {
				sw.WriteLine ($"    <{itemName} Include=\"{bcf.Name}\" />");
			}
			EndGroup (sw);
		}

		void WriteGroup (StreamWriter sw, IEnumerable<TestAssembly> files)
		{
			sw.WriteLine ("<!-- Manual fixups -->");
			StartGroup (sw);
			foreach (TestAssembly taf in files) {
				string itemName = "MonoTestAssembly";
				string testType = null;

				switch (taf.TestType) {
					case TestAssemblyType.Satellite:
						itemName = "MonoTestSatelliteAssembly";
						break;

					case TestAssemblyType.XUnit:
						testType = "xunit";
						break;

					case TestAssemblyType.Reference:
						testType = "reference";
						break;

					case TestAssemblyType.TestRunner:
						itemName = "MonoTestRunner";
						break;
				}

				sw.Write ($"    <{itemName} Include=\"{taf.Name}\"");
				if (String.IsNullOrEmpty (testType))
					sw.WriteLine (" />");
				else {
					sw.WriteLine (" >");
					sw.WriteLine ($"      <TestType>{testType}</TestType>");
					sw.WriteLine ($"    </{itemName}>");
				}
			}
			EndGroup (sw);
		}

		void StartGroup (StreamWriter sw)
		{
			sw.WriteLine ("  <ItemGroup>");
		}

		void EndGroup (StreamWriter sw)
		{
			sw.WriteLine ("  </ItemGroup>");
		}
	}
}
