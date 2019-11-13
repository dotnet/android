using System;

namespace Xamarin.Android.Prepare
{
	partial class MakeRunner
	{
		static string executableName;

		protected override string DefaultToolExecutableName => EnsureExecutableName ();

		static string EnsureExecutableName ()
		{
			if (!String.IsNullOrEmpty (executableName))
				return executableName;

			// gmake is preferred since it comes from HomeBrew and is a much newer version than the one provided by
			// Apple with Xcode. The reason we care is the `--output-sync` option which allows to generate sane output
			// when running with `-j`. The option is supported since Make v4, however Apple provide version 3 while
			// HomeBrew has v4 installed as `gmake`
			string gmake = Context.Instance.OS.Which ("gmake", false);
			if (!String.IsNullOrEmpty (gmake)) {
				Log.Instance.DebugLine ($"Found gmake at {gmake}, using it instead of the Apple provided make");
				executableName = "gmake";
			} else
				executableName = "make";

			return executableName;
		}
	}
}
