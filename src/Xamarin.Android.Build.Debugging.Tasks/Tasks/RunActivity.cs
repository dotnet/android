#nullable enable
//
// RunActivity.cs
//
// Author:
//       Jonathan Pryor <jonp@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc. (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.IO;
using System.Threading;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.AndroidTools;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Run;
using Xamarin.AndroidTools;
using Xamarin.AndroidTools.Debugging;
using Xamarin.Android.Build.Debugging.Tasks.Properties;

namespace Xamarin.Android.Tasks
{
	public class RunActivity : AsyncTask, ICancelableTask
	{
		readonly CancellationTokenSource managedLaunchCancellation = new CancellationTokenSource ();

		public override string TaskPrefix => "RUNA";

		[Required]
		public string PackageName { get; set; } = "";

		[Required]
		public string ActivityName { get; set; } = "";

		public string? AdbTarget { get; set; }

		public bool AttachDebugger { get; set; }

		public bool Server { get; set; }

		public string Port { get; set; }

		public int UserID { get; set; } = 0;

		public bool ForceStop { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether Java debugging is allowed. Defaults to true, but will be available to be toggled off via -p:_AndroidAllowJavaDebugging=false.
		/// </summary>
		public bool AllowJavaDebugging { get; set; } = true;

		/// <summary>
		/// Private tri-state override for the managed launch protection transaction.
		/// Blank or true preserves the existing automatic debug-path behavior; false
		/// keeps managed debugger setup but bypasses the debug-app transaction.
		/// </summary>
		public string? EnableManagedLaunchProtection { get; set; }

		AndroidDevice? Device;

		public RunActivity ()
		{
			Port = "10000";
		}

		/// <summary>
		/// Cancels the launch while allowing a managed-only transaction to finish cleanup.
		/// </summary>
		public new void Cancel ()
		{
			// AsyncTask.Cancel exits its completion/logging pump before the worker
			// finishes. Keep that pump alive for this transaction's mutation drain
			// and cleanup; leave cancellation of the legacy paths unchanged.
			if (AttachDebugger && !AllowJavaDebugging)
				managedLaunchCancellation.Cancel ();
			else
				base.Cancel ();
		}

		public override bool Execute ()
		{
			Device = AndroidHelper.ParseTarget (AdbTarget, LogMessage, LogCodedError, logErrors: true, engine4: BuildEngine4);
			if (Device == null) {
				return false;
			}
			LogMessage ($"Found device: {Device.ID}");
			return base.Execute () && !(AttachDebugger && !AllowJavaDebugging && managedLaunchCancellation.IsCancellationRequested);
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			LogDebugMessage ($"  ActivityName: {ActivityName}");

			var device = Device;
			if (device == null)
				throw new System.InvalidOperationException ("The Android device must be initialized before running the task.");

			var amStartCommand = new AmStartCommand (PackageName, ActivityName);
			amStartCommand.ForceStop = ForceStop;
			amStartCommand.EnableDebugging = false;
			amStartCommand.User = UserID.ToString ();

			amStartCommand.Action = amStartCommand.Action ?? "android.intent.action.MAIN";
			amStartCommand.Categories = amStartCommand.Categories ?? new[] { "android.intent.category.LAUNCHER" };

			var startConfiguration = new ExecutionConfiguration (PackageName, amStartCommand);
			startConfiguration.AllowJavaDebugging = AllowJavaDebugging;
			startConfiguration.LogWiter = (s) => LogDebugMessage (s);

			if (AttachDebugger) {
				var port = int.Parse (Port);
				var ipAddress = Server ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Parse("10.0.2.2");
				startConfiguration.Debugger.Address = ipAddress;
				startConfiguration.Debugger.SdbPort = port;
				startConfiguration.Debugger.StdoutPort = -1;
				startConfiguration.Debugger.Server = Server;
				LogMessage (string.Format (Resources.StartDebugger_ipAddress_port, ipAddress, port), MessageImportance.High);
				if (AllowJavaDebugging) {
					await device.StartWithDebuggingAsync (startConfiguration, CancellationToken);
				} else if (IsManagedLaunchProtectionEnabled ()) {
					// Keep the managed-only transaction in this task, not in the
					// deprecated libraries still needed by the other launch paths.
					var component = amStartCommand.Component;
					var command = $"am start{(ForceStop ? " -S" : "")} --user {ManagedActivityLaunch.QuoteForDeviceShell (amStartCommand.User)}" +
						" -a android.intent.action.MAIN -c android.intent.category.LAUNCHER" +
						$" -n {ManagedActivityLaunch.QuoteForDeviceShell (component)}";
					try {
						await ManagedActivityLaunch.RunAsync (
							device.ID, PackageName, component, amStartCommand.User, ForceStop,
							startCommand: command, startupTimeout: startConfiguration.Debugger.Timeout,
							prepare: token => device.SetDebugPropertiesAsync (PackageName, startConfiguration.Debugger, token),
							runShellCommand: device.RunShellCommand,
							launchUnprotected: token => device.ExecuteIntentCommandAsync (amStartCommand, startConfiguration.LogWiter, token),
							log: message => LogMessage (message),
							logCleanupError: (message, error) => this.LogUnhandledException (TaskPrefix, new AdbException (message, error)),
							token: managedLaunchCancellation.Token);
					} catch (ManagedActivityLaunch.CommandFailedException ex) {
						// Preserve the task's existing typed ADB launch diagnostics.
						if (ex.ActivityNotFound)
							throw new ActivityNotFoundException (ex.Message);
						throw new AdbException (ex.Message, ex);
					}
				} else {
					await device.SetDebugPropertiesAsync (PackageName, startConfiguration.Debugger, managedLaunchCancellation.Token);
					managedLaunchCancellation.Token.ThrowIfCancellationRequested ();
					await device.ExecuteIntentCommandAsync (amStartCommand, startConfiguration.LogWiter, managedLaunchCancellation.Token);
					managedLaunchCancellation.Token.ThrowIfCancellationRequested ();
				}
			} else {
				await device.StartWithoutDebuggingAsync (startConfiguration, CancellationToken);
			}
		}

		bool IsManagedLaunchProtectionEnabled () =>
			!string.Equals (EnableManagedLaunchProtection, "false", System.StringComparison.OrdinalIgnoreCase);
	}
}
