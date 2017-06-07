// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class GetAndroidDefineConstants : Task
	{
		[Required]
		public int AndroidApiLevel { get; set; }

		public string ProductVersion         { get; set; }

		[Output]
		public ITaskItem[] AndroidDefineConstants { get; set; }

		public override bool Execute ()
		{
			var items = new List<ITaskItem> ();

			if (!string.IsNullOrEmpty (ProductVersion)) {
				items.Add(new TaskItem ($"__XAMARIN_ANDROID_{Regex.Replace (ProductVersion, "[^A-Za-z0-9]", "_")}__"));
			}
			items.Add(new TaskItem ("__MOBILE__"));
			items.Add(new TaskItem("__ANDROID__"));

			for (int i = 1; i <= AndroidApiLevel; ++i)
				items.Add(new TaskItem ($"__ANDROID_{i}__"));

			AndroidDefineConstants = items.ToArray ();

			return true;
		}
	}
}
