// 
// GetAndroidPackageName.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Novell, Inc. (http://www.novell.com)
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

using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GetAndroidPackageName : Task
	{
		public string ManifestFile { get; set; }

		[Required]
		public string AssemblyName { get; set; }

		[Output]
		public string PackageName { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GetAndroidPackageName Task");
			Log.LogDebugMessage ("  ManifestFile: {0}", ManifestFile);
			Log.LogDebugMessage ("  AssemblyName: {0}", AssemblyName);

			// If we don't have a manifest, default to using the assembly name
			// If the assembly doesn't have a period in it, duplicate it so it does
			PackageName = AndroidAppManifest.CanonicalizePackageName (AssemblyName);

			if (string.IsNullOrEmpty (ManifestFile) || !File.Exists (ManifestFile)) {
				Log.LogMessage ("  PackageName: {0}", PackageName);
				return true;
			}

			XmlDocument doc = new XmlDocument ();

			doc.Load (ManifestFile);

			if (!doc.DocumentElement.HasAttribute ("package")) {
				Log.LogMessage ("  PackageName: {0}", PackageName);
				return true;
			}

			PackageName = AndroidAppManifest.CanonicalizePackageName (doc.DocumentElement.GetAttribute ("package"));

			Log.LogMessage ("  PackageName: {0}", PackageName);

			return true;
		}
	}
}
