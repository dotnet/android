// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks {
	// We have a list of files, we want to get the
	// ones that actually exist on disk.
	public class GetMappedFilesThatExist : Task
	{
		[Required]
		public string[] CheckedFiles { get; set; }

		[Required]
		public string[] ResultingFiles { get; set; }

		[Output]
		public string[] FilesThatExist { get; set; }

		public override bool Execute ()
		{
			if (CheckedFiles.Length != ResultingFiles.Length)
				throw new ArgumentException ("CheckedFiles and ResultingFiles must be arrays of the same length.");

			FilesThatExist = CheckedFiles.Zip (ResultingFiles, (c, r) => new {Checked = c, Result = r})
			                             .Where (p => File.Exists (p.Checked))
			                             .Select (p => p.Result)
			                             .ToArray ();
			
			return true;
		}
	}
}
