using System;

namespace Xamarin.Android.Prepare
{
	partial class EssentialTools : AppObject
	{
		public string BrewPath    { get; set; }
		public string PkgutilPath { get; set; }

		partial void InitOS (Context context)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} homebrew", ConsoleColor.White);
			if (String.IsNullOrEmpty (BrewPath))
				BrewPath = context.OS.Which ("brew", required: true);
			Log.StatusLine ("     Found: ", BrewPath, tailColor: Log.DestinationColor);

			Log.StatusLine ($"  {context.Characters.Bullet} pkgutil", ConsoleColor.White);
			if (String.IsNullOrEmpty (PkgutilPath))
				PkgutilPath = context.OS.Which ("/usr/sbin/pkgutil", required: true);
			Log.StatusLine ($"    Found: ", PkgutilPath, tailColor: Log.DestinationColor);

			InitSharedUnixOS (context);
		}
	}
}
