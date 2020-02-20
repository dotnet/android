using System;

namespace Xamarin.Android.Prepare
{
	partial class EssentialTools : AppObject
	{
		string gitPath = String.Empty;
		string sevenZipPath = String.Empty;
		bool initialized;

		public string GitPath {
			get => GetRequiredValue (gitPath, "git");
			set => gitPath = value;
		}

		public string SevenZipPath {
			get => GetRequiredValue (sevenZipPath, "7zip");
			set => sevenZipPath = value;
		}

		public bool IsInitialized => initialized;

		public EssentialTools ()
		{}

		public void Init (Context context)
		{
			bool require = context.CheckCondition (KnownConditions.AllowProgramInstallation);

			Log.StatusLine ();
			Log.StatusLine ("Locating essential tool binaries", ConsoleColor.DarkGreen);

			Log.StatusLine ($"  {context.Characters.Bullet} git", ConsoleColor.White);
			GitPath = context.OS.Which ("git", required: require);
			ReportToolPath (gitPath);

			Log.StatusLine ($"  {context.Characters.Bullet} 7za", ConsoleColor.White);
			SevenZipPath = context.OS.Which ("7za", required: require);
			ReportToolPath (sevenZipPath);

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
