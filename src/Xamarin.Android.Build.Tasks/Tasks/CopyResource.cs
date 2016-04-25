// Author: Jonathan Pobst <jpobst@xamarin.com>
// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;

using Java.Interop.Tools.JavaCallableWrappers;

namespace Xamarin.Android.Tasks
{
	public class CopyResource : Task
	{
		[Required]
		public string ResourceName { get; set; }

		[Required]
		public string OutputPath { get; set; }

		static readonly Assembly assm = Assembly.GetExecutingAssembly ();

		public override bool Execute ()
		{
			return Run (assm, ResourceName, OutputPath, Log);
		}

		public bool Run (Assembly assm, string ResourceName, string OutputPath, TaskLoggingHelper Log)
		{
			// Ensure our output directory exists
			if (!Directory.Exists (Path.GetDirectoryName (OutputPath)))
				Directory.CreateDirectory (Path.GetDirectoryName (OutputPath));

			// Copy out one of our embedded resources to a path
			using (var from = GetManifestResourceStream (ResourceName)) {
				
				// If the resource already exists, only overwrite if it's changed
				if (File.Exists (OutputPath)) {
					var hash1 = MonoAndroidHelper.HashFile (OutputPath);
					var hash2 = MonoAndroidHelper.HashStream (from);

					if (hash1 == hash2) {
						Log.LogDebugMessage ("Resource {0} is unchanged. Skipping.", OutputPath);
						return true;
					}
				}

				// Hash calculation read to the end, move back to beginning of file
				from.Position = 0;

				// Write out the resource
				using (var to = File.Create (OutputPath))
					Copy (from, to);

				Log.LogDebugMessage ("Wrote resource {0}.", OutputPath);
			}

			return true;
		}

		Stream GetManifestResourceStream (string name)
		{
			var r = assm.GetManifestResourceStream (name);
			if (r != null)
				return r;
			r = typeof (JavaCallableWrapperGenerator).Assembly.GetManifestResourceStream (name);
			return r;
		}

		public static void Copy (Stream input, Stream output)
		{
    			byte[] buffer = new byte [8192];
    			int cnt;

    			while ((cnt = input.Read (buffer, 0, buffer.Length)) > 0)
        			output.Write (buffer, 0, cnt);
		}
	}
}

