using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	public partial class FastDeploy2
	{
		// The high-level device operations below are implemented directly on top of `adb`
		// (via RunAdbCommand / RunAdbShellCommand) so that FastDeploy2 does not depend on the
		// legacy Mono.AndroidTools / Xamarin.AndroidTools assemblies.

		/// <summary>
		/// Reads a single system property via <c>adb shell getprop &lt;name&gt;</c>.
		/// Returns a trimmed value, or an empty string when the property is unset.
		/// </summary>
		async Task<string> GetDeviceProperty (string name)
		{
			var result = await RunAdbShellCommand ("getprop", name);
			return result.Output?.Trim () ?? "";
		}

		/// <summary>
		/// Returns the process id of <paramref name="packageName"/> via <c>adb shell pidof</c>,
		/// or <c>0</c> when the process is not running.
		/// </summary>
		async Task<int> GetProcessId (string packageName)
		{
			var result = await RunAdbShellCommand ("pidof", packageName);
			string output = result.Output?.Trim () ?? "";
			// `pidof` can return multiple, space-separated pids; take the first one.
			int space = output.IndexOf (' ');
			if (space >= 0) {
				output = output.Substring (0, space);
			}
			return int.TryParse (output, out int pid) ? pid : 0;
		}

		/// <summary>
		/// Force-stops <paramref name="packageName"/> via <c>adb shell am force-stop</c>.
		/// </summary>
		async Task ForceStopPackage (string packageName)
		{
			await RunAdbShellCommand ("am", "force-stop", packageName);
		}

		/// <summary>
		/// Uninstalls <paramref name="packageName"/> via <c>adb shell pm uninstall</c>, optionally
		/// preserving the application's data and cache directories (<c>-k</c>).
		/// </summary>
		async Task UninstallPackage (string packageName, bool preserveData, string user)
		{
			var args = new List<string> { "pm", "uninstall" };
			if (preserveData) {
				args.Add ("-k");
			}
			if (!string.IsNullOrEmpty (user)) {
				args.Add ("--user");
				args.Add (user);
			}
			args.Add (packageName);
			await RunLoggedDeviceOperation ($"UninstallPackage {string.Join (" ", args)}", () => RunAdbShellCommand (args.ToArray ()));
		}

		/// <summary>
		/// Installs an APK via <c>adb install</c>. On an "already exists" or "requires uninstall"
		/// failure the package is uninstalled (optionally preserving data) and the install is
		/// retried once, mirroring the legacy behavior. Any other failure throws a
		/// <see cref="FastDeployInstallException"/> carrying the matching <c>ADB####</c> error code.
		/// </summary>
		async Task InstallApkWithRetry (string apkFile, bool reinstall, bool testOnly, string user)
		{
			var result = await RunInstallCommand (apkFile, reinstall, testOnly, user);
			var kind = ClassifyInstallResult (result);
			if (kind == InstallResultKind.Success) {
				return;
			}

			if (kind == InstallResultKind.AlreadyExists || kind == InstallResultKind.RequiresUninstall) {
				bool preserveData = kind == InstallResultKind.AlreadyExists;
				LogDebugMessage ($"Package '{PackageName}' could not be installed directly ({kind}). Uninstalling (preserving data: {preserveData}) and retrying.");
				await UninstallPackage (PackageName, preserveData: preserveData, user: user);
				result = await RunInstallCommand (apkFile, reinstall: true, testOnly: testOnly, user: user);
				kind = ClassifyInstallResult (result);
				if (kind == InstallResultKind.Success) {
					return;
				}
			}

			throw new FastDeployInstallException (GetInstallErrorCode (kind), result.Output);
		}

		async Task<AdbCommandResult> RunInstallCommand (string apkFile, bool reinstall, bool testOnly, string user)
		{
			var args = new List<string> { "install" };
			if (reinstall) {
				args.Add ("-r");
			}
			// Allow downgrade: matches the legacy AllowDowngrade flag, which was always set on
			// API 19+ (every device supported by .NET for Android).
			args.Add ("-d");
			if (testOnly) {
				args.Add ("-t");
			}
			if (!string.IsNullOrEmpty (user)) {
				args.Add ("--user");
				args.Add (user);
			}
			args.Add (GetFullPath (apkFile));
			var operation = $"Install ApkFile={apkFile}, PackageName={PackageName}, ReInstall={reinstall}, User={user ?? ""}, TestOnly={testOnly}";
			return await RunLoggedDeviceOperation (operation, () => RunAdbCommand (args.ToArray ()));
		}

		enum InstallResultKind
		{
			Success,
			AlreadyExists,
			RequiresUninstall,
			IncompatibleCpuAbi,
			SdkNotSupported,
			InsufficientSpace,
			Failed,
		}

		/// <summary>
		/// Classifies <c>adb install</c> output, mirroring the failure categories that the legacy
		/// install path raised as typed exceptions.
		/// </summary>
		static InstallResultKind ClassifyInstallResult (AdbCommandResult result)
		{
			string output = result.Output ?? "";

			// `adb install` prints "Success" on success; it can also be empty on success.
			if (output.IndexOf ("Success", StringComparison.Ordinal) >= 0) {
				return InstallResultKind.Success;
			}

			// NOTE: match without the trailing ']', since adb may print either
			//       [INSTALL_FAILED_NO_MATCHING_ABIS] or [INSTALL_FAILED_NO_MATCHING_ABIS: ...].
			if (output.Contains ("[INSTALL_FAILED_INSUFFICIENT_STORAGE") || output.Contains ("[INSTALL_FAILED_MEDIA_UNAVAILABLE")) {
				return InstallResultKind.InsufficientSpace;
			}
			if (output.Contains ("[INSTALL_FAILED_ALREADY_EXISTS")) {
				return InstallResultKind.AlreadyExists;
			}
			if (output.Contains ("[INSTALL_FAILED_OLDER_SDK")) {
				return InstallResultKind.SdkNotSupported;
			}
			if (output.Contains ("[INSTALL_PARSE_FAILED_INCONSISTENT_CERTIFICATES") ||
					output.Contains ("doesn't support runtime permissions") ||
					output.Contains ("[INSTALL_FAILED_UPDATE_INCOMPATIBLE") ||
					output.Contains ("[INSTALL_FAILED_VERSION_DOWNGRADE")) {
				return InstallResultKind.RequiresUninstall;
			}
			if (output.Contains ("[INSTALL_FAILED_CPU_ABI_INCOMPATIBLE") || output.Contains ("[INSTALL_FAILED_NO_MATCHING_ABIS")) {
				return InstallResultKind.IncompatibleCpuAbi;
			}

			if (result.ExitCode != 0 ||
					output.IndexOf ("Failure", StringComparison.OrdinalIgnoreCase) >= 0 ||
					output.IndexOf ("INSTALL_FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
					output.IndexOf ("adb: failed", StringComparison.OrdinalIgnoreCase) >= 0) {
				return InstallResultKind.Failed;
			}

			// Exit code 0 with no recognizable failure marker: treat as success.
			return InstallResultKind.Success;
		}

		static string GetInstallErrorCode (InstallResultKind kind)
		{
			return kind switch {
				InstallResultKind.IncompatibleCpuAbi => "ADB0020",
				InstallResultKind.RequiresUninstall => "ADB0030",
				InstallResultKind.SdkNotSupported => "ADB0040",
				InstallResultKind.AlreadyExists => "ADB0050",
				InstallResultKind.InsufficientSpace => "ADB0060",
				_ => "ADB0010",
			};
		}

		/// <summary>Quotes an argument for <see cref="System.Diagnostics.ProcessStartInfo.Arguments"/>.</summary>
		static string QuoteProcessArgument (string argument)
		{
			if (argument == null) {
				return "\"\"";
			}
			var sb = new StringBuilder ();
			sb.Append ('"');
			// The .NET process class only supports quoted arguments with escaped quotes/backslashes.
			foreach (char c in argument) {
				if (c == '"' || c == '\\') {
					sb.Append ('\\');
				}
				sb.Append (c);
			}
			sb.Append ('"');
			return sb.ToString ();
		}
	}

	/// <summary>
	/// Thrown when <c>adb install</c> fails for a reason FastDeploy2 cannot recover from. The
	/// <see cref="ErrorCode"/> is the <c>ADB####</c> code reported to MSBuild.
	/// </summary>
	class FastDeployInstallException : Exception
	{
		public string ErrorCode { get; }

		public FastDeployInstallException (string errorCode, string message)
			: base (message)
		{
			ErrorCode = errorCode;
		}
	}
}
