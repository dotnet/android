using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_BuildMingwDependencies : Step
	{
		static readonly SortedDictionary <string, (string description, string libraryName)> dependencies = new SortedDictionary <string, (string description, string libraryName)> (StringComparer.Ordinal) {
			{ "mman-win32",  (description: "mmap for Windows", libraryName: "libmman.a") },
		};

		public Step_BuildMingwDependencies () :
			base ("Build MinGW dependencies")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			if (!context.WindowsJitAbisEnabled)
				throw new InvalidOperationException ("No Windows targets enabled, this step should not be called");

			Log.StatusLine ("Dependencies:");
			foreach (var kvp in dependencies) {
				string dependencyDir = kvp.Key;
				(string dependencyDescription, string libraryName) = kvp.Value;
				bool disabled, success;

				(disabled, success) = await ConfigureBuildAndInstall (context, AbiNames.HostJit.Win32, dependencyDir, dependencyDescription, libraryName);
				if (!disabled && !success)
					return false;

				(disabled, success) = await ConfigureBuildAndInstall (context, AbiNames.HostJit.Win64, dependencyDir, dependencyDescription, libraryName);
				if (!disabled && !success)
					return false;
			}

			return true;
		}

		async Task<(bool disabled, bool success)> ConfigureBuildAndInstall (Context context, string abiName, string dependencyDir, string dependencyDescription, string libraryName)
		{
			if (!context.IsHostJitAbiEnabled (abiName)) {
				Log.DebugLine ($"Windows target {abiName} disabled, not building ${dependencyDescription}");
				return (true, true);
			}

			Log.StatusLine ($"  {context.Characters.Bullet} {dependencyDescription}");
			bool sixtyFourBit = context.Is64BitMingwHostAbi (abiName);
			string sourceDir = Path.Combine (Configurables.Paths.ExternalDir, dependencyDir);
			string buildDir = Path.Combine (Configurables.Paths.BuildBinDir, $"{dependencyDir}-{abiName}");

			Log.DebugLine ($"{dependencyDir} source directory: {sourceDir}");
			Log.DebugLine ($"{dependencyDir} build directory: {buildDir}");

			string outputDir;
			if (sixtyFourBit)
				outputDir = "x86_64";
			else
				outputDir = "x86";
			outputDir = Path.Combine (context.Properties.GetRequiredValue (KnownProperties.MingwDependenciesRootDirectory), outputDir);
			Log.DebugLine ($"{dependencyDir} output directory: {outputDir}");

			string outputLibraryPath = Path.Combine (outputDir, "lib", libraryName);
			Log.DebugLine ($"{dependencyDir} output file path: {outputLibraryPath}");

			if (Utilities.FileExists (outputLibraryPath)) {
				Log.StatusLine ("    already built", Context.SuccessColor);
				return (false, true);
			}

			Utilities.DeleteDirectorySilent (buildDir);
			Utilities.CreateDirectory (buildDir);

			Log.StatusLine ("    configuring...");
			List<string> arguments = GetCmakeArguments (context, buildDir, outputDir, sixtyFourBit);
			string logTag = $"{dependencyDir}-{abiName}-configure";
			var cmake = new CMakeRunner (context);
			bool result = await cmake.Run (
				logTag: logTag,
				sourceDirectory: sourceDir,
				workingDirectory: buildDir,
				arguments: arguments
			);

			if (!result)
				return (false, false);

			logTag = $"{dependencyDir}-{abiName}-build";
			Log.StatusLine ("    building...");
			var ninja = new NinjaRunner (context);
			result = await ninja.Run (
				logTag: logTag,
				workingDirectory: buildDir
			);

			if (!result)
				return (false, false);

			logTag = $"{dependencyDir}-{abiName}-install";
			Log.StatusLine ("    installing...");
			result = await ninja.Run (
				logTag: logTag,
				workingDirectory: buildDir,
				arguments: new List<string> { "install", "-v" }
			);

			if (!File.Exists (outputLibraryPath)) {
				Log.ErrorLine ($"Installation of {dependencyDescription} failed");
				return (false, false);
			}

			if (!result)
				return (false, false);

			return (false, true);
		}

		List<string> GetCmakeArguments (Context context, string workingDirectory, string outputDirectory, bool sixtyFourBit)
		{
			string cmakeToolchainFile;

			if (sixtyFourBit) {
				cmakeToolchainFile = Configurables.Paths.Mingw64CmakePath;
			} else {
				cmakeToolchainFile = Configurables.Paths.Mingw32CmakePath;
			}

			return new List <string> {
				"-GNinja",
				"-DCMAKE_MAKE_PROGRAM=ninja",
				"-DBUILD_TESTS=OFF",
				"-DBUILD_SHARED_LIBS=OFF",
				$"-DCMAKE_INSTALL_PREFIX={outputDirectory}",
				"-Wno-dev", // Hushes some warnings that are useless for us
				$"-DCMAKE_TOOLCHAIN_FILE={Utilities.GetRelativePath (workingDirectory, cmakeToolchainFile)}",
			};
		}
	}
}
