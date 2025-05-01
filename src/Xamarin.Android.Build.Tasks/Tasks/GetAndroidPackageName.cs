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
#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GetAndroidPackageName : AndroidTask
	{
		public override string TaskPrefix => "GAP";

		public string ManifestFile { get; set; }

		[Required]
		public string AssemblyName { get; set; }

		public string [] ManifestPlaceholders { get; set; }

		[Output]
		public string PackageName { get; set; }

		public override bool RunTask ()
		{
			if (!string.IsNullOrEmpty (ManifestFile) && File.Exists (ManifestFile)) {
				using var stream = File.OpenRead (ManifestFile);
				using var reader = XmlReader.Create (stream);
				if (reader.MoveToContent () == XmlNodeType.Element) {
					var package = reader.GetAttribute ("package");
					if (!string.IsNullOrEmpty (package)) {
						PackageName = ManifestDocument.ReplacePlaceholders (ManifestPlaceholders, package);
					}
				}
			}

			if (!string.IsNullOrEmpty (PackageName)) {
				// PackageName may be passed in via $(ApplicationId) and missing from AndroidManifest.xml
				PackageName = AndroidAppManifest.CanonicalizePackageName (PackageName);
			} else {
				// If we don't have a manifest, default to using the assembly name
				// If the assembly doesn't have a period in it, duplicate it so it does
				PackageName = AndroidAppManifest.CanonicalizePackageName (AssemblyName);
			}

			Log.LogDebugMessage ($"  PackageName: {PackageName}");

			return !Log.HasLoggedErrors;
		}
	}
}
