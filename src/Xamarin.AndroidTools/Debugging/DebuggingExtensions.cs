//
// DebuggingExtensions.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc
//
using System;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;
using System.Diagnostics;
using Xamarin.AndroidTools.Debugging.Java;

namespace Xamarin.AndroidTools.Debugging
{
	/// <summary>
	/// Device extension methods for initiating debugging
	/// </summary>
	public static class DebuggingExtensions
	{
		const int WAIT_BEFORE_RETRY_GET_PID = 250;
		const int WAIT_FOR_DEBUGGER_TO_ATTACH_MS = 1400;

		/// <summary>
		/// Starts the process debugging using the given execution configuration
		/// </summary>
		public async static Task StartWithDebuggingAsync(this IAndroidDevice device, ExecutionConfiguration configuration, CancellationToken token)
		{
			// TODO: refactor IAndroidDevice some more to remove casts
			var androidDevice = (AndroidDevice)device;

			await androidDevice.ExecuteAndLogCommandAsync(configuration.BeforeRunCommand, configuration.LogWiter, token).ConfigureAwait(false);

			await device.SetDebugPropertiesAsync(configuration.PackageName, configuration.Debugger, token).ConfigureAwait(false);

			// add --include-stopped-packages if it's not present on broadcast commands because otherwise we won't start anything anyway
			if (configuration.RunCommand is AmBroadcastCommand) {
				((AmBroadcastCommand)configuration.RunCommand).Flags |= IntentFlag.IncludeStoppedPackages;
			}

			bool javaDebugging = false;
			if (configuration.AllowJavaDebugging && configuration.RunCommand is AmStartCommand) {
				var cmd = ((AmStartCommand)configuration.RunCommand);
				if (androidDevice.IsWSA() || androidDevice.IsEmulator) // force -D for WSA and Emulators
					cmd.EnableDebugging = true;
				javaDebugging = cmd.EnableDebugging;
			}

			// if the startCommand is null it is because there is no activity to start, in which case
			// we will return a completed task and the user will have to manually start up the process
			if (configuration.RunCommand == null)
				return;

			if (configuration.LogWiter != null) {
				configuration.LogWiter(configuration.RunCommand.ToString());
			}

			await androidDevice.ExecuteIntentCommandAsync(configuration.RunCommand, configuration.LogWiter, token).ConfigureAwait(false);
			if (javaDebugging) {
				try {
					await androidDevice.ConnectJdwpAsync (configuration, token).ConfigureAwait(false);
				} catch (Exception ex) {
					if (configuration.LogWiter != null)
						configuration.LogWiter ($"warning: Could not connect Jdwp. {ex}");
				}
			}
		}

		/// <summary>
		/// Starts the process without debugging using the given execution configuration
		/// </summary>
		public async static Task StartWithoutDebuggingAsync(this IAndroidDevice device, ExecutionConfiguration configuration, CancellationToken token)
		{
			// TODO: refactor IAndroidDevice some more to remove casts
			var androidDevice = (AndroidDevice)device;

			await androidDevice.ExecuteAndLogCommandAsync(configuration.BeforeRunCommand, configuration.LogWiter, token).ConfigureAwait(false);

			// reset the debug timeout in case the user tries to run after debugging within the 30 second time window
			await androidDevice.SetProperty("debug.mono.extra", string.Empty, token).ConfigureAwait(false);

			// clear the fast dev property file
			await androidDevice.SetFastDevPropertyFile(configuration.PackageName, "debug.mono.extra", string.Empty, token).ConfigureAwait(false);
			token.ThrowIfCancellationRequested();

			// add --include-stopped-packages if it's not present on broadcast commands because otherwise we won't start anything anyway
			if (configuration.RunCommand is AmBroadcastCommand)
			{
				((AmBroadcastCommand)configuration.RunCommand).Flags |= IntentFlag.IncludeStoppedPackages;
			}

			if (configuration.LogWiter != null)
			{
				configuration.LogWiter(configuration.RunCommand.ToString());
			}

			await androidDevice.ExecuteIntentCommandAsync(configuration.RunCommand, configuration.LogWiter, token).ConfigureAwait(false);
		}

		/// <summary>
		/// Returns a Task which sets up the debug property and sets the fast dev property for the given package
		/// </summary>
		public async static Task SetDebugPropertiesAsync(this IAndroidDevice device, string packageName, DebuggerOptions options, CancellationToken token)
		{
			if (string.IsNullOrEmpty(packageName))
				throw new ArgumentException(nameof(packageName));

			// TODO: refactor IAndroidDevice some more to remove casts
			var androidDevice = (AndroidDevice)device;

			const int loglevel = 0;

			// Get the time the device thinks it is, and add options.Timeout seconds (defaults to 30)
			long expire_date = (await androidDevice.GetDate(token).ConfigureAwait(false)) + (int)options.Timeout.TotalSeconds;

			string endpoint = options.StdoutPort > -1
				? string.Format("{0}:{1}:{2}", options.Address, options.SdbPort, options.StdoutPort)
				: string.Format("{0}:{1}", options.Address, options.SdbPort);

			// Set property to tell the device to launch in debug mode
			string debugArg = string.Format("debug={0},timeout={1},loglevel={2},server={3}", endpoint, expire_date, loglevel, options.Server ? "y" : "n");

			await androidDevice.SetProperty("debug.mono.extra", debugArg, token).ConfigureAwait(false);

			await androidDevice.SetFastDevPropertyFile(packageName, "debug.mono.extra", debugArg, token).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes the command and writes the result to the log writer
		/// </summary>
		public static Task ExecuteAndLogCommandAsync(this IAndroidDevice device, string command, Action<string> logWriter, CancellationToken token)
		{
			// TODO: refactor IAndroidDevice some more to remove casts
			var androidDevice = (AndroidDevice)device;
			return ExecuteAndLogCommandAsync(androidDevice, command, logWriter, token);
		}

		/// <summary>
		/// Executes the command and writes the result to the log writer
		/// </summary>
		public static async Task ExecuteAndLogCommandAsync(this AndroidDevice device, string command, Action<string> logWriter, CancellationToken token)
		{
			if (!string.IsNullOrEmpty(command))
			{
				if (logWriter != null)
				{
					logWriter(command);
				}

				var cmdResult = await device.RunShellCommand(command, token).ConfigureAwait(false);
				if (logWriter != null)
				{
					logWriter(cmdResult);
				}
			}
		}

		public static bool IsWSA(this AndroidDevice androidDevice)
		{
			if (androidDevice == null)
				return false;

			var vendor = androidDevice.Properties?.Get("ro.product.vendor.brand");
			var model = androidDevice.Properties?.Get("ro.product.vendor.model");

			return !string.IsNullOrEmpty (vendor) &&
				!string.IsNullOrEmpty (model) &&
				vendor.Equals ("Windows", StringComparison.InvariantCultureIgnoreCase) &&
				model.Equals ("Subsystem for Android(TM)", StringComparison.InvariantCultureIgnoreCase);
		}

		public static async Task ConnectJdwpAsync(this AndroidDevice androidDevice, ExecutionConfiguration config, CancellationToken token)
		{
			if (config.RunCommand != null && config.RunCommand is AmStartCommand amStartCommand && amStartCommand.EnableDebugging)
			{
				var packageName = (config.RunCommand as AmStartCommand).PackageName;
				var pid = await androidDevice.GetProcessIDAsync(packageName, 5, WAIT_BEFORE_RETRY_GET_PID, token);

				if (pid <= 0)
				{
					throw new Exception("Process Not Found.");
				}

				var jdwpClient = new JdwpClient(config.Debugger.JdwpHostName, config.Debugger.JdwpPort);

				await AdbServer.Default.ForwardPort(androidDevice, "tcp", jdwpClient.Port, "jdwp", pid, token);
				try {
					await jdwpClient.ConnectAsync (token);

					// Keep the Connection for 1300 milliseconds, otherwise the Android OS ignores the connection!
					// https://github.com/aosp-mirror/platform_frameworks_base/blob/6b28a227400749f4f8ad1f56799370e7c2cab149/core/java/android/os/Debug.java#L101C50-L101C54
					await Task.Delay (WAIT_FOR_DEBUGGER_TO_ATTACH_MS);

					await jdwpClient.DisconnectAsync ();
				} finally {
					await AdbServer.Default.KillForward (androidDevice, "tcp", jdwpClient.Port, token);
				}
			}
		}
	}
}
