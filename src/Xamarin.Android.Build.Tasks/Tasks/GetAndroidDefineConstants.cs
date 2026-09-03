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
			// API-36 had only one minor release
			if (36 < apiLevel.Major) {
				constants.Add (new TaskItem ($"__ANDROID_36_1__"));
			}
			// Assume no more than 4 "quarterly platform releases" per API level
			for (int api = 37; api < apiLevel.Major; ++api) {
				for (int minor = 1; minor <= 4; ++minor) {
					constants.Add (new TaskItem ($"__ANDROID_{api}_{minor}__"));
				}
			}
			// For current API level, minor releases from 1 through apiLevel.Minor
			for (int minor = 1; minor <= apiLevel.Minor; ++minor) {
				constants.Add (new TaskItem ($"__ANDROID_{apiLevel.Major}_{minor}__"));
			}

			AndroidDefineConstants = constants.ToArray ();

			return true;
		}
	}
}
