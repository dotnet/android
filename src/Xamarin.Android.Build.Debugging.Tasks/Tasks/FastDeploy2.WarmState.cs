using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xamarin.Android.Build.Debugging.Tasks.Properties;

namespace Xamarin.Android.Tasks
{
	public partial class FastDeploy2
	{
		const string WarmRedirectTag = "__XA_FD2_REDIRECT__=";
		const string WarmRunAsDisabledTag = "__XA_FD2_RUN_AS_DISABLED__=";
		const string WarmRemoteHashTag = "__XA_FD2_REMOTE_HASH__=";
		const string WarmPathTag = "__XA_FD2_PATH__=";
		const string WarmOverrideHashTag = "__XA_FD2_OVERRIDE_HASH__=";
		const string WarmPidTag = "__XA_FD2_PID__=";
		const string WarmRunAsStatusTag = "__XA_FD2_RUN_AS_STATUS__=";
		const string WarmForceStopStatusTag = "__XA_FD2_FORCE_STOP_STATUS__=";

		DeviceManifestState warmDeviceManifestState;
		string warmDeviceManifestHash = "";
		bool warmProbeStoppedApp;

		async Task<WarmStateProbeOutcome> TryRunWarmStateProbe (ManifestData previousManifest)
		{
			if (!CanRunWarmStateProbe (previousManifest)) {
				return WarmStateProbeOutcome.NotEligible;
			}

			string remoteStagingPath = GetRemoteAdbPushStagingPath ();
			string remoteMarkerPath = CombineRemotePath (remoteStagingPath, ManifestHashMarker);
			string overrideMarkerPath = CombineRemotePath (OverridePath, ManifestHashMarker);
			string packageName = QuoteShellArgument (PackageName);

			packageInfo = new PackageInfo {
				PackageName = PackageName,
				UserId = UserID,
			};
			string runAsScript =
				$"echo {QuoteShellArgument (WarmPathTag)}$(pwd); " +
				$"echo {QuoteShellArgument (WarmOverrideHashTag)}$(cat {QuoteShellArgument (overrideMarkerPath)} 2>/dev/null)";
			string runAsCommand = string.Join (" ", BuildRunAsArgs ().Concat (new [] {
				"sh",
				"-c",
				runAsScript,
			}).Select (QuoteShellArgument));
			string script =
				$"fd2_redirect=$(getprop log.redirect-stdio); echo {QuoteShellArgument (WarmRedirectTag)}$fd2_redirect; " +
				$"fd2_run_as_disabled=$(getprop ro.boot.disable_runas); echo {QuoteShellArgument (WarmRunAsDisabledTag)}$fd2_run_as_disabled; " +
				$"echo {QuoteShellArgument (WarmRemoteHashTag)}$(cat {QuoteShellArgument (remoteMarkerPath)} 2>/dev/null); " +
				"if [ \"$fd2_redirect\" != true ] && [ \"$fd2_run_as_disabled\" != true ]; then " +
				$"fd2_pid=$(pidof {packageName} 2>/dev/null || true); echo {QuoteShellArgument (WarmPidTag)}$fd2_pid; " +
				$"{runAsCommand}; fd2_run_as_status=$?; echo {QuoteShellArgument (WarmRunAsStatusTag)}$fd2_run_as_status; " +
				"fd2_force_stop_status=0; " +
				$"if [ \"$fd2_run_as_status\" -eq 0 ] && [ -n \"$fd2_pid\" ]; then am force-stop {packageName}; fd2_force_stop_status=$?; fi; " +
				$"echo {QuoteShellArgument (WarmForceStopStatusTag)}$fd2_force_stop_status; fi";

			AdbCommandResult result = await RunAdbShellCommand (script);
			WarmStateProbeData data = ParseWarmStateProbeOutput (result.Output);

			if (data.HasRedirectStdio && string.Equals ("true", data.RedirectStdio, StringComparison.OrdinalIgnoreCase)) {
				LogFastDeploy2Error ("XA0128", Resources.XA0128_RedirectStdioIsEnabled);
				return WarmStateProbeOutcome.Failed;
			}
			if (data.HasRunAsDisabled && string.Equals ("true", data.RunAsDisabled, StringComparison.OrdinalIgnoreCase)) {
				LogFastDeploy2Error ("XA0131", Resources.XA0131_DeveloperModeNotEnabled);
				return WarmStateProbeOutcome.Failed;
			}

			if (result.ExitCode != 0 ||
					!data.HasRequiredState ||
					data.RunAsStatus != 0 ||
					data.ForceStopStatus != 0 ||
					string.IsNullOrEmpty (data.InternalPath)) {
				LogDiagnostic ($"FastDeploy2 warm-state probe was inconclusive. Falling back to detailed device checks. Output: {result.Output}");
				packageInfo = new PackageInfo ();
				return WarmStateProbeOutcome.NotEligible;
			}

			packageInfo.InternalPath = data.InternalPath;
			packageInfo.ProcessId = 0;
			warmDeviceManifestHash = ComputeManifestHash (previousManifest);
			warmDeviceManifestState = new DeviceManifestState {
				RemoteHash = data.RemoteHash,
				OverrideHash = data.OverrideHash,
			};
			warmProbeStoppedApp = true;
			LogDiagnostic ("FastDeploy2 warm-state probe completed successfully.");
			return WarmStateProbeOutcome.Ready;
		}

		bool CanRunWarmStateProbe (ManifestData previousManifest)
		{
			return previousManifest != null &&
				File.Exists (GetFullPath (UploadFlagFile)) &&
				!ReInstall &&
				!EmbedAssembliesIntoApk &&
				GetUserId () == "0" &&
				!string.IsNullOrEmpty (PackageFile) &&
				File.Exists (GetFullPath (PackageFile)) &&
				!IsPackageFileOutOfDate () &&
				((FastDevFiles?.Length ?? 0) > 0 || (EnvironmentFiles?.Length ?? 0) > 0);
		}

		internal static WarmStateProbeData ParseWarmStateProbeOutput (string output)
		{
			var data = new WarmStateProbeData ();
			foreach (string line in (output ?? "").Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
				if (TryGetTaggedValue (line, WarmRedirectTag, out string value)) {
					data.HasRedirectStdio = true;
					data.RedirectStdio = value;
				} else if (TryGetTaggedValue (line, WarmRunAsDisabledTag, out value)) {
					data.HasRunAsDisabled = true;
					data.RunAsDisabled = value;
				} else if (TryGetTaggedValue (line, WarmRemoteHashTag, out value)) {
					data.HasRemoteHash = true;
					data.RemoteHash = value;
				} else if (TryGetTaggedValue (line, WarmPathTag, out value)) {
					data.HasInternalPath = true;
					data.InternalPath = value;
				} else if (TryGetTaggedValue (line, WarmOverrideHashTag, out value)) {
					data.HasOverrideHash = true;
					data.OverrideHash = value;
				} else if (TryGetTaggedValue (line, WarmPidTag, out value)) {
					data.HasProcessId = true;
					data.ProcessId = ParseFirstProcessId (value);
				} else if (TryGetTaggedValue (line, WarmRunAsStatusTag, out value)) {
					data.HasRunAsStatus = int.TryParse (value, out int status);
					data.RunAsStatus = status;
				} else if (TryGetTaggedValue (line, WarmForceStopStatusTag, out value)) {
					data.HasForceStopStatus = int.TryParse (value, out int status);
					data.ForceStopStatus = status;
				}
			}
			return data;
		}

		static bool TryGetTaggedValue (string line, string tag, out string value)
		{
			if (line.StartsWith (tag, StringComparison.Ordinal)) {
				value = line.Substring (tag.Length).Trim ();
				return true;
			}
			value = "";
			return false;
		}

		static int ParseFirstProcessId (string value)
		{
			int space = value.IndexOf (' ');
			if (space >= 0) {
				value = value.Substring (0, space);
			}
			return int.TryParse (value, out int processId) ? processId : 0;
		}

		enum WarmStateProbeOutcome
		{
			NotEligible,
			Ready,
			Failed,
		}

		internal class WarmStateProbeData
		{
			public bool HasRedirectStdio { get; set; }
			public string RedirectStdio { get; set; } = "";
			public bool HasRunAsDisabled { get; set; }
			public string RunAsDisabled { get; set; } = "";
			public bool HasRemoteHash { get; set; }
			public string RemoteHash { get; set; } = "";
			public bool HasInternalPath { get; set; }
			public string InternalPath { get; set; } = "";
			public bool HasOverrideHash { get; set; }
			public string OverrideHash { get; set; } = "";
			public bool HasProcessId { get; set; }
			public int ProcessId { get; set; }
			public bool HasRunAsStatus { get; set; }
			public int RunAsStatus { get; set; }
			public bool HasForceStopStatus { get; set; }
			public int ForceStopStatus { get; set; }

			public bool HasRequiredState =>
				HasRedirectStdio &&
				HasRunAsDisabled &&
				HasRemoteHash &&
				HasInternalPath &&
				HasOverrideHash &&
				HasProcessId &&
				HasRunAsStatus &&
				HasForceStopStatus;
		}
	}
}
