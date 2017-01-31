using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools
{
	class MonoDroidSdkWindows : MonoDroidSdkBase
	{
		protected override string FindRuntime ()
		{
			string monoAndroidPath = Environment.GetEnvironmentVariable ("MONO_ANDROID_PATH");
			if (!string.IsNullOrEmpty (monoAndroidPath)) {
				string libMandroid = Path.Combine (monoAndroidPath, "lib", "mandroid");
				if (Directory.Exists (libMandroid) && ValidateRuntime (libMandroid))
					return libMandroid;
				if (Directory.Exists (monoAndroidPath) && ValidateRuntime (monoAndroidPath))
					return monoAndroidPath;
			}
			string xamarinSdk = Path.Combine (OS.ProgramFilesX86, "MSBuild", "Xamarin", "Android");
			return Directory.Exists (xamarinSdk)
				? xamarinSdk
					: OS.ProgramFilesX86 + @"\MSBuild\Novell";
		}

		static readonly string[] RuntimeToFrameworkPaths = new []{
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

