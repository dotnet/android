using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract class Program : AppObject
	{
		const string DefaultCurrentVersion = "0.0.0";

		bool? installed;

		/// <summary>
		///   Whether the package/program needs <c>sudo</c> to install.
		/// </summary>
		public abstract bool NeedsSudoToInstall { get; }

		/// <summary>
		///   Program/package name - this may be an actual program name or a system package name. What it is affects
		///   version checking, <see cref="ExecutableName" />
		/// </summary>
		public string Name                      { get; set; }

		/// <summary>
		///   Name of an executable/binary inside a package (<see cref="Name"/>) which will be used to query the program
		///   version. If <see cref="Name"/> refers to an executable name (e.g. it coincides with the package name),
		///   this property may be left unset.
		/// </summary>
		public string ExecutableName            { get; set; }

		/// <summary>
		///   Minimum supported version of the package/program
		/// </summary>
		public string MinimumVersion            { get; set; }

		/// <summary>
		///   Maximum supported version of the package/program
		/// </summary>
		public string MaximumVersion            { get; set; }

		/// <summary>
		///   Version of the currently installed package/program, if any. If the program isn't detected, this property
		///   will be set to the default value of 0.0.0
		/// </summary>
		public string CurrentVersion            { get; protected set; } = DefaultCurrentVersion;

		public bool IgnoreMinimumVersion        { get; set; }
		public bool IgnoreMaximumVersion        { get; set; }
		public bool InstalledButWrongVersion    { get; private set; }

		public abstract Task<bool> Install ();

		public async Task<bool> IsInstalled ()
		{
			if (!installed.HasValue)
				installed = await Detect ();
			return installed.Value;
		}

		public virtual bool CanInstall ()
		{
			bool installationAllowed = Context.Instance.CheckCondition (KnownConditions.AllowProgramInstallation);
			if (!installationAllowed)
				Log.DebugLine ($"{Name} cannot be installed because program installation is disabled");

			return installationAllowed;
		}

		async Task<bool> Detect ()
		{
			if (!CheckWhetherInstalled ()) {
				await AfterDetect (false);
				return false;
			}

			bool success = await DetermineCurrentVersion ();
			if (!success) {
				Log.WarningLine ($"Unable to determine the current version of program '{Name}'");
			} else {
				InstalledButWrongVersion = !IsValidVersion ();
			}

			if (!success || String.IsNullOrEmpty (CurrentVersion)) {
				Log.DebugLine ($"Undetermined current version of program '{Name}', will default to {DefaultCurrentVersion}");
				CurrentVersion = DefaultCurrentVersion;
			}

			await AfterDetect (true);
			return true;
		}

		bool IsValidVersion ()
		{
			if (!ParseVersion (CurrentVersion, out Version curVer)) {
				Log.DebugLine ($"Unable to parse {Name} version from the string: {CurrentVersion}");
				Log.DebugLine ($"Version checks disabled for {Name}");
				return true;
			}

			if (!ParseVersion (MinimumVersion, out Version minVer))
				minVer = null;

			if (!ParseVersion (MaximumVersion, out Version maxVer))
				maxVer = null;

			if (minVer == null && maxVer == null)
				return true;

			if (!IgnoreMinimumVersion && minVer != null && curVer < minVer) {
				Log.DebugLine ($"{Name} is too old. Minimum version: {minVer}; Installed version: {curVer}");
				return false;
			}

			if (!IgnoreMaximumVersion && maxVer != null && curVer > maxVer) {
				Log.DebugLine ($"{Name} is too new. Maximum version: {maxVer}; Installed version: {curVer}");
				return false;
			}

			return true;
		}

#pragma warning disable CS1998
		protected virtual async Task AfterDetect (bool installed)
		{}
#pragma warning restore CS1998

		protected virtual bool ParseVersion (string version, out Version ver)
		{
			return Version.TryParse (version, out ver);
		}

#pragma warning disable CS1998
		protected virtual async Task<bool> DetermineCurrentVersion ()
		{
			if (String.IsNullOrEmpty (Name)) {
				Log.DebugLine ("Program name not specified, unable to check version");
				return false;
			}

			bool success;
			string version;
			string programName = ExecutableName?.Trim ();
			if (String.IsNullOrEmpty (programName))
				programName = Name;

			(success, version) = Utilities.GetProgramVersion (programName);
			if (success)
				CurrentVersion = version;

			return success;
		}
#pragma warning restore CS1998

		protected abstract bool CheckWhetherInstalled ();
	}
}
