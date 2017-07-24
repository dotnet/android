using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Build.Utilities
{
	class MonoDroidSdkWindows : MonoDroidSdkBase
	{
		protected override string FindRuntime ()
		{
			var r = base.FindRuntime ();
			if (r != null)
				return r;
			var paths = new []{
				Path.GetFullPath (Path.GetDirectoryName (GetType ().Assembly.Location)),
				Path.Combine (OS.ProgramFilesX86, "MSBuild", "Xamarin", "Android"),
				Path.Combine (OS.ProgramFilesX86, "MSBuild", "Novell"),
			};
			return paths.FirstOrDefault (p => ValidateRuntime (p));
		}

		static readonly string[] RuntimeToFrameworkPaths = new []{
			// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android/
			Path.Combine ("..", "..", "..", "xbuild-frameworks", "MonoAndroid"),
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

		protected override string FindLibraries (string runtimePath)
		{
			return Path.GetFullPath (runtimePath);
		}
	}
}

