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

		IEnumerable<GitSubmoduleInfo>?  gitSubmodules;
		string?                         xaCommit;


		public Step_GenerateFiles (bool atBuildStart, bool onlyRequired = false)
			: base ("Generating files required by the build")
		{
			this.atBuildStart = atBuildStart;
			this.onlyRequired = onlyRequired;
		}

		protected override async Task<bool> Execute (Context context)
		{
			var git                 = new GitRunner (context);
			xaCommit                = git.GetTopCommitHash (workingDirectory: BuildPaths.XamarinAndroidSourceRoot, shortHash: false);
			var gitSubmoduleInfo    = await git.ConfigList (new[]{"--blob", "HEAD:.gitmodules"});
			var gitSubmoduleStatus  = await git.SubmoduleStatus ();
			gitSubmodules           = GitSubmoduleInfo.GetGitSubmodules (gitSubmoduleInfo, gitSubmoduleStatus);

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

			return true;
		}

		List<GeneratedFile>? GetFilesToGenerate (Context context)
		{
			if (atBuildStart) {
				if (onlyRequired) {
					return new List<GeneratedFile> {
						Get_SourceLink_Json (context),
					};
				} else {
					return new List <GeneratedFile> {
						Get_SourceLink_Json (context),
						Get_Configuration_OperatingSystem_props (context),
						Get_XABuildConfig_cs (context),
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

		GeneratedFile Get_XABuildConfig_cs (Context context)
		{
			const string OutputFileName = "XABuildConfig.cs";

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@NDK_REVISION@",              context.BuildInfo.NDKRevision },
				{ "@NDK_RELEASE@",               BuildAndroidPlatforms.AndroidNdkVersion },
				{ "@NDK_VERSION_MAJOR@",         context.BuildInfo.NDKVersionMajor },
				{ "@NDK_VERSION_MINOR@",         context.BuildInfo.NDKVersionMinor },
				{ "@NDK_VERSION_MICRO@",         context.BuildInfo.NDKVersionMicro },
				{ "@NDK_ARMEABI_V7_API@",        BuildAndroidPlatforms.NdkMinimumAPILegacy32.ToString () },
				{ "@NDK_ARM64_V8A_API@",         BuildAndroidPlatforms.NdkMinimumAPI.ToString () },
				{ "@NDK_X86_API@",               BuildAndroidPlatforms.NdkMinimumAPILegacy32.ToString ().ToString () },
				{ "@NDK_X86_64_API@",            BuildAndroidPlatforms.NdkMinimumAPI.ToString ().ToString () },
				{ "@XA_SUPPORTED_ABIS@",         context.Properties.GetRequiredValue (KnownProperties.AndroidSupportedTargetJitAbis).Replace (':', ';') },
				{ "@SDK_BUILD_TOOLS_VERSION@",   context.Properties.GetRequiredValue (KnownProperties.XABuildToolsFolder) },
				{ "@ANDROID_DEFAULT_MINIMUM_DOTNET_API_LEVEL@", GetMajor (context.Properties.GetRequiredValue (KnownProperties.AndroidMinimumDotNetApiLevel)) },
				{ "@ANDROID_DEFAULT_MINIMUM_DOTNET_API_LEVEL_MINOR@", GetMinor (context.Properties.GetRequiredValue (KnownProperties.AndroidMinimumDotNetApiLevel)) },
				{ "@ANDROID_DEFAULT_TARGET_DOTNET_API_LEVEL@", GetMajor (context.Properties.GetRequiredValue (KnownProperties.AndroidDefaultTargetDotnetApiLevel)) },
				{ "@ANDROID_DEFAULT_TARGET_DOTNET_API_LEVEL_MINOR@", GetMinor (context.Properties.GetRequiredValue (KnownProperties.AndroidDefaultTargetDotnetApiLevel)) },
				{ "@ANDROID_LATEST_STABLE_API_LEVEL@", GetMajor (context.Properties.GetRequiredValue (KnownProperties.AndroidLatestStableApiLevel)) },
				{ "@ANDROID_LATEST_STABLE_API_LEVEL_MINOR@", GetMinor (context.Properties.GetRequiredValue (KnownProperties.AndroidLatestStableApiLevel)) },
				{ "@ANDROID_LATEST_UNSTABLE_API_LEVEL@", GetMajor (context.Properties.GetRequiredValue (KnownProperties.AndroidLatestUnstableApiLevel)) },
				{ "@ANDROID_LATEST_UNSTABLE_API_LEVEL_MINOR@", GetMinor (context.Properties.GetRequiredValue (KnownProperties.AndroidLatestUnstableApiLevel)) },
				{ "@XAMARIN_ANDROID_VERSION@",   context.Properties.GetRequiredValue (KnownProperties.ProductVersion) },
				{ "@XAMARIN_ANDROID_COMMIT_HASH@", context.BuildInfo.XACommitHash },
				{ "@XAMARIN_ANDROID_BRANCH@", context.BuildInfo.XABranch },
			};

			return new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BuildToolsScriptsDir, $"{OutputFileName}.in"),
				Path.Combine (Configurables.Paths.BuildBinDir, OutputFileName)
			);

			static string GetMajor (string value)
			{
				var dot = value.IndexOf ('.');
				if (dot < 0) {
					return value;
				}
				return value.Substring (0, dot);
			}

			static string GetMinor (string value)
			{
				var dot = value.IndexOf ('.');
				if (dot < 0) {
					return "0";
				}
				return value.Substring (dot + 1);
			}
		}

		public GeneratedFile Get_SourceLink_Json (Context context)
		{
			if (gitSubmodules == null || xaCommit == null) {
				return new SkipGeneratedFile ();
			}
			return new GeneratedSourceLinkJsonFile (
					gitSubmodules!,
					xaCommit!,
					Path.Combine (Configurables.Paths.BuildBinDir, "SourceLink.json")
			);
		}
	}
}
