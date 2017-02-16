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

		string Sudo {
			get {return (UseSudo && HostOS != "Windows") ? "sudo " : "";}
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (PrepareInstall)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOS)}: {HostOS}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HostOSName)}: {HostOSName}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Program)}: {Program}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (UseSudo)}: {UseSudo}");

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
			if (HostOS == "Darwin") {
				var brew    = Program.GetMetadata ("Homebrew");
				if (!string.IsNullOrEmpty (brew)) {
					InstallCommand = $"{Sudo}brew install '{brew}'";
				}
				return;
			}
			// TODO: other platforms
			Log.LogError ($"Unsupported platform {HostOS}!");
		}
	}
}

