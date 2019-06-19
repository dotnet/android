using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kajabity.Tools.Java;

namespace Xamarin.Android.Prepare
{
	class Step_Android_SDK_NDK : StepWithDownloadProgress
	{
		class AndroidPackage
		{
			public AndroidToolchainComponent Component;
			public string PackageName;
			public Uri Url;
			public string LocalPackagePath;
			public string DestinationDir;
		}

		bool RefreshSdk = false;
		bool RefreshNdk = false;

		public Step_Android_SDK_NDK ()
			: base ("Preparing Android SDK and NDK")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string sdkRoot = context.Properties.GetRequiredValue (KnownProperties.AndroidSdkDirectory);
			string ndkRoot = context.Properties.GetRequiredValue (KnownProperties.AndroidNdkDirectory);
			string packageCacheDir = context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory);

			RefreshSdk = context.ComponentsToRefresh.HasFlag (RefreshableComponent.AndroidSDK);
			RefreshNdk = context.ComponentsToRefresh.HasFlag (RefreshableComponent.AndroidNDK);

			Log.StatusLine ("Android SDK location: ", sdkRoot, tailColor: Log.DestinationColor);
			Log.StatusLine ("Android NDK location: ", ndkRoot, tailColor: Log.DestinationColor);
			Log.DebugLine ($"Toolchain cache directory: {packageCacheDir}");

			var toolchain = new AndroidToolchain ();
			var toInstall = new List <AndroidPackage> ();

			toolchain.Components.ForEach (c => Check (context, packageCacheDir, sdkRoot, c, toInstall, 4));
			if (toInstall.Count == 0)
				return GatherNDKInfo (context, ndkRoot);

			Log.MessageLine ();
			toInstall.ForEach (p => Log.DebugLine ($"Missing Android component: {p.Component.Name}"));

			string tempDir = Path.Combine (context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainDirectory), "temp");
			Log.DebugLine ($"Toolchain temporary directory: {tempDir}");

			if (Directory.Exists (tempDir)) {
				Log.DebugLine ("Temporary directory exists, cleaning it up");
				Utilities.DeleteDirectorySilent (tempDir);
			}
			Directory.CreateDirectory (tempDir);

			Log.MessageLine ("Installing missing components");
			var toDownload = new List <AndroidPackage> ();
			toInstall.ForEach (c => CheckPackageStatus (context, packageCacheDir, c, toDownload));

			if (toDownload.Count > 0) {
				ulong totalDownloadSize = 0;
				foreach (AndroidPackage pkg in toDownload) {
					Log.DebugLine ($"Android component '{pkg.Component.Name}' will be downloaded from {pkg.Url}");
					(bool success, ulong size) = await Utilities.GetDownloadSize (pkg.Url);
					if (!success)
						continue;
					totalDownloadSize += size;
				}

				toDownload.ForEach (p => Log.StatusLine ($"  {context.Characters.Link} {p.Url}", ConsoleColor.White));

				DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, totalDownloadSize, context.InteractiveSession);
				await Task.WhenAll (toDownload.Select (p => Download (context, p.Url, p.LocalPackagePath, p.Component.Name, p.PackageName, downloadStatus)));
			}

			foreach (AndroidPackage p in toInstall) {
				await Unpack (context, tempDir, p);
			}

			if (!AcceptLicenses (context, sdkRoot)) {
				Log.ErrorLine ("Failed to accept Android SDK licenses");
				return false;
			}

			return GatherNDKInfo (context, ndkRoot);
		}

		bool AcceptLicenses (Context context, string sdkRoot)
		{
			string sdkManager = context.OS.Which (Path.Combine (sdkRoot, "tools", "bin", "sdkmanager"));
			string jdkDir = context.OS.JavaHome;

			Log.Todo ("Modify ProcessRunner to allow standard input writing and switch to it here");
			// var runner = new ProcessRunner (sdkManager, "--licenses");
			// runner.StartInfoCallback = (ProcessStartInfo psi) => {
			// 	if (String.IsNullOrEmpty (jdkDir))
			// 		psi.EnvironmentVariables.Add ("JAVA_HOME", jdkDir);
			// 	psi.RedirectStandardInput = true;
			// };

			var psi = new ProcessStartInfo (sdkManager, "--licenses") {
				UseShellExecute = false,
				RedirectStandardInput = true
			};
			if (String.IsNullOrEmpty (jdkDir) && !psi.EnvironmentVariables.ContainsKey ("JAVA_HOME"))
				psi.EnvironmentVariables.Add ("JAVA_HOME", jdkDir);

			Log.DebugLine ($"Starting {psi.FileName} {psi.Arguments}");
			var proc = Process.Start (psi);
			for (int i = 0; i < 10; i++)
				proc.StandardInput.WriteLine ('y');
			proc.WaitForExit ();

			return true;
		}

		bool GatherNDKInfo (Context context, string ndkRoot)
		{
			return context.BuildInfo.GatherNDKInfo (context, ndkRoot);
		}

		void CheckPackageStatus (Context context, string packageCacheDir, AndroidPackage pkg, List <AndroidPackage> toDownload)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} Installing ", pkg.Component.Name, tailColor: ConsoleColor.White);

			if (File.Exists (pkg.LocalPackagePath)) {
				bool valid = Utilities.VerifyArchive (pkg.LocalPackagePath).GetAwaiter ().GetResult ();
				if (!valid || (RefreshSdk && !IsNdk (pkg.Component)) || (RefreshNdk && IsNdk (pkg.Component))) {
					if (valid)
						LogStatus ("Reinstall requested, deleting cache", 4, ConsoleColor.Magenta);
					else
						LogStatus ("Downloaded package is invalid, re-download required. Deleting cache.", 4, ConsoleColor.Magenta);
					Utilities.DeleteFile (pkg.LocalPackagePath);
					toDownload.Add (pkg);
				} else {
					Log.DebugLine ($"Component '{pkg.Component.Name}' package exists: {pkg.LocalPackagePath}");
					LogStatus ("already downloaded", 4, Log.InfoColor);
				}
			} else {
				Log.DebugLine ($"Component '{pkg.Component.Name}' package not downloaded yet: {pkg.LocalPackagePath}");
				LogStatus ("not downloaded yet", 4, ConsoleColor.Magenta);
				toDownload.Add (pkg);
			}
		}

		async Task Unpack (Context context, string tempDirRoot, AndroidPackage pkg)
		{
			Log.StatusLine (PadStatus ($"unpacking {pkg.PackageName} to ", 4), pkg.DestinationDir, tailColor: Log.DestinationColor);

			string sevenZip = context.Tools.SevenZipPath;
			Log.DebugLine ($"7z binary path: {sevenZip}");

			string tempDir = Path.Combine (tempDirRoot, Path.GetRandomFileName ());

			if (!await Utilities.Unpack (pkg.LocalPackagePath, tempDir)) {
				Utilities.DeleteFileSilent (pkg.LocalPackagePath);
				throw new InvalidOperationException ($"Failed to unpack {pkg.LocalPackagePath}");
			}

			if (pkg.Component.NoSubdirectory) {
				Utilities.MoveDirectoryContentsRecursively (tempDir, pkg.DestinationDir);
				return;
			}

			// There should be just a single subdirectory
			List<string> subdirs = Directory.EnumerateDirectories (tempDir).ToList ();
			if (subdirs.Count > 1)
				throw new InvalidOperationException ($"Unexpected contents layout of Android component '{pkg.Component.Name}' - expected a single subdirectory, instead found {subdirs.Count}");

			Utilities.MoveDirectoryContentsRecursively (subdirs [0], pkg.DestinationDir);
		}

		Uri GetPackageUrl (AndroidToolchainComponent component, string packageName)
		{
			Uri packageUrl;

			if (component.RelativeUrl != null)
				packageUrl = new Uri (AndroidToolchain.AndroidUri, component.RelativeUrl);
			else
				packageUrl = AndroidToolchain.AndroidUri;

			return new Uri (packageUrl, packageName);
		}

		string GetDestinationDir (AndroidToolchainComponent component, string sdkRoot)
		{
			string path = component.DestDir;
			if (!Path.IsPathRooted (path))
				return Path.Combine (sdkRoot, path);
			return path;
		}

		void Check (Context context, string packageCacheDir, string sdkRoot, AndroidToolchainComponent component, List <AndroidPackage> toInstall, int padLeft)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} Checking ", component.Name, tailColor: ConsoleColor.White);

			const string statusMissing = "missing";
			const string statusOutdated = "outdated";
			const string statusInstalled = "installed";

			string path = GetDestinationDir (component, sdkRoot);

			Log.DebugLine ($"Checking if {component.Name} exists in {path}");
			bool missing;
			if (IsInstalled (component, path, out missing)) {
				LogStatus (statusInstalled, padLeft, Log.InfoColor);
				return;
			}

			if (missing)
				LogStatus (statusMissing, padLeft, ConsoleColor.Magenta);
			else
				LogStatus (statusOutdated, padLeft, ConsoleColor.DarkYellow);

			string packageName = $"{component.Name}.zip";
			var pkg = new AndroidPackage {
				Component = component,
				PackageName = packageName,
				Url = GetPackageUrl (component, packageName),
				LocalPackagePath = Path.Combine (packageCacheDir, packageName),
				DestinationDir = GetDestinationDir (component, sdkRoot),
			};

			toInstall.Add (pkg);
		}

		bool IsInstalled (AndroidToolchainComponent component, string path, out bool missing)
		{
			missing = true;
			if (!Directory.Exists (path)) {
				Log.DebugLine ($"Component '{component.Name}' directory does not exist: {path}");
				return false;
			}

			// This is just a cursory check, we might want to check versions
			string propsFile = Path.Combine (path, "source.properties");
			if (!File.Exists (propsFile)) {
				Log.DebugLine ($"Component '{component.Name}' properties file does not exist: {propsFile}");
				return false;
			}

			missing = false;
			if ((RefreshSdk && !IsNdk (component)) || (RefreshNdk && IsNdk (component))) {
				Log.DebugLine ($"A reinstall has been requested for component '{component.Name}'");
				return false;
			}

			if (String.IsNullOrEmpty (component.PkgRevision)) {
				Log.DebugLine ($"Component '{component.Name}' does not specify required Pkg.Revision, assuming it's valid");
				return true;
			}

			Log.DebugLine ($"Component '{component.Name}' requires Pkg.Revision to be '{component.PkgRevision}', verifying");
			var props = new JavaProperties ();
			try {
				using (var fs = File.OpenRead (propsFile)) {
					props.Load (fs);
				}
			} catch (Exception ex) {
				Log.DebugLine ($"Failed to read '{component.Name}' source.properties. Assuming invalid version, component will be reinstalled.");
				Log.DebugLine (ex.ToString ());
				return false;
			}

			string pkgRevision = props.GetProperty ("Pkg.Revision", String.Empty);
			if (String.IsNullOrEmpty (pkgRevision)) {
				Log.DebugLine ($"Component '{component.Name}' does not have Pkg.Revision in its source.properties file, it will be reinstalled.");
				return false;
			}

			if (!ParseVersion (pkgRevision, out Version pkgVer)) {
				Log.DebugLine ($"Failed to parse a valid version from Pkg.Revision ({pkgRevision}) for component '{component.Name}'. Component will be reinstalled.");
				return false;
			}

			if (!ParseVersion (component.PkgRevision, out Version expectedPkgVer))
				throw new InvalidOperationException ($"Invalid expected package version for component '{component.Name}': {component.PkgRevision}");

			bool equal = pkgVer == expectedPkgVer;
			if (!equal)
				Log.DebugLine ($"Installed version of '{component.Name}' ({pkgVer}) is different than the required one ({expectedPkgVer})");

			return equal;
		}

		bool ParseVersion (string v, out Version version)
		{
			string ver = v?.Trim ();
			version = null;
			if (String.IsNullOrEmpty (ver))
				return false;

			if (ver.IndexOf ('.') < 0)
				ver = $"{ver}.0";

			if (Version.TryParse (ver, out version))
				return true;

			return false;
		}

		bool IsNdk (AndroidToolchainComponent component)
		{
			return component.Name.StartsWith ("android-ndk", StringComparison.OrdinalIgnoreCase);
		}
	}
}
