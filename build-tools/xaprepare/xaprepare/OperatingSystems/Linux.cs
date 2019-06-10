using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	abstract partial class Linux : Unix
	{
		const string BinfmtBaseWarning = @"
Your Linux appears to have support for binfmt_misc kernel module enabled.
The module makes it possible to execute non-Linux binaries if the appropriate
interpreter for the given format is available.
Your machine is configured to handle Windows PE executables either via Mono or
Wine. It will make the Xamarin.Android build fail IF you choose to build the
Windows cross-compilers by enabling the 'mxe-Win32' or 'mxe-Win64' host targets.

You can disable the binfmt_misc module by issuing the following command as root
before building Xamarin.Android:

   echo 0 > /proc/sys/fs/binfmt_misc/status

and re-enable it after building with the following command:

   echo 1 > /proc/sys/fs/binfmt_misc/status
";

		const string FlatpakInfoPath = "/.flatpak-info";
		const string FlatpakDefaultRelease = "0.0.0";
		const string DefaultLsbReleasePath = "/usr/bin/lsb_release";

		static readonly Dictionary<string, Func<Context, Linux>> distroMap = new Dictionary<string, Func<Context, Linux>> (StringComparer.OrdinalIgnoreCase) {
			{"Debian",    (ctx) => new LinuxDebian (ctx)},
			{"Ubuntu",    (ctx) => new LinuxUbuntu (ctx)},
			{"LinuxMint", (ctx) => new LinuxMint   (ctx)},
			{"Arch",      (ctx) => new LinuxArch   (ctx)},
		};

		bool warnBinFmt;
		string codeName;

		public override string Type { get; } = "Linux";
		public override List<Program> Dependencies { get; }
		public override StringComparison DefaultStringComparison => StringComparison.Ordinal;
		public override StringComparer DefaultStringComparer => StringComparer.Ordinal;

		protected bool WarnBinFmt => warnBinFmt;
		protected string CodeName => codeName;

		protected Linux (Context context)
			: base (context)
		{
			Flavor = "Generic";
			ZipExtension = "tar.bz2";
			Dependencies = new List<Program> ();
		}

		public override void ShowFinalNotices ()
		{
			if (!warnBinFmt)
				return;

			Log.WarningLine (showSeverity: false);
			Log.WarningLine ("*************** WARNING ***************", showSeverity: false);
			Log.WarningLine (BinfmtBaseWarning, ConsoleColor.White, showSeverity: false);
		}

		protected override bool InitOS ()
		{
			if (!base.InitOS ())
				return false;

			AntDirectory = "/usr";
			return true;
		}

		public static Linux DetectAndCreate (Context context)
		{
			string name;
			string release;
			string codeName;
			string progPath;

			if (Directory.Exists ("/app")) {
				name = "Flatpak";
				release = GetFlatpakRelease ();
				codeName = String.Empty;
			} else {
				if (File.Exists (DefaultLsbReleasePath))
					progPath = DefaultLsbReleasePath;
				else
					progPath = FindProgram ("lsb_release", GetPathDirectories ());

				if (String.IsNullOrEmpty (progPath) || !IsExecutable (progPath, true))
					throw new InvalidOperationException ("Your Linux distribution lacks a working `lsb_release` command");

				name = Utilities.GetStringFromStdout (progPath, "-is");
				release = Utilities.GetStringFromStdout (progPath, "-rs");
				codeName = Utilities.GetStringFromStdout (progPath, "-cs");
			}

			Func<Context, Linux> creator;
			if (!distroMap.TryGetValue (name, out creator))
				throw new InvalidOperationException ($"Your Linux distribution ({name} {release}) is not supported at this time.");

			Linux linux = creator (context);
			linux.Name = name;
			linux.Release = release;
			linux.warnBinFmt = ShouldWarnAboutBinfmt ();
			linux.codeName = codeName;

			Log.Instance.Todo ("Check Mono version and error out if not the required minimum version");
			return linux;
		}

		static bool ShouldWarnAboutBinfmt ()
		{
			const string procDir = "/proc/sys/fs/binfmt_misc";
			if (!Directory.Exists (procDir))
				return false;

			foreach (string trouble in new [] { "cli", "win" }) {
				if (File.Exists ($"{procDir}/{trouble}"))
					return true;
			}

			return false;
		}

		static string GetFlatpakRelease ()
		{
			if (!File.Exists (FlatpakInfoPath)) {
				Log.Instance.WarningLine ($"Unable to determine Flatpak release ({FlatpakInfoPath} does not exist)");
				return FlatpakDefaultRelease;
			}

			string[] lines = File.ReadAllLines (FlatpakInfoPath);
			foreach (string l in lines) {
				string line = l?.Trim ();
				if (String.IsNullOrEmpty (line))
					continue;

				if (!line.StartsWith ("flatpak-version", StringComparison.Ordinal))
					continue;

				string[] parts = line.Split (new [] { '=' }, 2);
				if (parts.Length != 2) {
					Log.Instance.WarningLine ($"Invalid version format in {FlatpakInfoPath}");
					return FlatpakDefaultRelease;
				}

				return parts [1];
			}

			Log.Instance.WarningLine ($"Unable to find Flatpak version information in {FlatpakInfoPath}");
			return FlatpakDefaultRelease;
		}
	}
}
