//
// ResolveSdksTask.cs
//
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
//       Jonathan Pryor <jonp@xamarin.com>
//
// Copyright (c) 2010 Novell, Inc.
// Copyright (c) 2013 Xamarin Inc.
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
#nullable enable
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Discovers SDK paths and refreshes MonoAndroidHelper.SupportedVersions.
	/// </summary>
	public class ResolveSdks : AndroidTask
	{
		public override string TaskPrefix => "RSD";

		/// <summary>
		/// In Xamarin.Android, this is the path to ReferenceAssemblies\Microsoft\Framework\MonoAndroid\v*.*\ that contains Mono.Android.dll
		/// In .NET 6, this is dotnet\packs\Microsoft.Android.Sdk.Windows|Darwin\*\data\net6.0-android*\. Only contains AndroidApiInfo.xml
		/// </summary>
		public string []? ReferenceAssemblyPaths { get; set; }

		public string? CommandLineToolsVersion { get; set; }

		[Required]
		public string MinimumSupportedJavaVersion   { get; set; } = "";

		[Required]
		public string LatestSupportedJavaVersion    { get; set; } = "";

		[Output]
		public string? CommandLineToolsPath { get; set; }

		[Output]
		public string? AndroidNdkPath { get; set; }

		[Output]
		public string? AndroidSdkPath { get; set; }

		[Output]
		public string? JavaSdkPath { get; set; }

		[Output]
		public string? MonoAndroidToolsPath { get; set; }

		[Output]
		public string? MonoAndroidBinPath { get; set; }

		[Output]
		public string? MonoAndroidLibPath { get; set; }

		public override bool RunTask ()
		{
			// OS X:    $prefix/lib/xamarin.android/xbuild/Xamarin/Android
			// Windows: %ProgramFiles(x86)%\MSBuild\Xamarin\Android
			if (MonoAndroidToolsPath.IsNullOrEmpty ()) {
				MonoAndroidToolsPath  = Path.GetDirectoryName (typeof (ResolveSdks).Assembly.Location);
			}
			MonoAndroidBinPath  = MonoAndroidHelper.GetOSBinPath () + Path.DirectorySeparatorChar;
			MonoAndroidLibPath  = MonoAndroidHelper.GetOSLibPath () + Path.DirectorySeparatorChar;

			var minVersion      = Version.Parse (MinimumSupportedJavaVersion);
			var maxVersion      = Version.Parse (LatestSupportedJavaVersion);

			JavaSdkPath         = MonoAndroidHelper.GetJdkInfo (this.CreateTaskLogger (), JavaSdkPath, minVersion, maxVersion)?.HomePath;

			MonoAndroidHelper.RefreshSupportedVersions (ReferenceAssemblyPaths);

			AndroidSdkInfo androidSdk;
			try {
				androidSdk = new AndroidSdkInfo (LogSdkMessage, AndroidSdkPath, AndroidNdkPath, JavaSdkPath);
			} catch (InvalidOperationException e) {
				if (e.Message.Contains (" Android ")) {
					Log.LogCodedError ("XA5300", Properties.Resources.XA5300_Android_SDK);
				} else if (e.Message.Contains (" Java ")) {
					Log.LogCodedError ("XA5300", Properties.Resources.XA5300_Java_SDK);
				} else {
					Log.LogCodedError ("XA5300", "{0}", e.Message);
				}
				return false;
			}

			AndroidNdkPath = androidSdk.AndroidNdkPath is { Length: > 0 } ndkPath ? Path.GetFullPath (ndkPath) : androidSdk.AndroidNdkPath;
			AndroidSdkPath = Path.GetFullPath (androidSdk.AndroidSdkPath);
			JavaSdkPath    = Path.GetFullPath (androidSdk.JavaSdkPath);

			CommandLineToolsPath    = AndroidSdkInfo.GetCommandLineToolsPaths (AndroidSdkPath, CommandLineToolsVersion ?? "")
				.FirstOrDefault () ??
				Path.Combine (AndroidSdkPath, "tools");

			if (AndroidSdkPath.IsNullOrEmpty ()) {
				Log.LogCodedError ("XA5300", Properties.Resources.XA5300_Android_SDK);
				return false;
			}
			if (JavaSdkPath.IsNullOrEmpty ()) {
				Log.LogCodedError ("XA5300", Properties.Resources.XA5300_Java_SDK);
				return false;
			}

			//note: this task does not error out if it doesn't find all things. that's the job of the targets
			return !Log.HasLoggedErrors;
		}

		void LogSdkMessage (TraceLevel level, string value)
		{
			switch (level) {
			case TraceLevel.Error:
				Log.LogCodedError ("XA5300", "{0}", value);
				break;
			case TraceLevel.Warning:
				Log.LogCodedWarning ("XA5300", "{0}", value);
				break;
			default:
				Log.LogDebugMessage ("{0}", value);
				break;
			}
		}
	}
}
