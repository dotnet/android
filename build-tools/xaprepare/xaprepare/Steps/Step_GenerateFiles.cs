using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateFiles : Step
	{
		bool atBuildStart;

		public Step_GenerateFiles (bool atBuildStart)
			: base ("Generating files required by the build")
		{
			this.atBuildStart = atBuildStart;
		}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			List<GeneratedFile> filesToGenerate = GetFilesToGenerate (context);
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

			return true;
		}
#pragma warning restore CS1998

		List<GeneratedFile> GetFilesToGenerate (Context context)
		{
			if (atBuildStart) {
				return new List <GeneratedFile> {
					Get_Configuration_OperatingSystem_props (context),
					Get_Ndk_projitems (context),
					Get_XABuildPaths_cs (context),
					Get_XABuildConfig_cs (context),
					Get_mingw_32_cmake (context),
					Get_mingw_64_cmake (context),
					Get_bundle_path_targets (context),
				};
			}

			var steps = new List <GeneratedFile> {
				new GeneratedProfileAssembliesProjitemsFile (Configurables.Paths.ProfileAssembliesProjitemsPath),
			};

			AddOSSpecificSteps (context, steps);
			AddUnixPostBuildSteps (context, steps);

			return steps;
		}

		partial void AddUnixPostBuildSteps (Context context, List<GeneratedFile> steps);
		partial void AddOSSpecificSteps (Context context, List<GeneratedFile> steps);

		GeneratedFile Get_Configuration_OperatingSystem_props (Context context)
		{
			const string OutputFileName = "Configuration.OperatingSystem.props";

			string javaSdkDirectory = context.Properties.GetValue ("JavaSdkDirectory");
			if (String.IsNullOrEmpty (javaSdkDirectory))
				javaSdkDirectory = context.OS.JavaHome;

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@OS_NAME@",              context.OS.Name ?? String.Empty },
				{ "@HOST_OS_FLAVOR@",       context.OS.Flavor ?? String.Empty },
				{ "@OS_RELEASE@",           context.OS.Release ?? String.Empty },
				{ "@HOST_TRIPLE@",          context.OS.Triple ?? String.Empty },
				{ "@HOST_TRIPLE32@",        context.OS.Triple32 ?? String.Empty },
				{ "@HOST_TRIPLE64@",        context.OS.Triple64 ?? String.Empty },
				{ "@HOST_CPUS@",            context.OS.CPUCount.ToString () },
				{ "@ARCHITECTURE_BITS@",    context.OS.Is64Bit ? "64" : "32" },
				{ "@HOST_CC@",              context.OS.CC ?? String.Empty },
				{ "@HOST_CXX@",             context.OS.CXX ?? String.Empty },
				{ "@HOST_CC32@",            context.OS.CC32 ?? String.Empty },
				{ "@HOST_CC64@",            context.OS.CC64 ?? String.Empty },
				{ "@HOST_CXX32@",           context.OS.CXX32 ?? String.Empty },
				{ "@HOST_CXX64@",           context.OS.CXX64 ?? String.Empty },
				{ "@HOST_HOMEBREW_PREFIX@", context.OS.HomebrewPrefix ?? String.Empty },
				{ "@JavaSdkDirectory@",     javaSdkDirectory ?? String.Empty },
				{ "@javac@",                context.OS.JavaCPath },
				{ "@java@",                 context.OS.JavaPath },
				{ "@jar@",                  context.OS.JarPath },
				{ "@ANT_DIRECTORY@",        context.OS.AntDirectory },
			};

			return new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BootstrapResourcesDir, $"{OutputFileName}.in"),
				Path.Combine (BuildPaths.XamarinAndroidSourceRoot, OutputFileName)
			);
		}

		GeneratedFile Get_XABuildPaths_cs (Context context)
		{
			const string OutputFileName = "XABuildPaths.cs";

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@CONFIGURATION@", context.Configuration },
				{ "@TOP_DIRECTORY@", BuildPaths.XamarinAndroidSourceRoot },
			};

			return new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BuildToolsScriptsDir, $"{OutputFileName}.in"),
				Path.Combine (Configurables.Paths.TestBinDir, OutputFileName)
			);
		}

		GeneratedFile Get_XABuildConfig_cs (Context context)
		{
			const string OutputFileName = "XABuildConfig.cs";

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@NDK_REVISION@",              context.BuildInfo.NDKRevision },
				{ "@NDK_RELEASE@",               BuildAndroidPlatforms.AndroidNdkVersion },
				{ "@NDK_MINIMUM_API_AVAILABLE@", context.BuildInfo.NDKMinimumApiAvailable },
				{ "@NDK_VERSION_MAJOR@",         context.BuildInfo.NDKVersionMajor },
				{ "@NDK_VERSION_MINOR@",         context.BuildInfo.NDKVersionMinor },
				{ "@NDK_VERSION_MICRO@",         context.BuildInfo.NDKVersionMicro },
				{ "@NDK_ARMEABI_V7_API@",        BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidArmV7a].ToString () },
				{ "@NDK_ARM64_V8A_API@",         BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidArmV8a].ToString () },
				{ "@NDK_X86_API@",               BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidX86].ToString () },
				{ "@NDK_X86_64_API@",            BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidX86_64].ToString () },
				{ "@XA_SUPPORTED_ABIS@",         context.Properties.GetRequiredValue (KnownProperties.AndroidSupportedTargetJitAbis).Replace (':', ';') },
			};

			return new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BuildToolsScriptsDir, $"{OutputFileName}.in"),
				Path.Combine (Configurables.Paths.BuildBinDir, OutputFileName)
			);
		}

		GeneratedFile Get_Ndk_projitems (Context context)
		{
			const string OutputFileName = "Ndk.projitems";

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@NDK_RELEASE@",               BuildAndroidPlatforms.AndroidNdkVersion },
				{ "@NDK_ARMEABI_V7_API@",        BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidArmV7a].ToString () },
				{ "@NDK_ARM64_V8A_API@",         BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidArmV8a].ToString () },
				{ "@NDK_X86_API@",               BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidX86].ToString () },
				{ "@NDK_X86_64_API@",            BuildAndroidPlatforms.NdkMinimumAPI [AbiNames.TargetJit.AndroidX86_64].ToString () },
			};

			return new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BuildToolsScriptsDir, $"{OutputFileName}.in"),
				Path.Combine (Configurables.Paths.BuildBinDir, OutputFileName)
			);
		}

		GeneratedFile Get_mingw_32_cmake (Context context)
		{
			return Get_mingw_cmake (context, Configurables.Paths.Mingw32CmakeTemplatePath, Configurables.Paths.Mingw32CmakePath);
		}

		GeneratedFile Get_mingw_64_cmake (Context context)
		{
			return Get_mingw_cmake (context, Configurables.Paths.Mingw64CmakeTemplatePath, Configurables.Paths.Mingw64CmakePath);
		}

		GeneratedFile Get_mingw_cmake (Context context, string input, string output)
		{
			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@HOMEBREW_PREFIX@", context.OS.HomebrewPrefix ?? String.Empty },
			};

			return new GeneratedPlaceholdersFile (replacements, Path.Combine (input), Path.Combine (output));
		}

		GeneratedFile Get_bundle_path_targets (Context context)
		{
			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@XA_BUNDLE_VERSION@", Configurables.Defaults.XABundleVersion },
				{ "@XA_BUNDLE_FILE_NAME@", Configurables.Paths.XABundleFileName },
			};

			return new GeneratedPlaceholdersFile (replacements, Configurables.Paths.BundlePathTemplate, Configurables.Paths.BundlePathOutput);
		}
	}
}
