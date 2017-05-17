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
	public class PrepareInstall : Task
	{
		[Required]
		public  ITaskItem           Program             { get; set; }

		public  bool                UseSudo             { get; set; }
		public  string              HostOS              { get; set; }
		public  string              HostOSName          { get; set; }

		[Output]
		public  string              InstallCommand      { get; set; }

		[Output]
		public  ITaskItem           DownloadUrl         { get; set; }

		string Sudo;
		string SudoBrew;

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (PrepareInstall)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOS)}: {HostOS}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOSName)}: {HostOSName}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Program)}: {Program}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (UseSudo)}: {UseSudo}");

			SetSudo ();
			SetDownloadUrl ();
			SetInstallCommand ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (DownloadUrl)}: {DownloadUrl} [Url: {DownloadUrl?.GetMetadata ("Url")}]");
			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (InstallCommand)}: {InstallCommand}");

			return !Log.HasLoggedErrors;
		}

		string GetHostProperty (string property)
		{
			return Which.GetHostProperty (Program, property, HostOS, HostOSName);
		}

		void SetSudo ()
		{
			if (!UseSudo || string.Equals ("Windows", HostOS, StringComparison.OrdinalIgnoreCase))
				return;

			Sudo    = "sudo ";

			if (!string.Equals ("Darwin", HostOS, StringComparison.OrdinalIgnoreCase)) {
				return;
			}
			string brewFilename;
			var brewPath = Which.GetProgramLocation ("brew", out brewFilename);
			if (string.IsNullOrEmpty (brewPath)) {
				return;
			}
			var brewVersion	= Which.GetProgramVersion (HostOS, $"{brewPath} --version");
			if (brewVersion < new Version (1, 1)) {
				SudoBrew    = "sudo ";
				return;
			}
		}

		void SetDownloadUrl ()
		{
			var minUrl = GetHostProperty ("MinimumUrl");
			if (string.IsNullOrEmpty (minUrl))
				return;

			DownloadUrl = new TaskItem (GetFilenameFromUrl (minUrl));
			DownloadUrl.SetMetadata ("Url", minUrl);
		}

		string GetFilenameFromUrl (string url)
		{
			var u   = new Uri (url);
			var p   = u.AbsolutePath;
			var s   = p.LastIndexOf ('/');
			if (s >= 0 && p.Length > (s+1)) {
				return p.Substring (s+1);
			}
			return Program.ItemSpec + ".bin";
		}

		void SetInstallCommand ()
		{
			var install = GetHostProperty ("Install");
			if (install != null) {
				InstallCommand = Sudo + install;
				return;
			}
			if (string.Equals (HostOS, "Darwin", StringComparison.OrdinalIgnoreCase)) {
				var brew    = Program.GetMetadata ("Homebrew");
				if (!string.IsNullOrEmpty (brew)) {
					InstallCommand = $"{SudoBrew}brew install '{brew}'";
				}
				return;
			}
			// TODO: other platforms
			var min = Program.GetMetadata ("MinimumVersion");
			var ver = "";
			if (!string.IsNullOrEmpty (min)) {
				ver = $", version >= {min}";
				var max = Program.GetMetadata ("MaximumVersion");
				if (!string.IsNullOrEmpty (max)) {
					ver += $" and <= {max}";
				}
			}
			Log.LogError ($"Missing dependency detected. For {HostOS} we do not know how to install program `{Program.ItemSpec}`{ver}.");
		}
	}
}

