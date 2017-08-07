using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Utilities
{
	class MonoDroidSdkUnix : MonoDroidSdkBase
	{
		readonly static string[] RuntimeToFrameworkPaths = new[]{
			// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android/
			Path.Combine ("..", "..", "..", "xbuild-frameworks", "MonoAndroid"),
			Path.Combine ("..", "..", "..", ".xamarin.android", "lib", "xbuild-frameworks", "MonoAndroid"),
			Path.Combine ("..", "xbuild-frameworks", "MonoAndroid"),
			Path.Combine ("..", "mono", "2.1"),
		};

		readonly static string[] SearchPaths = {
			"/Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/xamarin.android/xbuild/Xamarin/Android",
			"/Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/mandroid",
			"/Developer/MonoAndroid/usr/lib/mandroid",
			"/app/lib/mandroid",
			"/app/lib/xamarin.android/xbuild/Xamarin/Android",
			"/opt/mono-android/lib/mandroid",
			"/opt/mono-android/lib/xamarin.android/xbuild/Xamarin/Android",
		};

		protected override string FindRuntime ()
		{
			string monoAndroidPath = Environment.GetEnvironmentVariable ("MONO_ANDROID_PATH");
			if (!string.IsNullOrEmpty (monoAndroidPath)) {
				string msbuildDir = Path.Combine (monoAndroidPath, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
				if (Directory.Exists (msbuildDir)) {
					if (ValidateRuntime (msbuildDir))
						return msbuildDir;
					AndroidLogger.LogInfo (null, "MONO_ANDROID_PATH points to {0}, but it is invalid.", monoAndroidPath);
				} else
					AndroidLogger.LogInfo (null, "MONO_ANDROID_PATH points to {0}, but it does not exist.", monoAndroidPath);
			}

			// check also in the users folder
			var personal = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			var additionalSearchPaths = new [] {
				Path.GetFullPath (Path.GetDirectoryName (GetType ().Assembly.Location)),
				Path.Combine (personal, @".xamarin.android/lib/xamarin.android/xbuild/Xamarin/Android")
			};

			return additionalSearchPaths.Concat (SearchPaths).FirstOrDefault (ValidateRuntime);
		}

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
			return Path.GetFullPath (Path.Combine (runtimePath, ".."));
		}
	}
}

