﻿using System;
using System.IO;

namespace Xamarin.Android.Build.Utilities
{
	public static class MonoDroidSdk
	{
		static MonoDroidSdkBase sdk;

		public static string GetApiLevelForFrameworkVersion (string framework)
		{
			return GetSdk ().GetApiLevelForFrameworkVersion (framework);
		}

		public static string GetFrameworkVersionForApiLevel (string apiLevel)
		{
			return GetSdk ().GetFrameworkVersionForApiLevel (apiLevel);
		}

		public static bool IsSupportedFrameworkLevel (string apiLevel)
		{
			return GetSdk ().IsSupportedFrameworkLevel (apiLevel);
		}

		public static void Refresh (string runtimePath = null, string binPath = null, string bclPath = null)
		{
			if (OS.IsWindows) {
				sdk = new MonoDroidSdkWindows ();
			} else {
				sdk = new MonoDroidSdkUnix ();
			}

			try {
				sdk.Initialize (runtimePath, binPath, bclPath);
			} catch (Exception ex) {
				AndroidLogger.LogError ("Error finding Xamarin.Android SDK", ex);
			}
		}

		static MonoDroidSdkBase GetSdk ()
		{
			if (sdk == null) {
				Refresh ();
			}
			return sdk;
		}

		public static string FrameworkPath { get { return GetSdk ().BclPath; } }
	}
}

