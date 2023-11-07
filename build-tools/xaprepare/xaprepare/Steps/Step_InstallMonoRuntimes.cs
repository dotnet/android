using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	class Step_InstallMonoRuntimes : StepWithDownloadProgress
	{
		const string StatusIndent    = "  ";
		const string SubStatusIndent = "    ";

		Runtimes?     allRuntimes;

		public Step_InstallMonoRuntimes ()
			: base ("Installing Mono runtimes")
		{
		}

		protected override async Task<bool> Execute (Context context)
		{
			List<Runtime> enabledRuntimes = GetEnabledRuntimes (enableLogging: false);
			if (enabledRuntimes.Count == 0) {
				Log.StatusLine ("No runtimes to build/install");
				return true;
			}

			if (!context.MonoArchiveDownloaded) {
				// https://github.com/xamarin/xamarin-android/pull/3816
				throw new NotImplementedException ("Unable to build mono runtimes from sources.");
			}

			Log.StatusLine ("Checking if all runtime files are present");
			var allRuntimes = new Runtimes ();
			if (MonoRuntimesHelpers.AreRuntimeItemsInstalled (context, allRuntimes)) {

				// User might have changed the set of ABIs to build, we need to check and rebuild if necessary
				if (!Utilities.AbiChoiceChanged (context)) {
					Log.StatusLine ("Mono runtimes already present and complete. No need to download or build.");
					return true;
				}

				Log.StatusLine ("Mono already present, but the choice of ABIs changed since previous build, runtime refresh is necessary");
			}
			Log.Instance.StatusLine ($"  {Context.Instance.Characters.Bullet} some files are missing, rebuild/reinstall forced");

			CleanupBeforeInstall ();
			Log.StatusLine ();

			string managedRuntime = context.Properties.GetRequiredValue (KnownProperties.ManagedRuntime);
			bool haveManagedRuntime = !String.IsNullOrEmpty (managedRuntime);
			//if (!await ConjureXamarinCecilAndRemapRef (context, haveManagedRuntime, managedRuntime))
			//	return false;

			if (!await InstallRuntimes (context, enabledRuntimes))
				return false;

			if (!InstallBCL (context))
				return false;

			if (!InstallUtilities (context, haveManagedRuntime, managedRuntime))
				return false;

			//Utilities.PropagateXamarinAndroidCecil (context);

			return true;
		}

		void EnsureAllRuntimes ()
		{
			if (allRuntimes == null)
				throw new InvalidOperationException ("Step not initialized properly, allRuntimes is not set");
		}

		void CleanupBeforeInstall ()
		{
			EnsureAllRuntimes ();
			foreach (string dir in allRuntimes!.OutputDirectories) {
				Utilities.DeleteDirectorySilent (dir);
			}
		}

		/*async Task<bool> ConjureXamarinCecilAndRemapRef (Context context, bool haveManagedRuntime, string managedRuntime)
		{
			StatusStep (context, "Building remap-assembly-ref");
			bool result = await Utilities.BuildRemapRef (context, haveManagedRuntime, managedRuntime, quiet: true);
			if (!result)
				return false;

			var msbuild = new MSBuildRunner (context);
			StatusStep (context, "Building conjure-xamarin-android-cecil");
			string projectPath = Path.Combine (Configurables.Paths.BuildToolsDir, "conjure-xamarin-android-cecil", "conjure-xamarin-android-cecil.csproj");
			result = await msbuild.Run (
				projectPath: projectPath,
				logTag: "conjure-xamarin-android-cecil",
				binlogName: "build-conjure-xamarin-android-cecil"
			);

			if (!result) {
				Log.ErrorLine ("Failed to build conjure-xamarin-android-cecil");
				return false;
			}

			StatusStep (context, "Conjuring Xamarin.Android.Cecil and Xamari.Android.Cecil.Mdb");
			string conjurer = Path.Combine (Configurables.Paths.BuildBinDir, "conjure-xamarin-android-cecil.dll");

			result = Utilities.RunManagedCommand (
				conjurer, // command
				BuildPaths.XamarinAndroidSourceRoot, // workingDirectory
				true, // ignoreEmptyArguments

				// arguments
				Configurables.Paths.MonoProfileToolsDir, // source dir
				Configurables.Paths.BuildBinDir // destination dir
			);

			StatusStep (context, "Re-signing Xamarin.Android.Cecil.dll");
			var sn = new SnRunner (context);
			string snkPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "mono.snk");
			string assemblyPath = Path.Combine (Configurables.Paths.BuildBinDir, "Xamarin.Android.Cecil.dll");
			result = await sn.ReSign (snkPath, assemblyPath, $"sign-xamarin-android-cecil");
			if (!result) {
				Log.ErrorLine ("Failed to re-sign Xamarin.Android.Cecil.dll");
				return false;
			}
			var classicInstallMSBuildDir =  Path.Combine (Configurables.Paths.XAInstallPrefix, "xbuild", "Xamarin", "Android");
			Utilities.CreateDirectory (classicInstallMSBuildDir);
			Utilities.CopyFile (assemblyPath, Path.Combine (classicInstallMSBuildDir, "Xamarin.Android.Cecil.dll"));

			StatusStep (context, "Re-signing Xamarin.Android.Cecil.Mdb.dll");
			assemblyPath = Path.Combine (Configurables.Paths.BuildBinDir, "Xamarin.Android.Cecil.Mdb.dll");
			result = await sn.ReSign (snkPath, assemblyPath, $"sign-xamarin-android-cecil-mdb");
			if (!result) {
				Log.ErrorLine ("Failed to re-sign Xamarin.Android.Cecil.Mdb.dll");
				return false;
			}

			Utilities.CopyFile (assemblyPath, Path.Combine (classicInstallMSBuildDir, "Xamarin.Android.Cecil.Mdb.dll"));

			return true;
		}
		*/

		bool InstallUtilities (Context context, bool haveManagedRuntime, string managedRuntime)
		{
			string destDir = MonoRuntimesHelpers.UtilitiesDestinationDir;

			Utilities.CreateDirectory (destDir);

			//string remapper = Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, context.Properties.GetRequiredValue (KnownProperties.RemapAssemblyRefToolExecutable));
			//string targetCecil = Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, Path.Combine (Configurables.Paths.BuildBinDir, "Xamarin.Android.Cecil.dll"));

			StatusStep (context, "Installing runtime utilities");
			EnsureAllRuntimes ();
			foreach (MonoUtilityFile muf in allRuntimes!.UtilityFilesToInstall) {
				(string destFilePath, string debugSymbolsDestPath) = MonoRuntimesHelpers.GetDestinationPaths (muf);
				Utilities.CopyFile (muf.SourcePath, destFilePath);
				if (!muf.IgnoreDebugInfo) {
					if (!String.IsNullOrEmpty (debugSymbolsDestPath)) {
						Utilities.CopyFile (muf.DebugSymbolsPath, debugSymbolsDestPath);
					} else {
						Log.DebugLine ($"Debug symbols not found for utility file {Path.GetFileName (muf.SourcePath)}");
					}
				}
				/*
				if (!muf.RemapCecil)
					continue;

				string relDestFilePath = Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, destFilePath);
				StatusSubStep (context, $"Remapping Cecil references for {relDestFilePath}");
				bool result = Utilities.RunManagedCommand (
					remapper, // command
					BuildPaths.XamarinAndroidSourceRoot, // workingDirectory
					true, // ignoreEmptyArguments

					// arguments
					Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, muf.SourcePath),
					relDestFilePath,
					"Mono.Cecil",
					targetCecil);

				if (result)
					continue;

				Log.ErrorLine ($"Failed to remap cecil reference for {destFilePath}");
				return false;
				*/
			}

			return true;
		}

		bool GenerateFrameworkList (Context contex, string filePath, string bclDir, string facadesDir)
		{
			Log.DebugLine ($"Generating {filePath}");

			EnsureAllRuntimes ();
			var contents = new XElement (
				"FileList",
				new XAttribute ("Redist", Runtimes.FrameworkListRedist),
				new XAttribute ("Name", Runtimes.FrameworkListName),
				allRuntimes!.BclFilesToInstall.Where (f => f.Type == BclFileType.FacadeAssembly || f.Type == BclFileType.ProfileAssembly).Select (f => ToFileElement (f))
			);
			contents.Save (filePath);
			return true;

			XElement ToFileElement (BclFile bcf)
			{
				Log.Debug ("Writing ");
				string fullFilePath;

				switch (bcf.Type) {
					case BclFileType.ProfileAssembly:
						fullFilePath = Path.Combine (bclDir, bcf.Name);
						Log.Debug ("profile");
						break;

					case BclFileType.FacadeAssembly:
						Log.Debug ("facade");
						fullFilePath = Path.Combine (facadesDir, bcf.Name);
						break;

					default:
						Log.Debug ("unsupported");
						fullFilePath = String.Empty;
						break;
				}

				Log.DebugLine ($" BCL assembly {bcf.Name}");
				if (String.IsNullOrEmpty (fullFilePath))
					throw new InvalidOperationException ($"Unsupported BCL file type {bcf.Type}");

				AssemblyName aname = AssemblyName.GetAssemblyName (fullFilePath);
				string? version = bcf.Version ?? String.Empty;
				if (String.IsNullOrEmpty (version) && !Runtimes.FrameworkListVersionOverrides.TryGetValue (bcf.Name, out version) || version == null)
					version = aname.Version?.ToString () ?? "0.0.0";

				return new XElement (
					"File",
					new XAttribute ("AssemblyName", aname.Name ?? "Unknown"),
					new XAttribute ("Version", version),
					new XAttribute ("PublicKeyToken", String.Join (String.Empty, aname.GetPublicKeyToken ()?.Select (b => b.ToString ("x2")) ?? new string[]{})),
					new XAttribute ("ProcessorArchitecture", aname.ProcessorArchitecture.ToString ())
				);
			}
		}

		bool InstallBCL (Context context)
		{
			string redistListDir = MonoRuntimesHelpers.BCLRedistListDestinationDir;

			foreach (KeyValuePair<BclFileTarget, string> kvp in MonoRuntimesHelpers.BCLDestinationDirs) {
				Utilities.CreateDirectory (kvp.Value);
			}

			foreach (KeyValuePair<BclFileTarget, string> kvp in MonoRuntimesHelpers.BCLFacadesDestinationDirs) {
				Utilities.CreateDirectory (kvp.Value);
			}

			Utilities.CreateDirectory (redistListDir);

			StatusStep (context, "Installing Android BCL assemblies");
			EnsureAllRuntimes ();
			InstallBCLFiles (allRuntimes!.BclFilesToInstall);

			StatusStep (context, "Installing Designer Host BCL assemblies");
			InstallBCLFiles (allRuntimes.DesignerHostBclFilesToInstall);

			StatusStep (context, "Installing Designer Windows BCL assemblies");
			InstallBCLFiles (allRuntimes.DesignerWindowsBclFilesToInstall);

			return GenerateFrameworkList (
				context,
				MonoRuntimesHelpers.FrameworkListPath,
				MonoRuntimesHelpers.BCLDestinationDirs [BclFileTarget.Android],
				MonoRuntimesHelpers.BCLFacadesDestinationDirs [BclFileTarget.Android]
			);
		}

		void InstallBCLFiles (List<BclFile> files)
		{
			foreach (BclFile bf in files) {
				(string destFilePath, string debugSymbolsDestPath) = MonoRuntimesHelpers.GetDestinationPaths (bf);

				Utilities.CopyFile (bf.SourcePath, destFilePath);
				if (!bf.ExcludeDebugSymbols && !String.IsNullOrEmpty (bf.DebugSymbolsPath) && debugSymbolsDestPath.Length > 0)
					Utilities.CopyFile (bf.DebugSymbolsPath!, debugSymbolsDestPath);
			}
		}

		async Task<bool> InstallRuntimes (Context context, List<Runtime> enabledRuntimes)
		{
			StatusStep (context, "Installing tests");
			EnsureAllRuntimes ();
			foreach (TestAssembly tasm in allRuntimes!.TestAssemblies) {
				string sourceBasePath;

				switch (tasm.TestType) {
					case TestAssemblyType.Reference:
					case TestAssemblyType.TestRunner:
						sourceBasePath = Path.Combine (Configurables.Paths.MonoProfileDir);
						break;

					case TestAssemblyType.XUnit:
					case TestAssemblyType.NUnit:
					case TestAssemblyType.Satellite:
						sourceBasePath = Configurables.Paths.BCLTestsSourceDir;
						break;

					default:
						throw new InvalidOperationException ($"Unsupported test assembly type: {tasm.TestType}");
				}

				(string destFilePath, string debugSymbolsDestPath) = MonoRuntimesHelpers.GetDestinationPaths (tasm);
				CopyFile (Path.Combine (sourceBasePath, tasm.Name), destFilePath);
				if (debugSymbolsDestPath.Length > 0)
					CopyFile (Path.Combine (sourceBasePath, Utilities.GetDebugSymbolsPath (tasm.Name)), debugSymbolsDestPath);
			}

			StatusSubStep (context, "Creating BCL tests archive");
			Utilities.DeleteFileSilent (MonoRuntimesHelpers.BCLTestsArchivePath);
			var sevenZip = new SevenZipRunner (context);
			if (!await sevenZip.Zip (MonoRuntimesHelpers.BCLTestsArchivePath, MonoRuntimesHelpers.BCLTestsDestinationDir, new List<string> { "." })) {
				Log.ErrorLine ("BCL tests archive creation failed, see the log files for details.");
				return false;
			}

			StatusStep (context, "Installing runtimes");
			foreach (Runtime runtime in enabledRuntimes) {
				StatusSubStep (context, $"Installing {runtime.Flavor} runtime {runtime.DisplayName}");

				string src, dst;
				bool skipFile;
				foreach (RuntimeFile rtf in allRuntimes.RuntimeFilesToInstall) {
					if (rtf.Shared && rtf.AlreadyCopied)
						continue;

					(skipFile, src, dst) = MonoRuntimesHelpers.GetRuntimeFilePaths (runtime, rtf);
					if (skipFile || src.Length == 0 || dst.Length == 0)
						continue;

					CopyFile (src, dst);
					if (!StripFile (runtime, rtf, dst))
						return false;

					if (rtf.Shared)
						rtf.AlreadyCopied = true;
				}
			}

			return true;

			bool StripFile (Runtime runtime, RuntimeFile rtf, string filePath)
			{
				if (rtf.Type != RuntimeFileType.StrippableBinary)
					return true;

				var monoRuntime = runtime.As<MonoRuntime> ();
				if (monoRuntime == null || !monoRuntime.CanStripNativeLibrary || !rtf.Strip)
					return true;

				if (String.IsNullOrEmpty (monoRuntime.Strip)) {
					Log.WarningLine ($"Binary stripping impossible, runtime {monoRuntime.DisplayName} doesn't define the strip command");
					return true;
				}

				if (context.OS.IsWindows && (context.IsWindowsCrossAotAbi (monoRuntime.Name) || context.IsMingwHostAbi (monoRuntime.Name))) {
					Log.WarningLine ($"Unable to strip '{monoRuntime.DisplayName}' on Windows.");
					return true;
				}

				bool result;
				if (!String.IsNullOrEmpty (monoRuntime.StripFlags))
					result = Utilities.RunCommand (monoRuntime.Strip, monoRuntime.StripFlags, filePath);
				else
					result = Utilities.RunCommand (monoRuntime.Strip, filePath);

				if (result)
					return true;

				Log.ErrorLine ($"Failed to {monoRuntime.Strip} the binary file {filePath}, see logs for error details");
				return false;
			}

			void CopyFile (string src, string dest)
			{
				if (!CheckFileExists (src, true))
					return;

				Utilities.CopyFile (src, dest);
			}
		}

		List<Runtime> GetEnabledRuntimes (bool enableLogging)
		{
			var enabledRuntimes = new List<Runtime> ();

			if (allRuntimes == null)
				allRuntimes = new Runtimes ();
			return MonoRuntimesHelpers.GetEnabledRuntimes (allRuntimes, enableLogging);
		}

		void StatusMessage (Context context, string indent, string message)
		{
			Log.StatusLine ($"{indent}{context.Characters.Bullet} {message}");
		}

		void StatusStep (Context context, string name)
		{
			StatusMessage (context, StatusIndent, name);
		}

		void StatusSubStep (Context context, string name)
		{
			StatusMessage (context, SubStatusIndent, name);
		}

		bool CheckFileExists (string filePath, bool required)
		{
			if (File.Exists (filePath))
				return true;

			if (required)
				throw new InvalidOperationException ($"Required file not found: {filePath}");

			return false;
		}
	}
}
