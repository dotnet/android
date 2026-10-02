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
			int length      = pathExts?.Length ?? 0;
			FileExtensions  = new string [length * 2 + 1];
			for (int i = 0, j = 0; i < length; i++, j += 2) {
				FileExtensions [j] = pathExts [i].ToLowerInvariant ();
				FileExtensions [j + 1] = pathExts [i];
			}
			FileExtensions [FileExtensions.Length - 1] = null;
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

		static  readonly    Regex           VersionMatch    = new Regex (@"(?<version>\d+(\.\d+(\.\d+(\.\d+)?)?)?)");

		Version GetCurrentVersion ()
		{
			var command = GetHostProperty (Program, "CurrentVersionCommand", HostOS, HostOSName)
				?? Program.ItemSpec + " --version";

			return GetProgramVersion (HostOS, command);
		}

		internal static Version GetProgramVersion (string hostOS, string command)
		{
			bool windows = string.Equals (hostOS, "Windows", StringComparison.OrdinalIgnoreCase);
			var psi = new ProcessStartInfo (windows ? "cmd.exe" : "/bin/sh") {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				InheritedHandles = [],
			};
			if (windows) {
				psi.Arguments = $"/c \"{command}\"";
			} else {
				psi.ArgumentList.Add ("-c");
				psi.ArgumentList.Add (command);
			}
			var result = Process.RunAndCaptureText (psi, TimeSpan.FromSeconds (30));
			Console.Error.Write (result.StandardError);
			if (result.ExitStatus.Canceled) {
				throw new TimeoutException ($"Version command '{command}' did not complete within 30 seconds.");
			}
			if (result.ExitStatus.ExitCode != 0) {
				throw new InvalidOperationException ($"Version command '{command}' failed with exit code {result.ExitStatus.ExitCode}.");
			}

			using var reader = new StringReader (result.StandardOutput);
			string line;
			while ((line = reader.ReadLine ()) != null) {
				var match = VersionMatch.Match (line);
				if (!match.Success)
					continue;
				var version = match.Groups ["version"].Value;
				return new Version (version.Contains (".") ? version : version + ".0");
			}
			return new Version ();
		}
	}
}
