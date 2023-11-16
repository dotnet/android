using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	class MonoRuntimesHelpers
	{
		public static string UtilitiesDestinationDir     => Configurables.Paths.InstallMSBuildDir;
		public static string BCLRedistListDestinationDir => Configurables.Paths.InstallBCLFrameworkRedistListDir;
		public static string BCLTestsDestinationDir      => Configurables.Paths.BCLTestsDestDir;
		public static string BCLTestsArchivePath         => Configurables.Paths.BCLTestsArchivePath;
		public static string RuntimeDestinationDir       => Configurables.Paths.InstallMSBuildDir;
		public static string FrameworkListPath           => Configurables.Paths.FrameworkListInstallPath;

		public static readonly Dictionary<BclFileTarget, string> BCLDestinationDirs = new Dictionary<BclFileTarget, string> () {
			{ BclFileTarget.Android,         Configurables.Paths.InstallBCLFrameworkDir },
			{ BclFileTarget.DesignerHost,    Configurables.Paths.InstallHostBCLDir },
			{ BclFileTarget.DesignerWindows, Configurables.Paths.InstallWindowsBCLDir },
		};

		public static readonly Dictionary<BclFileTarget, string> BCLFacadesDestinationDirs = new Dictionary<BclFileTarget, string> () {
			{ BclFileTarget.Android,         Configurables.Paths.InstallBCLFrameworkFacadesDir },
			{ BclFileTarget.DesignerHost,    Configurables.Paths.InstallHostBCLFacadesDir },
			{ BclFileTarget.DesignerWindows, Configurables.Paths.InstallWindowsBCLFacadesDir },
		};

		public static List<Runtime> GetEnabledRuntimes (Runtimes allRuntimes, bool enableLogging)
		{
			if (allRuntimes == null)
				throw new ArgumentNullException (nameof (allRuntimes));

			var context = Context.Instance;
			var enabledRuntimes = new List<Runtime> ();

			if (enableLogging)
				Log.Instance.StatusLine ("Enabled Mono Android runtime ABIs:", ConsoleColor.White);

			foreach (Runtime runtime in allRuntimes.Items.Where (r => r is MonoJitRuntime && r.Enabled)) {
				enabledRuntimes.Add (runtime);
				if (enableLogging)
					Log.Instance.StatusLine ($"  {context.Characters.Bullet} {runtime.DisplayName}");
			}
			return enabledRuntimes;
		}

		public static (string executable, string debugSymbols) GetDestinationPaths (MonoUtilityFile muf)
		{
			string destDir = UtilitiesDestinationDir;
			string targetFileName;

			if (!String.IsNullOrEmpty (muf.TargetName))
				targetFileName = muf.TargetName!;
			else
				targetFileName = Path.GetFileName (muf.SourcePath);

			string destFilePath = Path.Combine (destDir, targetFileName);
			if (String.IsNullOrEmpty (muf.DebugSymbolsPath))
				return (destFilePath, String.Empty);

			return (destFilePath, Path.Combine (destDir, Utilities.GetDebugSymbolsPath (targetFileName)));
		}

		public static (string assembly, string debugSymbols) GetDestinationPaths (BclFile bf)
		{
			string destDir;
			switch (bf.Type) {
				case BclFileType.ProfileAssembly:
					destDir = BCLDestinationDirs [bf.Target];
					break;

				case BclFileType.FacadeAssembly:
					destDir = BCLFacadesDestinationDirs [bf.Target];
					break;

				default:
					throw new InvalidOperationException ($"Unsupported BCL file type {bf.Type} for file {bf.Name}");
			}

			string destFile = Path.Combine (destDir, bf.Name);
			if (bf.ExcludeDebugSymbols)
				return (destFile, String.Empty);

			return (destFile, Path.Combine (destDir, Path.GetFileName (bf.DebugSymbolsPath) ?? String.Empty));
		}

		public static (string assembly, string debugSymbols) GetDestinationPaths (TestAssembly tasm)
		{
			if (tasm == null)
				throw new ArgumentNullException (nameof (tasm));

			bool pdbRequired;
			switch (tasm.TestType) {
				case TestAssemblyType.Reference:
				case TestAssemblyType.TestRunner:
				case TestAssemblyType.XUnit:
				case TestAssemblyType.NUnit:
				case TestAssemblyType.Satellite:
					pdbRequired = tasm.TestType != TestAssemblyType.Satellite && !tasm.ExcludeDebugSymbols;
					break;

				default:
					throw new InvalidOperationException ($"Unsupported test assembly type: {tasm.TestType}");
			}

			string destDir;
			if (tasm.Name.IndexOf (Path.DirectorySeparatorChar) >= 0)
				destDir = Path.Combine (BCLTestsDestinationDir, Path.GetDirectoryName (tasm.Name) ?? String.Empty);
			else
				destDir = BCLTestsDestinationDir;

			string destFile = Path.Combine (destDir, Path.GetFileName (tasm.Name));
			return (destFile, pdbRequired ? Utilities.GetDebugSymbolsPath (destFile) : String.Empty);
		}

		public static (bool skip, string src, string dst) GetRuntimeFilePaths (Runtime runtime, RuntimeFile rtf)
		{
			if (rtf.ShouldSkip != null && rtf.ShouldSkip (runtime))
				return (true, String.Empty, String.Empty);

			return (
				false,
				GetPath ("source", rtf.Source (runtime), Configurables.Paths.MonoSourceFullPath),
				GetPath ("destination", rtf.Destination (runtime), RuntimeDestinationDir)
			);

			string GetPath (string name, string path, string defaultRoot)
			{
				if (String.IsNullOrEmpty (path))
					throw new InvalidOperationException ($"Empty {name} file path");
				if (!Path.IsPathRooted (path))
					path = Path.Combine (defaultRoot, path);
				return Path.GetFullPath (path);
			}
		}

		public static string GetRootDir (Runtime runtime)
		{
			if (runtime == null)
				throw new ArgumentNullException (nameof (runtime));

			return Path.Combine (Configurables.Paths.MonoSDKSRelativeOutputDir, $"android-{runtime.PrefixedName}-{Configurables.Defaults.MonoSdksConfiguration}");
		}

		public static bool AreRuntimeItemsInstalled (Context context, Runtimes runtimes)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));
			if (runtimes == null)
				throw new ArgumentNullException (nameof (runtimes));

			if (!string.IsNullOrEmpty (context.MonoArchiveCustomUrl)) {
				context.Log.StatusLine ("Skipping AreRuntimeItemsInstalled check, due to custom mono archive URL.");
				return false;
			}

			foreach (var bclFile in runtimes.BclFilesToInstall) {
				(string destFilePath, string debugSymbolsDestPath) = GetDestinationPaths (bclFile);
				if (!DoesItemExist (destFilePath, bclFile.ExcludeDebugSymbols, debugSymbolsDestPath))
					return false;
			}

			foreach (var bclFile in runtimes.DesignerHostBclFilesToInstall) {
				(string destFilePath, string debugSymbolsDestPath) = GetDestinationPaths (bclFile);
				if (!DoesItemExist (destFilePath, bclFile.ExcludeDebugSymbols, debugSymbolsDestPath))
					return false;
			}

			foreach (var bclFile in runtimes.DesignerWindowsBclFilesToInstall) {
				(string destFilePath, string debugSymbolsDestPath) = GetDestinationPaths (bclFile);
				if (!DoesItemExist (destFilePath, bclFile.ExcludeDebugSymbols, debugSymbolsDestPath))
					return false;
			}

			foreach (var testFile in runtimes.TestAssemblies) {
				(string destFilePath, string debugSymbolsDestPath) = GetDestinationPaths (testFile);
				if (!DoesItemExist (destFilePath, true, debugSymbolsDestPath))
					return false;
			}

			foreach (var utilFile in runtimes.UtilityFilesToInstall) {
				(string destFilePath, string debugSymbolsDestPath) = GetDestinationPaths (utilFile);
				if (!DoesItemExist (destFilePath, utilFile.IgnoreDebugInfo, debugSymbolsDestPath))
					return false;
			}

			foreach (var runtime in GetEnabledRuntimes (runtimes, true)) {
				foreach (var runtimeFile in runtimes.RuntimeFilesToInstall) {
					(bool skipFile, string src, string dst) = GetRuntimeFilePaths (runtime, runtimeFile);

					if (!skipFile && (dst.Length == 0 || !File.Exists (dst))) {
						Log.Instance.WarningLine ($"File '{dst}' missing, skipping the rest of runtime item file scan");
						return false;
					}
				}
			}

			return true;

			bool DoesItemExist (string destFilePath, bool shouldExcludeSymbols, string debugSymbolsDestPath)
			{
				if (File.Exists (destFilePath) && shouldExcludeSymbols)
					return true;

				if (File.Exists (destFilePath) && !shouldExcludeSymbols && !String.IsNullOrEmpty (debugSymbolsDestPath) && File.Exists (debugSymbolsDestPath))
					return true;

				Log.Instance.DebugLine ($"File '{destFilePath}' or symbols '{debugSymbolsDestPath}' missing, skipping the rest of runtime item file scan");
				return false;
			}
		}
	}
}
