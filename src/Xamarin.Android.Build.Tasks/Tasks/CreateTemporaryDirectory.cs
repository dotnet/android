// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CreateTemporaryDirectory : AndroidTask
	{
		public override string TaskPrefix => "CTD";

		[Output]
		public string TemporaryDirectory { get; set; }

		public override bool RunTask ()
		{
			TemporaryDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (TemporaryDirectory);
			Log.LogDebugMessage ("  OUTPUT: TemporaryDirectory: {0}", TemporaryDirectory);

			return true;
		}
	}
}
