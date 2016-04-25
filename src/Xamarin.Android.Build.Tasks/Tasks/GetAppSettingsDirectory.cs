// Author: Jonathan Pobst <jpobst@xamarin.com>
// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class GetAppSettingsDirectory : Task
	{
		[Output]
		public string AppSettingsDirectory { get; set; }

		public override bool Execute ()
		{
			// Windows: C:\Users\Jonathan\AppData\Local\Xamarin\Mono for Android
			var appdata_local = Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData);

			AppSettingsDirectory = Path.Combine (appdata_local, "Xamarin", "Mono for Android");

			return true;
		}
	}
}

