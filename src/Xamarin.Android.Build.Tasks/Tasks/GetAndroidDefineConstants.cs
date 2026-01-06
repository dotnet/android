// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GetAndroidDefineConstants : AndroidTask
	{
		public override string TaskPrefix => "GAD";

		[Required]
		public string AndroidApiLevel { get; set; } = "";

		public string? ProductVersion         { get; set; }

		[Output]
		public  ITaskItem[]?    AndroidDefineConstants      { get; set; }

		public override bool RunTask ()
		{
			var constants = new List<ITaskItem> ();

			if (!ProductVersion.IsNullOrEmpty ()) {
				var version = Regex.Replace (ProductVersion, "[^A-Za-z0-9]", "_");
				constants.Add (new TaskItem ($"__XAMARIN_ANDROID_{version}__"));
			}

			constants.Add (new TaskItem ("__MOBILE__"));
			constants.Add (new TaskItem ("__ANDROID__"));

			if (!MonoAndroidHelper.TryParseApiLevel (AndroidApiLevel, out var apiLevel)) {
				return false;
			}

			for (int i = 1; i <= apiLevel.Major; ++i) {
				constants.Add (new TaskItem ($"__ANDROID_{i}__"));
			}
			// TODO: We're just going to assume that there is a minor release for every major release from API-36.1 onward…
			for (int i = 36; i < apiLevel.Major; ++i) {
				constants.Add (new TaskItem ($"__ANDROID_{i}_1__"));
			}
			if (apiLevel.Minor != 0) {
				constants.Add (new TaskItem ($"__ANDROID_{apiLevel.Major}_{apiLevel.Minor}__"));
			}

			AndroidDefineConstants = constants.ToArray ();

			return true;
		}
	}
}
