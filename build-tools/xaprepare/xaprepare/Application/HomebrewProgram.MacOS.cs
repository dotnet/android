using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class HomebrewProgram : Program, IBuildInventoryItem
	{
		static readonly char[] lineSplit = new [] { '\n' };

		string? cachedVersionOutput;
		bool brewNeedsSudo = false;
		bool multipleVersionsPickle = false;

		public override bool NeedsSudoToInstall => brewNeedsSudo;
		public string HomebrewTapName           { get; } = String.Empty;
		public Uri? HomebrewFormulaUrl           { get; }
		public bool Pin                         { get; set; }

		public string BuildToolName => Name;

		public string BuildToolVersion => CurrentVersion;

		public HomebrewProgram (string homebrewPackageName, string? executableName = null)
			: this (homebrewPackageName, homebrewTap: null, executableName: executableName)
		{}

		public HomebrewProgram (string homebrewPackageName, Uri homebrewFormulaUrl, string? executableName = null)
			: this (homebrewPackageName, homebrewTap: null, executableName: executableName)
		{
			HomebrewFormulaUrl = homebrewFormulaUrl ?? throw new ArgumentNullException (nameof (homebrewFormulaUrl));
		}

		public HomebrewProgram (string homebrewPackageName, string? homebrewTap, string? executableName)
		{
			if (String.IsNullOrEmpty (homebrewPackageName))
				throw new ArgumentException ("must not be null or empty", nameof (homebrewPackageName));
			Name = homebrewPackageName;
			HomebrewTapName = homebrewTap?.Trim () ?? String.Empty;
			ExecutableName = executableName?.Trim () ?? String.Empty;
		}

		public override async Task<bool> Install ()
		{
			var runner = new BrewRunner (Context.Instance);
			if (!String.IsNullOrEmpty (HomebrewTapName)) {
				if (!await runner.Tap (HomebrewTapName))
					return false;
			}

			bool install = !InstalledButWrongVersion;
			bool success;
			if (multipleVersionsPickle) {
				Log.InfoLine ($"{Name} has multiple versions installed, let's get out of this pickle");
				// 1. unpin
				success = await runner.UnPin (Name);

				// 2. unlink
				success = await runner.Unlink (Name);

				// 3. uninstall --ignore-dependencies
				success = await runner.Uninstall (Name, ignoreDependencies: true, force: true);
				install = true;
			}

			string installName = HomebrewFormulaUrl != null ? HomebrewFormulaUrl.ToString () : Name;
			if (!install) {
				success = await runner.Upgrade (installName);
			} else
				success = await runner.Install (installName);

			await DetermineCurrentVersion ();
			AddToInventory ();

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

		protected override bool ForceReinstall ()
		{
			Log.DebugLine ($"ForceReinstall called, pickle? {multipleVersionsPickle}");
			return multipleVersionsPickle;
		}

		protected override bool ParseVersion (string? version, out Version? ver)
		{
			if (base.ParseVersion (version, out ver))
				return true;

			ver = null;
			if (String.IsNullOrEmpty (version))
				return false;

			// It is sometimes possible (if one tries hard) to have two versions of the same package installed.
			// In such instances brew will report *all* the versions in a single line, e.g.:
			//
			//   $ brew ls --versions -1 mingw-w64
			//   mingw-w64 7.0.0_1 6.0.0_1
			//
			// We need to handle it here *and* on install time (see Install above)

			int pos = version!.IndexOf (' ');
			if (pos > 0) {
				Log.DebugLine ($"Brew reported more than one version of {Name} is installed: {version}");
				multipleVersionsPickle = true;
				var versions = new List<Version> ();
				foreach (string v in version.Split (' ')) {
					string cvs = GetCleanedUpVersion (v);
					if (Version.TryParse (cvs, out Version? tempVer) && tempVer != null) {
						versions.Add (tempVer);
					}
				}

				if (versions.Count == 0) {
					Log.DebugLine ($"Failed to parse any valid versions for {Name} from {version}");
					return false;
				}

				versions.Sort ();
				ver = versions [0];
				Log.DebugLine ($"Will use version {ver}");
				return true;
			}

			string cv = GetCleanedUpVersion (version);
			if (Version.TryParse (cv, out ver))
				return true;

			Log.DebugLine ($"Failed to parse {Name} version {version}");
			return false;

			string GetCleanedUpVersion (string inVer)
			{
				// Some brew packages (e.g. mingw-w64) have "weird" version formats, we'll handle them here on the
				// case-by-case basis. First we should try some general rules to handle the weird versions, checking package
				// name should be the very last resort.
				pos =  inVer.IndexOf ('_');
				if (pos < 0)
					return inVer;
				return inVer.Replace ('_', '.');
			}
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

			string[] parts = cachedVersionOutput!.Split (new [] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
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
			if (multipleVersionsPickle) {
				Log.DebugLine ($"Multiple versions of {Name} are installed, forcing reinstallation");
				return false;
			}

			return true;
		}

		protected override async Task AfterDetect (bool installed)
		{
			if (!installed)
				return;

			AddToInventory ();

			var runner = new BrewRunner (Context.Instance);
			if (InstalledButWrongVersion) {
				Log.DebugLine ($"Unpinning {Name} as wrong version installed (may show warnings if package isn't pinned)");
				await runner.UnPin (Name);
				return;
			}

			// It may happen that the package is installed but not linked to the prefix that's in the user's PATH.
			// Detecting whether the package is linked would require requesting and parsing JSON for all the packages which
			// would be more trouble than it's worth. Let's just link the package
			await runner.Link (Name, echoOutput: false, echoError: false);

			if (!Pin)
				return;

			Log.DebugLine ($"Pinning {Name} to version {CurrentVersion}");
			await runner.Pin (Name);
		}

		string GetPackageVersion ()
		{
			string output = Utilities.GetStringFromStdout (
				Context.Instance.Tools.BrewPath,
				false, // throwOnErrors
				true,  // trimTrailingWhitespace
				true,  // quietErrors
				"ls", "--versions", Name
			);

			if (String.IsNullOrEmpty (output))
				return output;

			string[] lines = output.Split (lineSplit, StringSplitOptions.RemoveEmptyEntries);
			if (lines.Length == 0)
				return String.Empty;
			return lines [0];
		}

		public void AddToInventory ()
		{
			if (!string.IsNullOrEmpty (BuildToolName) && !string.IsNullOrEmpty (BuildToolVersion) && !Context.Instance.BuildToolsInventory.ContainsKey (BuildToolName)) {
				Context.Instance.BuildToolsInventory.Add (BuildToolName, BuildToolVersion);
			}
		}
	}
}
