// 
// Utils.cs
//  
// Author:
//       Andreia Gaita <andreia@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc.
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

namespace Xamarin.AndroidTools
{
	[Obsolete]
	public static class Utils
	{

		/// <summary>
		/// Converts a framework version (2.2 or v2.2) to an Android API Level (8)
		/// </summary>
		[Obsolete("Use AndroidVersion.TryOSVersionToApiLevel")]
		public static int FrameworkVersionToApiLevel (string version)
		{
			version = version.TrimStart ('v');

			switch (version) {
				case "1.6":
					return 4;
				case "2.0":
					return 5;
				case "2.0.1":
					return 6;
				case "2.1":
					return 7;
				case "2.2":
					return 8;
				case "2.3":
					return 10;
				case "3.0":
					return 11;
				case "3.1":
					return 12;
				case "3.2":
					return 13;
				case "4.0":
					return 14;
				case "4.0.3":
					return 15;
				case "4.1":
					return 16;
				case "4.2":
					return 17;
				case "4.3":
					return 18;
				case "4.4":
					return 19;
				case "4.4.87":
					return 20;
				case "5.0":
					return 21;
			}

			return -1;
		}

		/// <summary>
		/// Gets all the api levels we are currently supporting (shipping assemblies for)
		/// </summary>
		/// <returns></returns>
		[Obsolete("Use MonoDroidSdk.SupportedApiLevels")]
		public static int[] SupportedApiLevels {
			get { return MonoDroidSdk.SupportedApiLevels; }
		}

		/// <summary>
		/// Gets the path to Google's Android SDK
		/// </summary>
		[Obsolete("Use AndroidSdk.AndroidSdkPath")]
		public static string GetAndroidSdkPath ()
		{
			return AndroidSdk.AndroidSdkPath;
		}

		/// <summary>
		/// Sets the user specified path to Google's Android SDK
		/// </summary>
		/// <param name="path"></param>
		[Obsolete("Use AndroidSdk.SetPreferredAndroidSdkPath")]
		public static void SetAndroidSdkPath (string path)
		{
			AndroidSdk.SetPreferredAndroidSdkPath (path);
		}
	}
}
