using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	class AndroidPlatform
	{
		public uint ApiLevel     { get; }
		public string PlatformID { get; }
		public string Framework  { get; }
		public bool Stable       { get; }
		public bool Supported    { get; }

		public AndroidPlatform (uint apiLevel, string platformID, string framework = null, bool stable = true)
		{
			if (String.IsNullOrEmpty (platformID))
				throw new ArgumentException ("must not be null or empty", nameof (platformID));

			ApiLevel = apiLevel;
			PlatformID = platformID;
			Framework = framework ?? String.Empty;
			Stable = stable;
			Supported = !String.IsNullOrEmpty (framework);
		}
	}

	static class AndroidPlatformExtensions
	{
		public static void Add (this List<AndroidPlatform> list, uint apiLevel, string platformID, string framework, bool stable)
		{
			if (list == null)
				throw new ArgumentNullException (nameof (list));

			if (list.Any (p => p.ApiLevel == apiLevel))
				throw new InvalidOperationException ($"Duplicate Android platform, API level {apiLevel}");

			list.Add (new AndroidPlatform (apiLevel, platformID, framework, stable));
		}
	}
}
