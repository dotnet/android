using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class Which : Task
	{
		[Required]
		public  ITaskItem           Program             { get; set; }

		public  ITaskItem[]         Directories         { get; set; }

		public  bool                Required            { get; set; }

		public  string              HostOS              { get; set; }
		public  string              HostOSName          { get; set; }

		[Output]
		public  ITaskItem           Location            { get; set; }

		static  readonly    string[]    FileExtensions;

		static Which ()
		{
			var pathExt     = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts    = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
			FileExtensions  = new string [(pathExts?.Length ?? 0) + 1];
			FileExtensions [0] = null;
			if (pathExts != null) {
				Array.Copy (pathExts, 0, FileExtensions, 1, pathExts.Length);
			}
		}

		public static string GetProgramLocation (string programBasename, out string filename, string[] directories = null)
		{
			directories = directories ?? GetPathDirectories ();
			foreach (var d in directories) {
				var p   = GetProgramLocation (programBasename, d, out filename);
				if (p != null)
					return p;
			}
			filename    = programBasename;
			return null;
		}

		static string GetProgramLocation (string programBasename, string directory, out string filename)
		{
			foreach (var ext in FileExtensions) {
				filename    = Path.ChangeExtension (programBasename, ext);
				var p       = Path.Combine (directory, filename);
				if (File.Exists (p)) {
					return p;
				}
			}
			filename    = programBasename;
			return null;
		}

		static string[] GetPathDirectories ()
		{
			return Environment.GetEnvironmentVariable ("PATH")
				.Split (Path.PathSeparator);
		}

		internal static string GetHostProperty (ITaskItem program, string property, string hostOS, string hostOSName)
		{
			var value = program.GetMetadata (hostOSName + property);
			if (string.IsNullOrEmpty (value)) {
				value = program.GetMetadata (hostOS + property);
			}
			if (string.IsNullOrEmpty (value)) {
				value = program.GetMetadata (property);
			}
			return string.IsNullOrEmpty (value) ? null : value;
		}

		public override bool Execute ()
		{
			string[]    paths   = Directories?.Select (d => d.ItemSpec).ToArray ();
			if (paths == null || paths.Length == 0) {
				paths   = GetPathDirectories ();
			}

			Log.LogMessage (MessageImportance.Low, $"Task {nameof (Which)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOS)}: {HostOS}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOSName)}: {HostOSName}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Program)}: {Program}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Directories)}:");
			foreach (var p in paths) {
				Log.LogMessage (MessageImportance.Low, $"    {p}");
			}
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Required)}: {Required}");

			string _;
			var e = GetProgramLocation (Program.ItemSpec, out _, paths);
			if (e != null && !NeedInstall ()) {
				Location = new TaskItem (e);
			}

			if (Location == null && Required) {
				Log.LogError ("Could not find required program '{0}'.", Program.ItemSpec);
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Location)}: {Location?.ItemSpec}");

			return !Log.HasLoggedErrors;
		}

		bool NeedInstall ()
		{
			var min = Program.GetMetadata ("MinimumVersion");
			var max = Program.GetMetadata ("MaximumVersion");

			if (string.IsNullOrEmpty (min) && string.IsNullOrEmpty (max)) {
				return false;
			}

			var zero        = new Version ();
			var minVersion  = string.IsNullOrEmpty (min) ? zero : new Version (min);
			var maxVersion  = string.IsNullOrEmpty (max) ? zero : new Version (max);
			var curVersion  = GetCurrentVersion ();

			Log.LogMessage (MessageImportance.Low, $"Checking '{Program.ItemSpec}' version: Minimum: {minVersion}; Maximum: {maxVersion}; Current: {curVersion}");

			if (minVersion == zero) {
				return curVersion > maxVersion;
			}
			if (curVersion < minVersion)
				return true;
			if (maxVersion == zero)
				return false;
			return curVersion > maxVersion;
		}

		static  readonly    Regex           VersionMatch    = new Regex (@"(?<version>\d+\.\d+(\.\d+(\.\d+)?)?)");

		Version GetCurrentVersion ()
		{
			var command = GetHostProperty (Program, "CurrentVersionCommand", HostOS, HostOSName)
				?? Program.ItemSpec + " --version";

			return GetProgramVersion (HostOS, command);
		}

		internal static Version GetProgramVersion (string hostOS, string command)
		{
			string shell, format;
			GetShell (hostOS, out shell, out format);

			var psi = new ProcessStartInfo (shell, string.Format (format, command)) {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
			};
			string curVersion = null;
			using (var p = new Process { StartInfo = psi }) {
				p.OutputDataReceived += (sender, e) => {
					if (string.IsNullOrEmpty (e.Data) || curVersion != null)
						return;
					var m = VersionMatch.Match (e.Data);
					if (!m.Success)
						return;
					curVersion = m.Groups ["version"].Value;
				};
				p.Start ();
				p.BeginOutputReadLine ();
				p.WaitForExit ();
			}
			return curVersion == null
				? new Version ()
				: new Version (curVersion);
		}

		static void GetShell (string hostOS, out string shell, out string format)
		{
			if (string.Equals (hostOS, "Windows", StringComparison.OrdinalIgnoreCase)) {
				shell = "cmd.exe";
				format = "/c \"{0}\"";
				return;
			}
			shell = "/bin/sh";
			format = "-c \"{0}\"";
		}
	}
}

