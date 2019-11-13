﻿// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	// We have a list of files, we want to get the
	// ones that actually exist on disk.
	public class GetFilesThatExist : AndroidTask
	{
		public override string TaskPrefix => "GFT";

		[Required]
		public ITaskItem[] Files { get; set; }

		public ITaskItem [] IgnoreFiles { get; set; }

		[Output]
		public ITaskItem[] FilesThatExist { get; set; }

		public override bool RunTask ()
		{
			FilesThatExist = Files.Where (p => File.Exists (p.ItemSpec) &&
					(!IgnoreFiles?.Contains (p, TaskItemComparer.DefaultComparer) ?? true)).ToArray ();

			Log.LogDebugTaskItems ("  [Output] FilesThatExist", FilesThatExist);

			return true;
		}
	}
}
