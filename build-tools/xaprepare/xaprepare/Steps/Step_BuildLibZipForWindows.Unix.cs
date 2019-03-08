using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_BuildLibZipForWindows : Step
	{
		const string LibZipName = "libzip.dll";

		 // This is for homebrew on mac. We need to do it this way because Configuration.props will not have the
		 // `HostHomebrewPrefix` property set yet - it's *our* task to detect it - and thus the
		 // `MingwZlibRootDirectory{32,64}` properties will *not* have the right path and the build will fail on macOS.
		string zlibRootPrefix = String.Empty;

		public Step_BuildLibZipForWindows ()
			: base ("Cross-building the LibZip library for Windows")
		{
			InitOS ();
		}

		partial void InitOS ();

		protected override async Task<bool> Execute (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			if (!context.WindowsJitAbisEnabled)
				throw new InvalidOperationException ("No Windows targets enabled, this step should not be called");

			bool disabled, success;
			(disabled, success) = await ConfigureBuildAndInstall (context, AbiNames.HostJit.Win32);
			if (!disabled && !success)
				return false;

			(disabled, success) = await ConfigureBuildAndInstall (context, AbiNames.HostJit.Win64);
			if (!disabled && !success)
				return false;

			return true;
		}

		async Task<(bool disabled, bool success)> ConfigureBuildAndInstall (Context context, string abiName)
		{
			if (!context.IsHostJitAbiEnabled (abiName)) {
				Log.DebugLine ($"Windows target {abiName} disabled, not building libzip");
				return (true, true);
			}

			bool sixtyFourBit = context.Is64BitMingwHostAbi (abiName);
			string sourceDir = context.Properties.GetRequiredValue (KnownProperties.LibZipSourceFullPath);
			string buildDir = Path.Combine (Configurables.Paths.BuildBinDir, $"libzip-windows-{abiName}");
			string stampFile = Path.Combine (buildDir, $".build-{context.BuildInfo.FullLibZipHash}");
			string outputPath;

			if (sixtyFourBit)
				outputPath = Path.Combine ("x64", LibZipName);
			else
				outputPath = LibZipName;

			string sourceFile = Path.Combine (buildDir, "lib", LibZipName);
			string destFile = Path.Combine (Configurables.Paths.LibZipOutputPath, outputPath);
			bool needBuild;

			if (Utilities.FileExists (stampFile) && Utilities.FileExists (sourceFile)) {
				Log.DebugLine ($"LibZip-Windows build stamp file exists: {stampFile}");
				Log.StatusLine ($"LibZip for {abiName} already built, skipping compilation");
				needBuild = false;
			} else {
				needBuild = true;
			}

			if (needBuild) {
				Utilities.DeleteDirectorySilent (buildDir);
				Utilities.CreateDirectory (buildDir);
				List<string> arguments = GetCmakeArguments (context, buildDir, sixtyFourBit);

				string logTag = $"libzip-windows-{abiName}";
				var cmake = new CMakeRunner (context);
				bool result = await cmake.Run (
					logTag: logTag,
					sourceDirectory: sourceDir,
					workingDirectory: buildDir,
					arguments: arguments
				);

				if (!result)
					return (false, false);

				var ninja = new NinjaRunner (context);
				result = await ninja.Run (
					logTag: logTag,
					workingDirectory: buildDir
				);

				if (!result)
					return (false, false);
			}

			Utilities.CopyFile (sourceFile, destFile);

			if (!File.Exists (destFile)) {
				Log.ErrorLine ($"Failed to copy {sourceFile} to {destFile}");
				return (false, false);
			}

			TouchStampFile (stampFile);
			return (false, true);
		}

		List<string> GetCmakeArguments (Context context, string workingDirectory, bool sixtyFourBit)
		{
			string cmakeToolchainFile;
			string zlibRoot;

			if (sixtyFourBit) {
				cmakeToolchainFile = Configurables.Paths.Mingw64CmakePath;
				zlibRoot = context.Properties.GetRequiredValue (KnownProperties.MingwZlibRootDirectory64);
			} else {
				cmakeToolchainFile = Configurables.Paths.Mingw32CmakePath;
				zlibRoot = context.Properties.GetRequiredValue (KnownProperties.MingwZlibRootDirectory32);
			}

			if (!String.IsNullOrEmpty (zlibRootPrefix))
				zlibRoot = Path.Combine (zlibRootPrefix, zlibRoot);

			string zlibLibrary = Path.Combine (zlibRoot, "lib", context.Properties.GetRequiredValue (KnownProperties.MingwZlibLibraryName));
			string zlibIncludeDir = Path.Combine (zlibRoot, "include");

			return new List <string> {
				"-GNinja",
				"-DCMAKE_MAKE_PROGRAM=ninja",
				"-DCMAKE_POLICY_DEFAULT_CMP0074=NEW",
				"-DENABLE_GNUTLS=OFF",
				"-DENABLE_OPENSSL=OFF",
				"-DENABLE_COMMONCRYPTO=OFF",
				"-Wno-dev", // Hushes some warnings that are useless for us
				$"-DCMAKE_TOOLCHAIN_FILE={Utilities.GetRelativePath (workingDirectory, cmakeToolchainFile)}",
				$"-DZLIB_ROOT={zlibRoot}",
				$"-DZLIB_LIBRARY={zlibLibrary}",
				$"-DZLIB_INCLUDE_DIR={zlibIncludeDir}"
			};
		}
	}
}
