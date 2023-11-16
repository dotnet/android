using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Xamarin.Android.Tools.VSWhere;

namespace Xamarin.Android.Prepare
{
	partial class Windows : OS
	{
		readonly List <string> executableExtensions;
		readonly VisualStudioInstance vsInstance;

		public override string Type { get; } = "Windows";
		public override List<Program> Dependencies { get; } = new List<Program> ();
		public override StringComparison DefaultStringComparison => StringComparison.OrdinalIgnoreCase;
		public override StringComparer DefaultStringComparer => StringComparer.OrdinalIgnoreCase;

		protected override List<string> ExecutableExtensions => executableExtensions;
		public override bool IsWindows => true;
		public override bool IsUnix => false;
		public override string HomeDirectory => GetHomeDir ();

		public Windows (Context context) : base (context)
		{
			string[]? pathext = Environment.GetEnvironmentVariable ("PATHEXT")?.Split (new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			if (pathext == null || pathext.Length == 0) {
				executableExtensions = new List<string> {
					".exe",
					".cmd",
					".bat"
				};
			} else {
				executableExtensions = new List<string> ();
				foreach (string ext in pathext) {
					executableExtensions.Add (ext.ToLowerInvariant ());
				}
			}

			vsInstance = MSBuildLocator.QueryLatest ();
			Log.DebugLine ($"Visual Studio detected in {vsInstance.VisualStudioRootPath}");
			Log.DebugLine ($"MSBuild detected at {vsInstance.MSBuildPath}");

			OSVersionInfo osinfo = OSVersionInfo.GetOSVersionInfo ();
			Flavor = osinfo.Name;
			Name = osinfo.FullName;
			Architecture = Is64Bit ? "x86" : "x86_64"; // Not good enough! (ARM)
			Release = $"{osinfo.Major}.{osinfo.Minor}.{osinfo.Build}";
			DiskInformation = DiskInfo ();
		}

		public override string Which (string programPath, bool required = true)
		{
			if (String.Compare ("7za", programPath, StringComparison.OrdinalIgnoreCase) == 0) {
				string? packagePath = Context.Instance.Properties.GetValue (KnownProperties.Pkg7Zip_CommandLine);
				if (String.IsNullOrEmpty (packagePath)) {
					packagePath = Path.Combine (Configurables.Paths.XAPackagesDir, "7-zip.commandline", "18.1.0");
				}
				packagePath = Path.Combine (packagePath, "tools");
				if (Is64Bit)
					packagePath = Path.Combine (packagePath, "x64");
				return Path.Combine (packagePath, "7za.exe");
			}

			if (String.Compare ("msbuild", programPath, StringComparison.OrdinalIgnoreCase) == 0) {
				return vsInstance.MSBuildPath;
			}

			if (String.Compare ("sn", programPath, StringComparison.OrdinalIgnoreCase) == 0) {
				var netFXToolsPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Microsoft SDKs", "Windows", "v10.0A", "bin");
				var latestOrDefaultSn = Directory.EnumerateFiles (netFXToolsPath, "sn.exe", SearchOption.AllDirectories).LastOrDefault ();
				return latestOrDefaultSn != null && File.Exists (latestOrDefaultSn) ? latestOrDefaultSn : base.Which (programPath, required);
			}

			return base.Which (programPath, required);
		}

		public override string AppendExecutableExtension (string programName)
		{
			string ext = Path.GetExtension (programName);
			if (String.Compare (".exe", ext, StringComparison.OrdinalIgnoreCase) == 0) {
				return programName;
			}

			return $"{programName}.exe";
		}

		protected override string AssertIsExecutable (string fullPath)
		{
			return fullPath;
		}

		public override string GetManagedProgramRunner (string programPath)
		{
			return String.Empty;
		}

		protected override bool InitOS ()
		{
			base.InitOS ();
			Log.Todo ("gather dependencies here");

			// This is required by Android SDK which uses a utility to locate Java on Windows
			// ($SDK_ROOT/tools/lib/find_java.bat) and that utility, in turn, looks at JAVA_HOME
			EnvironmentVariables ["JAVA_HOME"] = JavaHome;

			return true;
		}

		string GetHomeDir ()
		{
			string? homeDir = Environment.GetEnvironmentVariable ("USERPROFILE");
			if (!String.IsNullOrEmpty (homeDir))
				return homeDir;

			string? homeDrive = Environment.GetEnvironmentVariable ("HOMEDRIVE");
			if (String.IsNullOrEmpty (homeDrive))
				throw new InvalidOperationException ("Unable to determine user's home directory, missing HOMEDRIVE environment variable");

			homeDir = Environment.GetEnvironmentVariable ("HOMEPATH");
			if (String.IsNullOrEmpty (homeDir))
				throw new InvalidOperationException ("Unable to determine user's home directory, missing HOMEPATH environment variable");

			return $"{homeDrive}{homeDir}";
		}

		string DiskInfo ()
		{
			var sb = new StringBuilder ();
			sb.AppendLine ("Drive\tTotalSize\tAvailableFreeSpace");
			foreach (DriveInfo d in DriveInfo.GetDrives ()) {
				if (!d.IsReady)
					continue;
				sb.AppendLine ($"{d.Name}\t{Utilities.SizeToString (d.TotalSize)}\t{Utilities.SizeToString (d.AvailableFreeSpace)}");
			}
			return sb.ToString ();
		}
	};
}
