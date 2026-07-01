using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateFiles : Step
	{
		bool atBuildStart;
		bool onlyRequired;

		public Step_GenerateFiles (bool atBuildStart, bool onlyRequired = false)
			: base ("Generating files required by the build")
		{
			this.atBuildStart = atBuildStart;
			this.onlyRequired = onlyRequired;
		}

		protected override Task<bool> Execute (Context context)
		{
			List<GeneratedFile>? filesToGenerate = GetFilesToGenerate (context);
			if (filesToGenerate != null && filesToGenerate.Count > 0) {
				foreach (GeneratedFile gf in filesToGenerate) {
					if (gf == null)
						continue;

					Log.Status ("Generating ");
					Log.Status (Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, gf.OutputPath), ConsoleColor.White);
					if (!String.IsNullOrEmpty (gf.InputPath))
						Log.StatusLine ($" {context.Characters.LeftArrow} ", Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, gf.InputPath), leadColor: ConsoleColor.Cyan, tailColor: ConsoleColor.White);
					else
						Log.StatusLine ();

					gf.Generate (context);
				}
			}

			return Task.FromResult (true);
		}

		List<GeneratedFile>? GetFilesToGenerate (Context context)
		{
			if (atBuildStart) {
				if (onlyRequired) {
					return null;
				} else {
					return new List <GeneratedFile> {
						Get_Configuration_OperatingSystem_props (context),
					};
				}
			}

			if (onlyRequired)
				return null;

			var steps = new List <GeneratedFile> ();

			AddOSSpecificSteps (context, steps);

			return steps;
		}

		partial void AddOSSpecificSteps (Context context, List<GeneratedFile> steps);

		GeneratedFile Get_Configuration_OperatingSystem_props (Context context)
		{
			const string OutputFileName = "Configuration.OperatingSystem.props";

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@OS_NAME@",              context.OS.Name ?? String.Empty },
				{ "@HOST_OS_FLAVOR@",       context.OS.Flavor ?? String.Empty },
				{ "@OS_RELEASE@",           context.OS.Release ?? String.Empty },
				{ "@HOST_CPUS@",            context.OS.CPUCount.ToString () },
				{ "@ARCHITECTURE_BITS@",    context.OS.Is64Bit ? "64" : "32" },
				{ "@JAVA_SDK_VERSION@",     Configurables.Defaults.MicrosoftOpenJDKVersion.ToString () },
				{ "@JavaSdkDirectory@",     context.OS.JavaHome },
				{ "@javac@",                context.OS.JavaCPath },
				{ "@java@",                 context.OS.JavaPath },
				{ "@jar@",                  context.OS.JarPath },
				{ "@NDK_LLVM_TAG@",         $"{context.OS.Type.ToLowerInvariant ()}-x86_64" },
				{ "@MIN_SUPPORTED_JDK_VERSION@",    $"{Configurables.Defaults.MicrosoftMinOpenJDKVersion.Major}.0" },
			};

			return new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BootstrapResourcesDir, $"{OutputFileName}.in"),
				Path.Combine (BuildPaths.XamarinAndroidSourceRoot, OutputFileName)
			);
		}
	}
}
