// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Text;
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
		public string AndroidDefineConstants { get; set; }

		public override bool Execute ()
		{
			var sb = new StringBuilder ();

			if (!string.IsNullOrEmpty (ProductVersion)) {
				sb.AppendFormat ("__XAMARIN_ANDROID_{0}__;", Regex.Replace (ProductVersion, "[^A-Za-z0-9]", "_"));
			}
			sb.Append ("__MOBILE__;__ANDROID__");

			for (int i = 1; i <= AndroidApiLevel; ++i)
				sb.Append (";__ANDROID_").Append (i).Append ("__");

			AndroidDefineConstants = sb.ToString ();

			return true;
		}
	}
}
