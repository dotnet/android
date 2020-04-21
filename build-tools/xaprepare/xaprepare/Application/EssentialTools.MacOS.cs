using System;

namespace Xamarin.Android.Prepare
{
	partial class EssentialTools : AppObject
	{
		string brewPath;
		string pkgutilPath;

		public string BrewPath {
			get => GetRequiredValue (brewPath, "brew");
			set => brewPath = value;
		}

		public string PkgutilPath {
			get => GetRequiredValue (pkgutilPath, "pkgutil");
			set => pkgutilPath = value;
		}

		partial void InitOS (Context context)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} homebrew", ConsoleColor.White);
			if (String.IsNullOrEmpty (brewPath))
				BrewPath = context.OS.Which ("brew", required: true);
			ReportToolPath (brewPath);

			Log.StatusLine ($"  {context.Characters.Bullet} pkgutil", ConsoleColor.White);
			if (String.IsNullOrEmpty (pkgutilPath))
				PkgutilPath = context.OS.Which ("/usr/sbin/pkgutil", required: true);
			ReportToolPath (pkgutilPath);

			InitSharedUnixOS (context);
		}
	}
}
