using System;
using System.Diagnostics;
using System.IO;

namespace Xamarin.Android.Tools.VSWhere
{
	/// <summary>
	/// https://github.com/Microsoft/vswhere/wiki/Find-MSBuild
	/// </summary>
	public static class MSBuildLocator
	{
		static readonly string [] msbuildLocations = new [] {
			// VS 2019 & Higher
			Path.Combine ("MSBuild", "Current", "Bin", "MSBuild.exe"),
			// VS 2017
			Path.Combine ("MSBuild", "15.0", "Bin", "MSBuild.exe"),
		};

		public static VisualStudioInstance QueryLatest ()
		{
			var instance = new VisualStudioInstance ();
			var vsInstallDir = Environment.GetEnvironmentVariable ("VSINSTALLDIR");
			if (string.IsNullOrEmpty (vsInstallDir)) {
				var programFiles = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
				var vswhere = Path.Combine (programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");
				if (!File.Exists (vswhere))
					throw new FileNotFoundException ("Cannot find vswhere.exe!", vswhere);
				instance.VisualStudioRootPath = Exec (vswhere, "-prerelease -latest -property installationPath");
				if (string.IsNullOrEmpty (instance.VisualStudioRootPath)) {
					throw new DirectoryNotFoundException ($"vswhere.exe was unable to locate any Visual Studio instances!");
				}
				if (!Directory.Exists (instance.VisualStudioRootPath)) {
					throw new DirectoryNotFoundException ($"vswhere.exe result returned a directory that did not exist: {instance.VisualStudioRootPath}");
				}
			} else {
				instance.VisualStudioRootPath = vsInstallDir;
			}

			foreach (var path in msbuildLocations) {
				instance.MSBuildPath = Path.Combine (instance.VisualStudioRootPath, path);
				if (File.Exists (instance.MSBuildPath))
					return instance;
			}

			throw new FileNotFoundException ("Cannot find MSBuild.exe!");
		}

		static string Exec (string fileName, string args)
		{
			var info = new ProcessStartInfo {
				FileName = fileName,
				WorkingDirectory = Path.GetDirectoryName (fileName),
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};
			using (var p = Process.Start (info)) {
				p?.WaitForExit ();
				return p?.StandardOutput.ReadToEnd ().Trim () ?? String.Empty;
			}
		}
	}
}
