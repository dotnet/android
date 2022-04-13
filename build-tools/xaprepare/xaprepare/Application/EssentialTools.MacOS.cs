using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class EssentialTools : AppObject
	{
		string brewPath = String.Empty;
		string pkgutilPath = String.Empty;

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
			if (File.Exists (brewPath)) {
				(bool success, string version) = Utilities.GetProgramVersion (brewPath);
				if (success)
					context.BuildToolsInventory.Add ("homebrew", version);
			}

			Log.StatusLine ($"  {context.Characters.Bullet} pkgutil", ConsoleColor.White);
			if (String.IsNullOrEmpty (pkgutilPath))
				PkgutilPath = context.OS.Which ("/usr/sbin/pkgutil", required: true);
			ReportToolPath (pkgutilPath);

			InitSharedUnixOS (context);
		}
	}
}
