using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class CheckAdbTarget : Adb
	{
		const int StateCheckSdk = 0;
		const int StateCheckPM = 1;
		const int MaxState = StateCheckPM;

		public                  string              SdkVersion              {get; set;}

		[Output]
		public                  string              DetectedAdbTarget       {get; set;}

		[Output]
		public                  bool                IsValidTarget           {get; set;}

		int currentState = -1;

		public override bool Execute ()
		{
			// Log messages as output rather than warnings
			WriteOutputAsMessage = true;

			base.Execute ();

			// We always succeed
			return true;
		}

		protected override List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell getprop ro.build.version.sdk",
					IgnoreExitCode = true,
					LogIgnoredExitCodeAsWarning = false,
					MergeStdoutAndStderr = false,
				},

				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell pm path com.android.shell",
					IgnoreExitCode = true,
					LogIgnoredExitCodeAsWarning = false,
					MergeStdoutAndStderr = false,
					ShouldRun = () => IsValidTarget
				},
			};
		}

		protected override void BeforeCommand (int commandIndex, CommandInfo info)
		{
			if (commandIndex < 0 || commandIndex > MaxState)
				throw new ArgumentOutOfRangeException (nameof (commandIndex));

			currentState = commandIndex;
		}

		protected override void ProcessStdout (string line)
		{
			if (String.IsNullOrEmpty (line))
				return;

			switch (currentState) {
				case StateCheckSdk:
					CheckSdkOutput (line);
					break;

				case StateCheckPM:
					CheckPMOutput (line);
					break;

				default:
					throw new InvalidOperationException ($"Invalid state {currentState}");
			}
		}

		void CheckSdkOutput (string singleLine)
		{
			if (singleLine.Equals ("List of devices attached", StringComparison.OrdinalIgnoreCase))
				return;

			// Informational messages, e.g.
			//  * daemon not running. starting it now on port 5037 *
			//  * daemon started successfully *
			if (singleLine.StartsWith ("* ", StringComparison.Ordinal))
				return;
			// Error messages, e.g.: error: device '(null)' not found
			if (singleLine.StartsWith ("error: ", StringComparison.OrdinalIgnoreCase))
				return;

			if (string.IsNullOrEmpty (SdkVersion)) {
				IsValidTarget   = true;
				return;
			}
			if (string.Equals (SdkVersion, singleLine, StringComparison.OrdinalIgnoreCase)) {
				IsValidTarget   = true;
				return;
			}
			int required, target;
			if (int.TryParse (SdkVersion, out required) &&
				    int.TryParse (singleLine, out target) &&
					target >= required) {
				IsValidTarget   = true;
				return;
			}
		}

		void CheckPMOutput (string singleLine)
		{
			if (singleLine.Contains ("Error: Could not access the Package Manager") ||
				    singleLine.Contains ("Failure ")) {
				IsValidTarget   = false;
				return;
			}
		}
	}
}

