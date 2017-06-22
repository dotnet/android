using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Xamarin.Android.Tools
{
	class MonoDroidSdkWindows : MonoDroidSdkBase
	{
		protected override string FindRuntime ()
		{
			string monoAndroidPath = Environment.GetEnvironmentVariable ("MONO_ANDROID_PATH");
			if (!string.IsNullOrEmpty (monoAndroidPath)) {
				if (Directory.Exists (monoAndroidPath) && ValidateRuntime (monoAndroidPath))
					return monoAndroidPath;
			}

			string xamarinSdk = Path.Combine (OS.ProgramFilesX86, "MSBuild", "Xamarin", "Android");
			if (Directory.Exists(xamarinSdk))
				return xamarinSdk;
			if (TryVSWhere(out xamarinSdk))
				return xamarinSdk;
			return OS.ProgramFilesX86 + @"\MSBuild\Novell";
		}

		bool TryVSWhere(out string xamarinSdk)
		{
			xamarinSdk = null;

			//Docs on this tool here: https://github.com/Microsoft/vswhere
			string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
			if (!File.Exists(vswhere))
				return false;

			//VS Workload ID here: https://docs.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-community
			var p = Process.Start(new ProcessStartInfo
			{
				FileName = vswhere,
				Arguments = "-latest -requires Component.Xamarin -property installationPath",
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
			});
			p.WaitForExit();

			if (p.ExitCode != 0)
				return false;

			xamarinSdk = Path.Combine(p.StandardOutput.ReadToEnd().Trim(), "MSBuild", "Xamarin", "Android");
			return true;
		}

		static readonly string[] RuntimeToFrameworkPaths = new []{
			Path.Combine ("..", "..", "..", "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework","MonoAndroid"),
			Path.Combine ("..", "..", "..", "Reference Assemblies", "Microsoft", "Framework", "MonoAndroid"),
			Path.Combine (OS.ProgramFilesX86, "Reference Assemblies", "Microsoft", "Framework", "MonoAndroid"),
		};

		protected override string FindFramework (string runtimePath)
		{
			foreach (var relativePath in RuntimeToFrameworkPaths) {
				var fullPath = Path.GetFullPath (Path.Combine (runtimePath, relativePath));
				if (Directory.Exists (fullPath)) {
					if (ValidateFramework (fullPath))
						return fullPath;

					// check to see if full path is the folder that contains each framework version, eg contains folders of the form v1.0, v2.3 etc
					var subdirs = Directory.GetDirectories (fullPath, "v*").OrderBy (x => x).ToArray ();
					foreach (var subdir in subdirs) {
						if (ValidateFramework (subdir))
							return subdir;
					}
				}
			}

			return null;
		}

		protected override string FindBin (string runtimePath)
		{
			return runtimePath;
		}

		protected override bool ValidateBin (string binPath)
		{
			return !string.IsNullOrWhiteSpace (binPath) &&
				File.Exists (Path.Combine (binPath, "generator.exe"));
		}

		protected override string FindInclude (string runtimePath)
		{
			return Path.GetFullPath (Path.Combine (runtimePath, "include"));
		}

		protected override string FindLibraries (string runtimePath)
		{
			return Path.GetFullPath (runtimePath);
		}

		protected override IEnumerable<string> GetVersionFileLocations ()
		{
			yield return Path.GetFullPath (Path.Combine (RuntimePath, "Version"));
		}
	}
}

