// Copyright (C) 2022 Microsoft Ltd, Inc. All rights reserved.
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateRtxt : AndroidTask
	{
		public override string TaskPrefix => "GR";

		[Required]
		public string RTxtFile { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }
		public string[] AdditionalResourceDirectories { get; set; }

		public string JavaPlatformJarPath { get; set; }

		public string ResourceFlagFile { get; set; }
		public string CaseMapFile { get; set; }

		public override bool RunTask ()
		{
			// Parse the Resource files and then generate an R.txt file
			var writer = new RtxtWriter ();

			var resource_fixup = MonoAndroidHelper.LoadMapFile (BuildEngine4, Path.GetFullPath (CaseMapFile), StringComparer.OrdinalIgnoreCase);

			var javaPlatformDirectory = string.IsNullOrEmpty (JavaPlatformJarPath) ? "" : Path.GetDirectoryName (JavaPlatformJarPath);
			var parser = new FileResourceParser () { Log = Log, JavaPlatformDirectory = javaPlatformDirectory, ResourceFlagFile = ResourceFlagFile};
			var resources = parser.Parse (ResourceDirectory, AdditionalResourceDirectories, resource_fixup);

			// only update if it changed.
			writer.Write (RTxtFile, resources);

			return !Log.HasLoggedErrors;
		}
	}
}
