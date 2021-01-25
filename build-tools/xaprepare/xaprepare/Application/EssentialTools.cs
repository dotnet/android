using System;

namespace Xamarin.Android.Prepare
{
	partial class EssentialTools : AppObject
	{
		bool initialized;

		public bool IsInitialized => initialized;

		public EssentialTools ()
		{}

		partial void InitGit (Context context, bool require);
		partial void InitSevenZip (Context context, bool require);

		public void Init (Context context)
		{
			bool require = AreToolsRequired (context);

			Log.StatusLine ();
			Log.StatusLine ("Locating essential tool binaries", ConsoleColor.DarkGreen);

			InitGit (context, require);
			InitSevenZip (context, require);
			InitOS (context);
			initialized = true;
		}

		void ReportToolPath (string path)
		{
			if (!String.IsNullOrEmpty (path))
				Log.StatusLine ("     Found: ", path, tailColor: Log.DestinationColor);
			else
				Log.StatusLine ("   Missing: will be installed later");
		}

		string GetRequiredValue (string val, string name)
		{
			if (String.IsNullOrEmpty (val)) {
				throw new InvalidOperationException ($"{name} not found but required by this scenario");
			}

			return val;
		}

		partial void InitOS (Context context);
	}
}
