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
					Log.Instance.StatusLine ($"  {context.Characters.Bullet} {runtime.Name}");
			}

			if (enableLogging) {
				Log.Instance.StatusLine ();
				Log.Instance.StatusLine ("Enabled Mono host runtimes:", ConsoleColor.White);
			}
			foreach (Runtime runtime in allRuntimes.Items.Where (r => r is MonoHostRuntime && r.Enabled)) {
				enabledRuntimes.Add (runtime);
				if (enableLogging)
					Log.Instance.StatusLine ($"  {context.Characters.Bullet} {runtime.Name}");
			}

			bool anyCrossEnabled = false;
			if (enableLogging) {
				Log.Instance.StatusLine ();
				Log.Instance.StatusLine ("Enabled Mono cross compilers:", ConsoleColor.White);
			}
			foreach (Runtime runtime in allRuntimes.Items.Where (r => r is MonoCrossRuntime && r.Enabled)) {
				anyCrossEnabled = true;
				enabledRuntimes.Add (runtime);
				if (enableLogging)
					Log.Instance.StatusLine ($"  {context.Characters.Bullet} {runtime.Name}");
			}

			if (enableLogging && !anyCrossEnabled)
				Log.Instance.StatusLine ($"  NONE", ConsoleColor.DarkCyan);

			anyCrossEnabled = false;
			if (enableLogging) {
				Log.Instance.StatusLine ();
				Log.Instance.StatusLine ("Enabled LLVM cross compilers:", ConsoleColor.White);
			}
			foreach (Runtime runtime in allRuntimes.Items.Where (r => r is LlvmRuntime && r.Enabled)) {
				anyCrossEnabled = true;
				enabledRuntimes.Add (runtime);
				if (enableLogging)
					Log.Instance.StatusLine ($"  {context.Characters.Bullet} {runtime.Name}");
			}
			if (enableLogging && !anyCrossEnabled)
				Log.Instance.StatusLine ($"  NONE", ConsoleColor.DarkCyan);

			return enabledRuntimes;
		}

		public static (string executable, string debugSymbols) GetDestinationPaths (MonoUtilityFile muf)
		{
			if (muf == null)
				throw new ArgumentNullException (nameof (muf));

			string destDir = UtilitiesDestinationDir;
			string targetFileName;

			if (!String.IsNullOrEmpty (muf.TargetName))
				targetFileName = muf.TargetName;
			else
				targetFileName = Path.GetFileName (muf.SourcePath);

			string destFilePath = Path.Combine (destDir, targetFileName);
			if (String.IsNullOrEmpty (muf.DebugSymbolsPath))
				return (destFilePath, null);

			return (destFilePath, Path.Combine (destDir, Utilities.GetDebugSymbolsPath (targetFileName)));
		}

		public static (string assembly, string debugSymbols) GetDestinationPaths (BclFile bf)
		{
			if (bf == null)
				throw new ArgumentNullException (nameof (bf));

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
			if (bf.ExcludeDebugSymbols || String.IsNullOrEmpty (bf.DebugSymbolsPath))
				return (destFile, null);

			return (destFile, Path.Combine (destDir, Path.GetFileName (bf.DebugSymbolsPath)));
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
				destDir = Path.Combine (BCLTestsDestinationDir, Path.GetDirectoryName (tasm.Name));
			else
				destDir = BCLTestsDestinationDir;

			string destFile = Path.Combine (destDir, Path.GetFileName (tasm.Name));
			return (destFile, pdbRequired ? Utilities.GetDebugSymbolsPath (destFile) : null);
		}

		public static (bool skip, string src, string dst) GetRuntimeFilePaths (Runtime runtime, RuntimeFile rtf)
		{
			if (rtf.ShouldSkip != null && rtf.ShouldSkip (runtime))
				return (true, null, null);

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

		public static bool AllBundleItemsPresent (Runtimes runtimes)
		{
			if (runtimes == null)
				throw new ArgumentNullException (nameof (runtimes));

			bool runtimesFoundAndComplete = true;
			foreach (BundleItem item in runtimes.BundleItems) {
				if (item == null)
					continue;

				// BundleItem.SourcePath is the path *after* the file is installed into our tree
				if (File.Exists (item.SourcePath))
					continue;

				runtimesFoundAndComplete = false;
				Log.Instance.DebugLine ($"{item.SourcePath} missing, skipping the rest of bundle item file scan");
				break;
			}

			return runtimesFoundAndComplete;
		}
	}
}
