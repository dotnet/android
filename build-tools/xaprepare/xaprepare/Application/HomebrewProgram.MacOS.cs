using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class HomebrewProgram : Program
	{
		string cachedVersionOutput;
		bool brewNeedsSudo;

		public override bool NeedsSudoToInstall => brewNeedsSudo;
		public string HomebrewTapName           { get; }
		public Uri HomebrewFormulaUrl           { get; }
		public bool Pin                         { get; set; }

		public HomebrewProgram (string homebrewPackageName, string executableName = null)
			: this (homebrewPackageName, homebrewTap: null, executableName: executableName)
		{}

		public HomebrewProgram (string homebrewPackageName, Uri homebrewFormulaUrl, string executableName = null)
			: this (homebrewPackageName, homebrewTap: null, executableName: executableName)
		{
			HomebrewFormulaUrl = homebrewFormulaUrl ?? throw new ArgumentNullException (nameof (homebrewFormulaUrl));
		}

		public HomebrewProgram (string homebrewPackageName, string homebrewTap, string executableName)
		{
			if (String.IsNullOrEmpty (homebrewPackageName))
				throw new ArgumentException ("must not be null or empty", nameof (homebrewPackageName));
			Name = homebrewPackageName;
			HomebrewTapName = homebrewTap?.Trim ();
			ExecutableName = executableName?.Trim ();
		}

		public override async Task<bool> Install ()
		{
			var runner = new BrewRunner (Context.Instance);
			if (!String.IsNullOrEmpty (HomebrewTapName)) {
				if (!await runner.Tap (HomebrewTapName))
					return false;
			}

			bool success;
			if (InstalledButWrongVersion) {
				if (HomebrewFormulaUrl != null)
					success = await runner.Upgrade (HomebrewFormulaUrl.ToString ());
				else
					success = await runner.Upgrade (Name);
			} else
				success = await runner.Install (Name);

			if (!success || !Pin)
				return success;

			return await runner.Pin (Name);
		}

		protected override bool CheckWhetherInstalled ()
		{
			if (String.IsNullOrEmpty (Name)) {
				Log.DebugLine ("Homebrew package name not specified, unable to check installation state");
				return false;
			}

			cachedVersionOutput = GetPackageVersion ();
			return !String.IsNullOrEmpty (cachedVersionOutput);
		}

		protected override bool ParseVersion (string version, out Version ver)
		{
			if (base.ParseVersion (version, out ver))
				return true;

			ver = null;
			if (String.IsNullOrEmpty (version))
				return false;

			// Some brew packages (e.g. mingw-w64) have "weird" version formats, we'll handle them here on the
			// case-by-case basis. First we should try some general rules to handle the weird versions, checking package
			// name should be the very last resort.
			int pos =  version.IndexOf ('_');
			if (pos > 0) {
				// e.g. 6.0.0_1
				string v = version.Replace ('_', '.');
				if (Version.TryParse (v, out ver))
					return true;

				Log.DebugLine ($"Failed to parse {Name} version {version} as {v}");
			}

			return false;
		}

		protected override async Task<bool> DetermineCurrentVersion ()
		{
			bool result = await base.DetermineCurrentVersion ();
			if (result)
				return true;

			if (String.IsNullOrEmpty (cachedVersionOutput)) {
				cachedVersionOutput = GetPackageVersion ();
				if (String.IsNullOrEmpty (cachedVersionOutput))
					return false;
			}

			string[] parts = cachedVersionOutput.Split (new [] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 2) {
				Log.DebugLine ($"Unable to parse {Name} version from Homebrew output: '{cachedVersionOutput}'");
				return false;
			}

			string currentVersion = parts [1];
			if (String.IsNullOrEmpty (currentVersion)) {
				Log.DebugLine ($"Missing Homebrew version info for package {Name}");
				return false;
			}

			CurrentVersion = currentVersion;

			return true;
		}

		protected override async Task AfterDetect (bool installed)
		{
			if (!installed)
				return;

			var runner = new BrewRunner (Context.Instance);
			if (InstalledButWrongVersion) {
				Log.DebugLine ($"Unpinning {Name} as wrong version installed (may show warnings if package isn't pinned)");
				await runner.UnPin (Name);
				return;
			}

			if (!Pin)
				return;

			Log.DebugLine ($"Pinning {Name} to version {CurrentVersion}");
			await runner.Pin (Name);
		}

		string GetPackageVersion ()
		{
			return Utilities.GetStringFromStdout (
				Context.Instance.Tools.BrewPath,
				false, // throwOnErrors
				true,  // trimTrailingWhitespace
				true,  // quietErrors
				"ls", "--versions", "-1", Name
			);
		}
	}
}
