using System;
using System.Diagnostics;
using System.IO;

namespace Xamarin.Android.Tools.VSWhere
{
	/// <summary>
	/// https://github.com/Microsoft/vswhere/wiki/Find-MSBuild
	/// </summary>
	public class MSBuildLocator
	{
		static readonly string [] msbuildLocations = new [] {
			// VS 2019 & Higher
			Path.Combine ("MSBuild", "Current", "Bin", "MSBuild.exe"),
			// VS 2017
			Path.Combine ("MSBuild", "15.0", "Bin", "MSBuild.exe"),
		};

		/// <summary>
		/// Adds the -prerelease flag, defaults to false
		/// </summary>
		public bool IncludePrerelease { get; set; } = false;

		public string VisualStudioDirectory { get; private set; }

		public string MSBuildPath { get; private set; }

		public void Locate ()
		{
			var vsInstallDir = Environment.GetEnvironmentVariable ("VSINSTALLDIR");
			if (string.IsNullOrEmpty (vsInstallDir)) {
				var programFiles = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
				var vswhere = Path.Combine (programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");
				if (!File.Exists (vswhere))
					throw new FileNotFoundException ("Cannot find vswhere.exe!", vswhere);

				string prerelease = IncludePrerelease ? "-prerelease" : "";
				VisualStudioDirectory = Exec (vswhere, $"-latest {prerelease} -products * -requires Microsoft.Component.MSBuild -property installationPath");
				if (!Directory.Exists (VisualStudioDirectory)) {
					throw new Exception ($"vswhere.exe result returned a directory that did not exist: {VisualStudioDirectory}");
				}
			} else {
				VisualStudioDirectory = vsInstallDir;
			}

			foreach (var path in msbuildLocations) {
				MSBuildPath = Path.Combine (VisualStudioDirectory, path);
				if (File.Exists (MSBuildPath))
					return;
			}

			throw new FileNotFoundException ("Cannot find MSBuild.exe!");
		}

		string Exec (string fileName, string args)
		{
			var info = new ProcessStartInfo {
				FileName = fileName,
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};
			using (var p = Process.Start (info)) {
				p.WaitForExit ();
				return p.StandardOutput.ReadToEnd ().Trim ();
			}
		}
	}
}
