﻿// 
// ReadImportedLibrariesCache.cs
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
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ReadImportedLibrariesCache : AndroidTask
	{
		public override string TaskPrefix => "RIL";

		[Required]
		public string CacheFile { get; set;} 

		[Output]
		public ITaskItem [] Jars { get; set; }

		[Output]
		public ITaskItem [] NativeLibraries { get; set; }

		[Output]
		public ITaskItem [] ManifestDocuments { get; set; }

		public override bool RunTask ()
		{
			if (!File.Exists (CacheFile)) {
				Log.LogDebugMessage ("{0} does not exist. No Imported Libraries found", CacheFile);
				return !Log.HasLoggedErrors;
			}
			var doc = XDocument.Load (CacheFile);
			Jars = doc.GetPathsAsTaskItems ("Jars", "Jar");
			NativeLibraries = doc.GetPathsAsTaskItems ("NativeLibraries", "NativeLibrary");
			ManifestDocuments = doc.GetPathsAsTaskItems ("ManifestDocuments", "ManifestDocument");

			Log.LogDebugTaskItems ("  NativeLibraries: ", NativeLibraries);
			Log.LogDebugTaskItems ("  Jars: ", Jars);
			Log.LogDebugTaskItems ("  ManifestDocuments: ", ManifestDocuments);

			return !Log.HasLoggedErrors;
		}
	}
}

