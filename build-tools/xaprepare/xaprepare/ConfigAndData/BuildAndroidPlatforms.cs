using System;

namespace Xamarin.Android.Prepare
{
	class BuildAndroidPlatforms
	{
		public const string AndroidNdkVersion = "28c";
		public const string AndroidNdkPkgRevision = "28.2.13676358";

		public static string NdkMinimumAPI => Context.Instance.Properties.GetRequiredValue (KnownProperties.AndroidMinimumDotNetApiLevel);
		public static string NdkMinimumAPILegacy32 => NdkMinimumAPI;
	}
}
