using System;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class AndroidSdkResolver
	{
		static AndroidSdkInfo sdk_info = new AndroidSdkInfo ();

		public static string GetAndroidSdkPath () => sdk_info.AndroidSdkPath;

		public static string GetAndroidNdkPath () => sdk_info.AndroidNdkPath;
	}
}
