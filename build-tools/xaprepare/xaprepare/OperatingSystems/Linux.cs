using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.Android.Prepare
{
	abstract partial class Linux : Unix
	{
		const string BinfmtBaseWarning = @"
Your Linux appears to have support for binfmt_misc kernel module enabled.
The module makes it possible to execute non-Linux binaries if the appropriate
interpreter for the given format is available.
Your machine is configured to handle Windows PE executables either via Mono or
Wine. It will make the .NET for Android build fail IF you choose to build the
Windows cross-compilers by enabling the 'mxe-Win32' or 'mxe-Win64' host targets.

You can disable the binfmt_misc module by issuing the following command as root
before building .NET for Android:

   echo 0 > /proc/sys/fs/binfmt_misc/status

and re-enable it after building with the following command:

   echo 1 > /proc/sys/fs/binfmt_misc/status
";

		const string FlatpakInfoPath = "/.flatpak-info";
		const string FlatpakDefaultRelease = "0.0.0";
		const string DefaultLsbReleasePath = "/usr/bin/lsb_release";
		const string OsReleasePath = "/etc/os-release";

		static readonly Dictionary<string, Func<Context, Linux>> distroMap = new Dictionary<string, Func<Context, Linux>> (StringComparer.OrdinalIgnoreCase) {
			{"Debian",    (ctx) => new LinuxDebian (ctx)},
			{"Ubuntu",    (ctx) => new LinuxUbuntu (ctx)},
			{"LinuxMint", (ctx) => new LinuxMint   (ctx)},
			{"Arch",      (ctx) => new LinuxArch   (ctx)},
			{"Fedora",    (ctx) => new LinuxFedora (ctx)},
			{"Gentoo",    (ctx) => new LinuxGentoo (ctx)},
		};

		static readonly Dictionary<string, string> distroIdMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
			{"debian",    "Debian"},
			{"ubuntu",    "Ubuntu"},
			{"arch",      "Arch"},
			{"linuxmint", "LinuxMint"},
			{"fedora",    "Fedora"},
			{"gentoo",    "Gentoo"},
		};

		bool warnBinFmt;
		string codeName = String.Empty;
		bool derived = false;

		public override string Type { get; } = "Linux";
		public override List<Program> Dependencies { get; }
		public override StringComparison DefaultStringComparison => StringComparison.Ordinal;
		public override StringComparer DefaultStringComparer => StringComparer.Ordinal;

		protected bool WarnBinFmt => warnBinFmt;
		protected string CodeName => codeName;
		protected bool DerivativeDistro => derived;

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
			return true;
		}

		static bool DetectFlatpak (ref string name, ref string release, ref string codeName, ref string idLike, ref string id)
		{
			if (!Directory.Exists ("/app"))
				return false;

			name = "Flatpak";
			release = GetFlatpakRelease ();
			codeName = String.Empty;

			return true;
		}

		static bool DetectLsbRelease (ref string name, ref string release, ref string codeName, ref string idLike, ref string id)
		{
			string progPath;

			if (File.Exists (DefaultLsbReleasePath))
				progPath = DefaultLsbReleasePath;
			else
				progPath = FindProgram ("lsb_release", GetPathDirectories ());

			if (String.IsNullOrEmpty (progPath) || !IsExecutable (progPath, true))
				return false;

			id = name = Utilities.GetStringFromStdout (progPath, "-is");
			release = Utilities.GetStringFromStdout (progPath, "-rs");
			codeName = Utilities.GetStringFromStdout (progPath, "-cs");

			return true;
		}

		static bool DetectOsRelease (ref string name, ref string release, ref string codeName, ref string idLike, ref string id)
		{
			if (!File.Exists (OsReleasePath))
				return false;

			foreach (string l in File.ReadLines (OsReleasePath)) {
				string line = l.Trim ();
				if (String.IsNullOrEmpty (line))
					continue;

				int idx = line.IndexOf ('=');
				if (idx < 1)
					continue;

				string fieldName = line.Substring (0, idx);
				string fieldValue = line.Substring (idx + 1).Trim ('"');
				if (String.Compare ("NAME", fieldName, StringComparison.OrdinalIgnoreCase) == 0) {
					name = fieldValue;
					continue;
				}

				if (String.Compare ("VERSION_ID", fieldName, StringComparison.OrdinalIgnoreCase) == 0) {
					release = fieldValue;
					continue;
				}

				if (String.Compare ("VERSION_CODENAME", fieldName, StringComparison.OrdinalIgnoreCase) == 0) {
					codeName = fieldValue;
					continue;
				}

				if (String.Compare ("ID", fieldName, StringComparison.OrdinalIgnoreCase) == 0) {
					id = fieldValue;
					continue;
				}

				if (String.Compare ("ID_LIKE", fieldName, StringComparison.OrdinalIgnoreCase) == 0) {
					idLike = fieldValue;
					continue;
				}
			}

			return true;
		}

		static bool MapDistro (string id, ref string distro)
		{
			if (String.IsNullOrEmpty (id))
				return false;

			if (!distroIdMap.TryGetValue (id, out string? val) || val == null) {
				return false;
			}

			distro = val;
			return true;
		}

		protected virtual bool EnsureVersionInformation (Context context)
		{
			return true;
		}

		public static Linux DetectAndCreate (Context context)
		{
			string name = String.Empty;
			string release = String.Empty;
			string codeName = String.Empty;
			string idLike = String.Empty;
			string id = String.Empty;
			bool detected = DetectFlatpak (ref name, ref release, ref codeName, ref idLike, ref id);

			if (!detected)
				detected = DetectOsRelease (ref name, ref release, ref codeName, ref idLike, ref id);;
			if (!detected)
				detected = DetectLsbRelease (ref name, ref release, ref codeName, ref idLike, ref id);

			if (!detected)
				throw new InvalidOperationException ("Unable to detect your Linux distribution");

			bool usingBaseDistro = false;
			string distro = String.Empty;
			detected = MapDistro (id, ref distro);
			if (!detected) {
				usingBaseDistro = detected = MapDistro (idLike, ref distro);
			}

			if (!detected) {
				var list = new List<string> ();

				if (!String.IsNullOrEmpty (name))
					list.Add ($"name: ${name}");
				if (!String.IsNullOrEmpty (release))
					list.Add ($"release: ${release}");
				if (!String.IsNullOrEmpty (codeName))
					list.Add ($"codename: ${codeName}");
				if (!String.IsNullOrEmpty (id))
					list.Add ($"id: ${id}");
				if (!String.IsNullOrEmpty (idLike))
					list.Add ($"id like: ${idLike}");

				string info;
				if (list.Count > 0) {
					string infoText = String.Join ("; ", list);
					info = $" Additional info: {infoText}";
				} else
					info = String.Empty;

				throw new InvalidOperationException ($"Failed to detect your Linux distribution.{info}");
			}

			if (usingBaseDistro)
				Log.Instance.InfoLine ($"Distribution supported via its base distribution: {idLike}");

			if (!distroMap.TryGetValue (distro, out Func<Context, Linux>? creator) || creator == null) {
				throw new InvalidOperationException ($"Your Linux distribution ({name} {release}) is not supported at this time.");
			}

			Linux linux = creator (context);
			linux.Name = name;
			linux.Release = release;
			linux.warnBinFmt = ShouldWarnAboutBinfmt ();
			linux.codeName = codeName;
			linux.derived = usingBaseDistro;

			if (!linux.EnsureVersionInformation (context)) {
				throw new InvalidOperationException ("Unable to detect version of your Linux distribution");
			}

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
				string line = l.Trim ();
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
