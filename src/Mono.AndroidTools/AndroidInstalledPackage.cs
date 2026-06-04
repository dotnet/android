// 
// Package.cs
//  
// Author:
//       Jonathan Pobst (monkey@jpobst.com)
// 
// Copyright (c) 2011 Novell, Inc.
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

// COPIED FROM ANDROIDVS

using System;
using System.Globalization;
using System.IO;

namespace Mono.AndroidTools
{
	public class AndroidInstalledPackage
	{
		private int version = -1;

		public string Name { get; set; }
		public string ApkFile { get; set; }

		public AndroidInstalledPackage ()
		{
		}

		/// <summary>
		/// API 26+ input of the form:
		/// package:/data/app/Mono.Android.Platform.ApiLevel_29-w_Hk8Z0S3TaJRbd9aJd2Eg==/base.apk=Mono.Android.Platform.ApiLevel_29 versionCode:1578928154
		///
		/// API less than 26 input of the form:
		/// package:/data/app/Mono.Android.Platform.ApiLevel_29-w_Hk8Z0S3TaJRbd9aJd2Eg==/base.apk=Mono.Android.Platform.ApiLevel_29
		/// </summary>
		public AndroidInstalledPackage (string value)
		{
			if (value.StartsWith ("package:", StringComparison.Ordinal))
				value = value.Substring ("package:".Length);

			int equalsIndex = value.LastIndexOf ('=');
			if (equalsIndex == -1)
				return;
			ApkFile = value.Substring (0, equalsIndex);

			string afterEquals = value.Substring (equalsIndex + 1, value.Length - equalsIndex - 1);
			int spaceIndex = afterEquals.LastIndexOf (' ');
			if (spaceIndex == -1) {
				// Treat the rest of the string as the Name
				Name = afterEquals;
				return;
			}
			Name = afterEquals.Substring (0, spaceIndex);

			int colonIndex = afterEquals.LastIndexOf (':');
			if (colonIndex == -1)
				return;
			if (!int.TryParse (afterEquals.Substring (colonIndex + 1, afterEquals.Length - colonIndex - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out version)) {
				version = -1;
			}
		}

		public AndroidInstalledPackage (string name, string apkfile)
		{
			Name = name;
			ApkFile = apkfile;
		}

		public AndroidInstalledPackage (string name, string apkfile, int version)
		{
			Name = name;
			ApkFile = apkfile;
			this.version = version;
		}

		public string ApkFileWithoutVersion {
			get { return Path.GetFileNameWithoutExtension (ApkFile).Split ('-')[0]; }
		}

		public int Version {
			get {
				if (version != -1)
					return version;

				return int.MaxValue;
			}
		}

		public override string ToString ()
		{
			return string.Format ("{0} [{1}]", ApkFileWithoutVersion, Version);
		}
	}
}