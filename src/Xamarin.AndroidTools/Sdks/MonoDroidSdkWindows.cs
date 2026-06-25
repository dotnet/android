//
// MonoDroidSdkWindows.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.AndroidTools
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
			var assemblyLocation = Path.GetFullPath (Path.GetDirectoryName (GetType ().Assembly.Location));
			if (Directory.Exists (assemblyLocation) && ValidateRuntime (assemblyLocation))
				return assemblyLocation;
			string xamarinSdk = Path.Combine (OS.ProgramFilesX86, "MSBuild", "Xamarin", "Android");
			return Directory.Exists (xamarinSdk)
				? xamarinSdk
				: OS.ProgramFilesX86 + @"\MSBuild\Novell";
		}

		static readonly string[] RuntimeToFrameworkPaths = new []{
			// runtimePath=C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Xamarin\Android
			Path.Combine ("..", "..", "..", "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework","MonoAndroid"),
			// runtimePath=??? I suspect this is leftover from VS 2015
			Path.Combine ("..", "..", "..", "Reference Assemblies", "Microsoft", "Framework", "MonoAndroid"),
			// global reference assemblies directory
			Path.Combine (OS.ProgramFilesX86, "Reference Assemblies", "Microsoft", "Framework", "MonoAndroid"),
			// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android/, when using a local xamarin-android inverted build
			Path.Combine ("..", "..", "..", "xbuild-frameworks", "MonoAndroid"),
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
			if (!Directory.Exists (binPath))
				return false;
			if (File.Exists (Path.Combine (binPath, "aapt2.exe")))
				return true;
			return false;
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
			yield return Path.GetFullPath (Path.Combine (RuntimePath, "Version.txt"));
		}
	}
}
