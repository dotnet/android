using System;

namespace Xamarin.Android.Prepare
{
	partial class EssentialTools : AppObject
	{
		public string GitPath             { get; set; }
		public string SevenZipPath        { get; set; }

		public EssentialTools ()
		{}

		public void Init (Context context)
		{
			Log.StatusLine ();
			Log.StatusLine ("Locating essential tool binaries", ConsoleColor.DarkGreen);

			Log.StatusLine ($"  {context.Characters.Bullet} git", ConsoleColor.White);
			GitPath = context.OS.Which ("git", required: true);
			Log.StatusLine ("     Found: ", GitPath, tailColor: Log.DestinationColor);

			Log.StatusLine ($"  {context.Characters.Bullet} 7za", ConsoleColor.White);
			SevenZipPath = context.OS.Which ("7za", required: true);
			Log.StatusLine ("     Found: ", SevenZipPath, tailColor: Log.DestinationColor);

			InitOS (context);
		}

		partial void InitOS (Context context);
	}
}
