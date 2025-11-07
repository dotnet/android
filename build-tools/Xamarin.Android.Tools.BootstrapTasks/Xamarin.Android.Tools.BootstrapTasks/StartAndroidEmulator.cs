using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class StartAndroidEmulator : Task
	{
		[Output]
		public                  string          AdbTarget       {get; set;}

		[Output]
		public                  int             EmulatorProcessId       {get; set;}

		/// <summary>
		/// Specifies $ANDROID_HOME and $ANDROID_SDK_ROOT. This is the path to the Android SDK.
		/// </summary>
		[Required]
		public                  string          AndroidSdkDirectory     {get; set;}

		/// <summary>
		/// Specifies $ANDROID_PREFS_ROOT. This is not the path to the Android SDK, but a root folder that contains the `.android` folder.
		/// </summary>
		[Required]
		public                  string          AvdManagerHome  {get; set;}
		public                  string          Port            {get; set;}
		public                  string          ImageName       {get; set;} = "XamarinAndroidTestRunner64";
		public                  string          Arguments       {get; set;}
		public                  string          ToolPath        {get; set;}
		public                  string          ToolExe         {get; set;}
		public                  string          LogcatFile      {get; set;}
		public                  int             MaxRetries      {get; set;} = 3;

		public override bool Execute ()
		{
			string emulatorPath = GetEmulatorPath ();
			if (string.IsNullOrEmpty (emulatorPath)) {
				return false;
			}

			bool ranSuccessfully = false;
			int attempt = 0;

			while (attempt < MaxRetries && !ranSuccessfully) {
				attempt++;
				if (attempt > 1) {
					Log.LogMessage (MessageImportance.High, $"Retrying emulator start (attempt {attempt} of {MaxRetries})...");
					// Wait a bit before retrying
					Thread.Sleep (2000);
				}

				ranSuccessfully = Run (emulatorPath, out bool shouldRetry);

				if (!ranSuccessfully && shouldRetry && attempt < MaxRetries) {
					Log.LogMessage (MessageImportance.High, $"Emulator failed to start due to transient error. Will retry...");
					continue;
				}

				if (!ranSuccessfully && !shouldRetry) {
					// Non-retryable error, break immediately
					break;
				}
			}

			if (!string.IsNullOrEmpty (Port)) {
				AdbTarget   = $"-s emulator-{Port}";
			}

			return ranSuccessfully && !Log.HasLoggedErrors;
		}

		string GetEmulatorPath ()
		{
			if (string.IsNullOrEmpty (ToolExe))
				ToolExe = "emulator";

			var dirs = string.IsNullOrEmpty (ToolPath)
				? null
				: new [] { ToolPath };
			string filename;
			var path = Which.GetProgramLocation (ToolExe, out filename, dirs);
			if (path == null) {
				Log.LogError ($"Could not find `emulator`. Please set the `{nameof (StartAndroidEmulator)}.{nameof (ToolExe)}` property appropriately.");
				return null;
			}
			return path;
		}

		bool Run (string emulator, out bool shouldRetry)
		{
			shouldRetry = false;

			if (emulator == null)
				return false;

			var port = string.IsNullOrEmpty (Port) ? "" : $"-port {Port}";
			var arguments = $"{Arguments ?? string.Empty} -verbose -detect-image-hang -logcat-output \"{LogcatFile}\" -no-audio -no-snapshot -cache-size 512 -change-locale en-US -timezone \"Etc/UTC\" {port} -avd {ImageName}";
			Log.LogMessage ($"Tool {emulator} execution started with arguments: {arguments}");
			var psi = new ProcessStartInfo () {
				FileName                = emulator,
				Arguments               = arguments,
				UseShellExecute         = false,
				CreateNoWindow          = true,
				RedirectStandardOutput  = true,
				RedirectStandardError   = true,
				WindowStyle             = ProcessWindowStyle.Hidden,
			};

			Log.LogMessage (MessageImportance.Low, $"Environment variables being passed to the tool:");
			var p = new Process () {
				StartInfo = psi,
			};
			psi.EnvironmentVariables ["ANDROID_HOME"]       = AndroidSdkDirectory;
			psi.EnvironmentVariables ["ANDROID_PREFS_ROOT"] = AvdManagerHome;
			psi.EnvironmentVariables ["ANDROID_SDK_ROOT"]   = AndroidSdkDirectory;
			Log.LogMessage (MessageImportance.Low, $"\tANDROID_HOME=\"{psi.EnvironmentVariables ["ANDROID_HOME"]}\"");
			Log.LogMessage (MessageImportance.Low, $"\tANDROID_PREFS_ROOT=\"{psi.EnvironmentVariables ["ANDROID_PREFS_ROOT"]}\"");
			Log.LogMessage (MessageImportance.Low, $"\tANDROID_SDK_ROOT=\"{psi.EnvironmentVariables ["ANDROID_SDK_ROOT"]}\"");

			var sawError        = new AutoResetEvent (false);
			bool isRetryableError = false;

			DataReceivedEventHandler output = null;
			output = (o, e) => {
				Log.LogMessage (MessageImportance.Low, $"[emulator stdout] {e.Data}");
				if (string.IsNullOrWhiteSpace (e.Data))
					return;
				if (e.Data.StartsWith ("Hax ram_size", StringComparison.Ordinal) &&
						e.Data.EndsWith (" 0x0", StringComparison.Ordinal)) {
					Log.LogError ("Emulator failed to start: ram_size is 0MB! Please re-install HAXM.");
					sawError.Set ();
				}
				if (e.Data.IndexOf ("ERROR:", StringComparison.Ordinal) >= 0) {
					Log.LogError ($"Emulator failed to start: {e.Data}");
					sawError.Set ();
				}
			};
			DataReceivedEventHandler error = null;
			error = (o, e) => {
				Log.LogMessage (MessageImportance.Low, $"[emulator stderr] {e.Data}");
				if (string.IsNullOrWhiteSpace (e.Data))
					return;
				if (e.Data.IndexOf ("failed to initialize WHPX", StringComparison.OrdinalIgnoreCase) >= 0 ||
						e.Data.IndexOf ("failed to initialize HAX", StringComparison.OrdinalIgnoreCase) >= 0 ||
						e.Data.StartsWith ("Unknown hax vcpu return", StringComparison.Ordinal) ||
						e.Data.Contains ("Internal error")) {
					Log.LogWarning ($"Emulator failed to start: {e.Data}");
					isRetryableError = true;
					sawError.Set ();
					return;
				}
				if (e.Data.StartsWith ("Failed to sync", StringComparison.Ordinal)) {
					Log.LogError ($"Emulator failed to start: {e.Data}");
					Log.LogError ($"Do you have another VM running on the machine? If so, please try exiting the VM and try again.");
					sawError.Set ();
				}
				// The following may not be fatal:
				// [emulator stderr] eglMakeCurrent failed in binding subwindow!
				if (e.Data.IndexOf ("ERROR:", StringComparison.Ordinal) >= 0 ||
						(e.Data.IndexOf (" failed ", StringComparison.Ordinal) >= 0 && e.Data.IndexOf ("eglMakeCurrent", StringComparison.Ordinal) == -1)) {
					Log.LogError ($"Emulator failed to start: {e.Data}");
					sawError.Set ();
				}
			};

			p.OutputDataReceived  += output;
			p.ErrorDataReceived   += error;

			try {
				p.Start ();
				EmulatorProcessId = p.Id;
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();

				const int Timeout = 20*1000;
				int i = WaitHandle.WaitAny (new[]{sawError}, millisecondsTimeout: Timeout);
				if (i == 0 || Log.HasLoggedErrors) {
					p.Kill ();
					shouldRetry = isRetryableError;
					return false;
				}
			} finally {
				p.CancelOutputRead ();
				p.CancelErrorRead ();
				p.OutputDataReceived  -= output;
				p.ErrorDataReceived   -= error;
			}
			return true;
		}

	}
}
