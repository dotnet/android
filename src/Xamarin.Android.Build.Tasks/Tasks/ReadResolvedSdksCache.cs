// 
// ReadResolveSdksCache.cs
//  
// Author:
//       Dean Ellis <dean.ellis@xamarin.com>
// 
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
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.AndroidTools;

namespace Xamarin.Android.Tasks
{
	public class ReadResolvedSdksCache : Task 
	{
		[Required]
		public string CacheFile { get; set;} 

		[Output]
		public string[] ReferenceAssemblyPaths { get; set; }

		[Output]
		public string AndroidApiLevel { get; set; }

		[Output]
		public string AndroidApiLevelName { get; set; }

		[Output]
		public string SupportedApiLevel { get; set; }

		[Output]
		public string TargetFrameworkVersion { get; set; }

		[Output]
		public string MonoAndroidToolsPath { get; set; }

		[Output]
		public string MonoAndroidBinPath { get; set; }

		[Output]
		public string MonoAndroidIncludePath { get; set; }

		[Output]
		public string AndroidNdkPath { get; set; }

		[Output]
		public string AndroidSdkPath { get; set; }

		[Output]
		public string JavaSdkPath { get; set; }

		[Output]
		public string AndroidSdkBuildToolsPath { get; set; }

		[Output]
		public string AndroidSdkBuildToolsBinPath { get; set; }

		[Output]
		public string ZipAlignPath { get; set; }

		[Output]
		public string AndroidSequencePointsMode { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Task ReadResolvedSdksCache");
			Log.LogDebugMessage ("  CacheFile: {0}", CacheFile);
			if (!File.Exists (CacheFile)) {
				Log.LogWarning ("{0} does not exist. No Resolved Sdks found", CacheFile);
				return !Log.HasLoggedErrors;
			}
			var doc = XDocument.Load (CacheFile);
			//Sdk/AndroidApiLevel
			var sdk = doc.Element ("Sdk");
			AndroidApiLevel = sdk.Element ("AndroidApiLevel").Value;
			AndroidApiLevelName = sdk.Element ("AndroidApiLevelName").Value;
			SupportedApiLevel = sdk.Element ("SupportedApiLevel").Value;
			TargetFrameworkVersion = sdk.Element ("TargetFrameworkVersion").Value;
			MonoAndroidToolsPath = sdk.Element ("MonoAndroidToolsPath").Value;
			MonoAndroidBinPath = sdk.Element ("MonoAndroidBinPath").Value;
			MonoAndroidIncludePath = sdk.Element ("MonoAndroidIncludePath").Value;
			AndroidNdkPath = sdk.Element ("AndroidNdkPath").Value;
			AndroidSdkPath = sdk.Element ("AndroidSdkPath").Value;
			JavaSdkPath = sdk.Element ("JavaSdkPath").Value;
			AndroidSdkBuildToolsPath = sdk.Element ("AndroidSdkBuildToolsPath").Value;
			AndroidSdkBuildToolsBinPath = sdk.Element ("AndroidSdkBuildToolsBinPath").Value;
			ZipAlignPath = sdk.Element ("ZipAlignPath").Value;
			ReferenceAssemblyPaths = sdk.Elements ("ReferenceAssemblyPaths")
				.Elements ("ReferenceAssemblyPath")
				.Select (e => e.Value)
				.ToArray ();
			AndroidSequencePointsMode = sdk.Element ("AndroidSequencePointsMode")?.Value ?? "None";

			Log.LogDebugMessage ("ResolveSdksTask Outputs:");
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  AndroidApiLevelName: {0}", AndroidApiLevelName);
			Log.LogDebugMessage ("  AndroidNdkPath: {0}", AndroidNdkPath);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsPath: {0}", AndroidSdkBuildToolsPath);
			Log.LogDebugMessage ("  AndroidSdkBuildToolsBinPath: {0}", AndroidSdkBuildToolsBinPath);
			Log.LogDebugMessage ("  AndroidSdkPath: {0}", AndroidSdkPath);
			Log.LogDebugMessage ("  JavaSdkPath: {0}", JavaSdkPath);
			Log.LogDebugMessage ("  MonoAndroidBinPath: {0}", MonoAndroidBinPath);
			Log.LogDebugMessage ("  MonoAndroidToolsPath: {0}", MonoAndroidToolsPath);
			Log.LogDebugMessage ("  MonoAndroidIncludePath: {0}", MonoAndroidIncludePath);
			Log.LogDebugMessage ("  TargetFrameworkVersion: {0}", TargetFrameworkVersion);
			Log.LogDebugMessage ("  ZipAlignPath: {0}", ZipAlignPath);
			Log.LogDebugMessage ("  SupportedApiLevel: {0}", SupportedApiLevel);
			Log.LogDebugMessage ("  AndroidSequencePointsMode: {0}", AndroidSequencePointsMode);

			MonoAndroidHelper.TargetFrameworkDirectories	= ReferenceAssemblyPaths;

			MonoAndroidHelper.RefreshAndroidSdk (AndroidSdkPath, AndroidNdkPath, JavaSdkPath);
			MonoAndroidHelper.RefreshMonoDroidSdk (MonoAndroidToolsPath, MonoAndroidBinPath, ReferenceAssemblyPaths);

			return !Log.HasLoggedErrors;
		}
	}
}

