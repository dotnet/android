using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Prepare
{
	partial class MacOS : Unix
	{
		const string HomebrewErrorsAdvice = @"
Some errors occurred while running one or more Homebrew commands (please check logs for details).
It may mean that there are some issues with your instance of Homebrew. In order to make sure that
this is not the case, please run the following command and follow any instructions given by it:

   brew doctor

After all the issues are fixed, please re-run the bootstrapper.
";

		public override string Type { get; } = "Darwin";
		public override List<Program> Dependencies { get; }

		public override StringComparison DefaultStringComparison => StringComparison.OrdinalIgnoreCase;
		public override StringComparer DefaultStringComparer => StringComparer.OrdinalIgnoreCase;

		public Version Version         { get; }
		public string Build            { get; }
		public Version? HomebrewVersion { get; private set; }
		public bool HomebrewErrors     { get; set; }

		bool processIsTranslated;

		public override bool ProcessIsTranslated => processIsTranslated;

		public MacOS (Context context)
			: base (context)
		{
			Flavor = "macOS";

			string progPath = FindProgram ("sw_vers", GetPathDirectories ());
			if (!String.IsNullOrEmpty (progPath)) {
				Name = Utilities.GetStringFromStdout (progPath, "-productName");

				string release = Utilities.GetStringFromStdout (progPath, "-productVersion");
				string build = Utilities.GetStringFromStdout (progPath, "-buildVersion");
				Build = build;
				Release = $"{release} ({build})";

				if (!Version.TryParse (release, out Version? ver) || ver == null) {
					Log.WarningLine ($"Unable to parse macOS version: {release}");
					Version = new Version (0, 0, 0);
				} else
					Version = ver;
			} else {
				Name = "macOS";
				Release = "0.0.0";
				Build = "Unknown";
				Version = new Version (0, 0, 0);
			}

			processIsTranslated = GetProcessIsTranslated ();

			Dependencies = new List<Program> ();
		}

		unsafe bool GetProcessIsTranslated ()
		{
			int ret = 0;
			ulong size = sizeof (int);
			if (NativeMethods.sysctlbyname ("sysctl.proc_translated", &ret, &size, null, 0) == -1) {
				return false;
			}
			return ret != 0;
		}

		protected override bool InitOS ()
		{
			if (!base.InitOS ())
				return false;

			string brewPath = Which ("brew", false);
			if (String.IsNullOrEmpty (brewPath)) {
				Log.ErrorLine ("Could not find Homebrew on this system, please install it from https://brew.sh/");
				return false;
			}

			Context.MonoOptions.Add ("--arch=64");
			Context.Instance.Tools.BrewPath = brewPath;

			(bool success, string bv) = Utilities.GetProgramVersion (brewPath);
			if (!success || !Version.TryParse (bv, out Version? brewVersion) || brewVersion == null) {
				Log.ErrorLine ("Failed to obtain Homebrew version");
				return false;
			}

			HomebrewVersion = brewVersion;

			return true;
		}

		public override void ShowFinalNotices ()
		{
			base.ShowFinalNotices ();
			if (!HomebrewErrors)
				return;

			Log.WarningLine (HomebrewErrorsAdvice, ConsoleColor.White, showSeverity: false);
		}
	};

	partial class NativeMethods {
		[DllImport ("c")]
		internal static unsafe extern int sysctlbyname (
			string name, void* oldp, ulong *oldlenp, void* newp, ulong newlen);
	}
}
