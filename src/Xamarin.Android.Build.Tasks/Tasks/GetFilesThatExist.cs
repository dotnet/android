// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	// We have a list of files, we want to get the
	// ones that actually exist on disk.
	public class GetFilesThatExist : Task
	{
		[Required]
		public ITaskItem[] Files { get; set; }

		[Output]
		public ITaskItem[] FilesThatExist { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GetFilesThatExist Task");
			Log.LogDebugTaskItems ("  Files", Files);

			FilesThatExist = Files.Where (p => File.Exists (p.ItemSpec)).ToArray ();

			Log.LogDebugTaskItems ("  [Output] FilesThatExist", FilesThatExist);

			return true;
		}
	}
}
