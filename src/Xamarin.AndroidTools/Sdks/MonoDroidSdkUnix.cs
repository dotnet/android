//
// MonoDroidSdkUnix.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//       Andreia Gaita <andreia@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Mono.AndroidTools;

namespace Xamarin.AndroidTools
{
	class MonoDroidSdkUnix : MonoDroidSdkBase
	{
		readonly static string[] RuntimeToFrameworkPaths = new[]{
			// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android/
			Path.Combine ("..", "..", "..", "xbuild-frameworks", "MonoAndroid"),
			// runtimePath=??? What is this entry for?
			Path.Combine ("..", "..", "..", ".xamarin.android", "lib", "xbuild-frameworks", "MonoAndroid"),
			// runtimePath=$prefix/lib/xbuild
			Path.Combine ("..", "xbuild-frameworks", "MonoAndroid"),
			// runtimePath=??? System-wide Unix install?
			Path.Combine ("..", "mono", "xbuild-frameworks", "MonoAndroid"),
		};

		readonly static string[] SearchPaths = {
			// macOS
			"/Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/xamarin.android/xbuild/Xamarin/Android",
			"/Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/mandroid",
			"/Developer/MonoAndroid/usr/lib/mandroid",
			// FlatPak
			"/app/lib/mandroid",
			"/app/lib/xamarin.android/xbuild/Xamarin/Android",
			// Various non-existent Unix paths
			"/opt/mono-android/lib/mandroid",
			"/opt/mono-android/lib/xamarin.android/xbuild/Xamarin/Android",
			"/usr/lib/xamarin.android/xbuild/Xamarin/Android",
		};

		protected override string FindRuntime ()
		{
			string environmentPath  = GetRuntimePathFromEnvironment ();
			if (environmentPath != null)
				return environmentPath;

			// check also in the users folder
			var personal = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
			var initialSearchPaths = new[] {
				Path.Combine (personal, @".xamarin.android/lib/mandroid"),
				Path.Combine (personal, @".xamarin.android/lib/xamarin.android/xbuild/Xamarin/Android"),
			};
			var finalSearchPaths = new[] {
				Path.GetFullPath (Path.GetDirectoryName (GetType ().Assembly.Location)),
			};

			return initialSearchPaths
				.Concat (SearchPaths)
				.Concat (finalSearchPaths)
				.FirstOrDefault (ValidateRuntime);
		}

		string GetRuntimePathFromEnvironment ()
		{
			string monoAndroidPath = Environment.GetEnvironmentVariable ("MONO_ANDROID_PATH");
			if (string.IsNullOrEmpty (monoAndroidPath))
				return null;

			if (!Directory.Exists (monoAndroidPath)) {
				AndroidLogger.LogInfo (null, "$MONO_ANDROID_PATH points to `{0}`, but it does not exist.", monoAndroidPath);
				return null;
			}

			string msbuildDir = Path.Combine (monoAndroidPath, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
			if (Directory.Exists (msbuildDir) && ValidateRuntime (msbuildDir))
				return msbuildDir;

			string libMandroid = Path.Combine (monoAndroidPath, "lib", "mandroid");
			if (Directory.Exists (libMandroid) && ValidateRuntime (libMandroid))
				return libMandroid;
			AndroidLogger.LogInfo (null, "$MONO_ANDROID_PATH points to `{0}`, but it is invalid.", monoAndroidPath);
			return null;
		}

		protected override bool ValidateBin (string binPath)
		{
			if (!Directory.Exists (binPath))
				return false;
			if (File.Exists (Path.Combine (binPath, "aapt2")))
				return true;
			return false;
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

		protected override string FindBin (string runtimePath)
		{
			string osName   = OS.IsMac ? "Darwin" : "Linux";
			string binPath  = Path.GetFullPath (Path.Combine (runtimePath, osName));
			if (File.Exists (Path.Combine (binPath, "aapt2")))
				return binPath;

			return null;
		}

		protected override string FindInclude (string runtimePath)
		{
			string includeDir = Path.GetFullPath (Path.Combine (runtimePath, "..", "..", "include"));
			if (Directory.Exists (includeDir))
				return includeDir;
			return null;
		}

		protected override string FindLibraries (string runtimePath)
		{
			var libPaths = new[]{
				// runtimePath=$prefix/lib/mandroid
				Path.GetFullPath (Path.Combine (runtimePath, "..")),
				// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android
				Path.GetFullPath (Path.Combine (runtimePath, "lib", "host")),
			};
			var requiredFile  = "libmono-android.debug.dylib";
			foreach (var libPath in libPaths) {
				var required  = Path.Combine (libPath, requiredFile);
				if (File.Exists (required))
					return libPath;
			}
			return null;
		}
	}
}
