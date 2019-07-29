using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	class BuildAndroidPlatforms
	{
		public const string AndroidNdkVersion = "20";
		public const string AndroidNdkPkgRevision = "20.0.5594570";

		public static readonly List<AndroidPlatform> AllPlatforms = new List<AndroidPlatform> {
			new AndroidPlatform (apiLevel: 1,  platformID: "1"),
			new AndroidPlatform (apiLevel: 2,  platformID: "2"),
			new AndroidPlatform (apiLevel: 3,  platformID: "3"),
			new AndroidPlatform (apiLevel: 4,  platformID: "4"),
			new AndroidPlatform (apiLevel: 5,  platformID: "5"),
			new AndroidPlatform (apiLevel: 6,  platformID: "6"),
			new AndroidPlatform (apiLevel: 7,  platformID: "7"),
			new AndroidPlatform (apiLevel: 8,  platformID: "8"),
			new AndroidPlatform (apiLevel: 9,  platformID: "9"),
			new AndroidPlatform (apiLevel: 10, platformID: "10"),
			new AndroidPlatform (apiLevel: 11, platformID: "11"),
			new AndroidPlatform (apiLevel: 12, platformID: "12"),
			new AndroidPlatform (apiLevel: 13, platformID: "13"),
			new AndroidPlatform (apiLevel: 14, platformID: "14"),
			new AndroidPlatform (apiLevel: 15, platformID: "15"),
			new AndroidPlatform (apiLevel: 16, platformID: "16"),
			new AndroidPlatform (apiLevel: 17, platformID: "17"),
			new AndroidPlatform (apiLevel: 18, platformID: "18"),
			new AndroidPlatform (apiLevel: 19, platformID: "19",  framework: "v4.4"),
			new AndroidPlatform (apiLevel: 20, platformID: "20",  framework: "v4.4.87"),
			new AndroidPlatform (apiLevel: 21, platformID: "21",  framework: "v5.0"),
			new AndroidPlatform (apiLevel: 22, platformID: "22",  framework: "v5.1"),
			new AndroidPlatform (apiLevel: 23, platformID: "23",  framework: "v6.0"),
			new AndroidPlatform (apiLevel: 24, platformID: "24",  framework: "v7.0"),
			new AndroidPlatform (apiLevel: 25, platformID: "25",  framework: "v7.1"),
			new AndroidPlatform (apiLevel: 26, platformID: "26",  framework: "v8.0"),
			new AndroidPlatform (apiLevel: 27, platformID: "27",  framework: "v8.1"),
			new AndroidPlatform (apiLevel: 28, platformID: "28",  framework: "v9.0"),
			new AndroidPlatform (apiLevel: 29, platformID: "29",   framework: "v10.0"),
		};

		public static readonly Dictionary<string, uint> NdkMinimumAPI = new Dictionary<string, uint> {
			{ AbiNames.TargetJit.AndroidArmV7a, 16 },
			{ AbiNames.TargetJit.AndroidArmV8a, 21 },
			{ AbiNames.TargetJit.AndroidX86,    16 },
			{ AbiNames.TargetJit.AndroidX86_64, 21 },
		};
	}
}
