// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CreateResgenManifest : Task
	{
		[Required]
		public string ManifestOutputFile { get; set; }

		[Required]
		public string PackageName { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CreateResgenManifest Task");
			Log.LogDebugMessage ("  ManifestOutputFile: {0}", ManifestOutputFile);
			Log.LogDebugMessage ("  PackageName: {0}", PackageName);

			// <manifest xmlns:android="http://schemas.android.com/apk/res/android"
			//		   package="MonoAndroidApplication4.MonoAndroidApplication4" />

			var doc = new XDocument (
					new XDeclaration ("1.0", "utf-8", null),
					new XElement ("manifest",
							new XAttribute (XNamespace.Xmlns + "android", "http://schemas.android.com/apk/res/android"),
							new XAttribute ("package", PackageName)));

			Files.Archive (ManifestOutputFile, manifest => {
					using (var o = new StreamWriter (manifest, false, Encoding.UTF8))
						doc.Save (o);
			});

			return true;
		}
	}
}
