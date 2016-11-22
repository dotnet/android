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
		public                  string              SdkVersion              {get; set;}

		[Output]
		public                  string              AdbTarget               {get; set;}

		[Output]
		public                  bool                IsValidTarget           {get; set;}

		protected   override    bool                LogTaskMessages {
			get { return false; }
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (CheckAdbTarget)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbTarget)}: {AdbTarget}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (SdkVersion)}: {SdkVersion}");

			base.Execute ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (AdbTarget)}: {AdbTarget}");
			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (IsValidTarget)}: {IsValidTarget}");

			return true;
		}

		protected override bool HandleTaskExecutionErrors ()
		{
			// We ignore all generated errors
			return true;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return $"{AdbTarget} shell getprop ro.build.version.sdk";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			base.LogEventsFromTextOutput (singleLine, messageImportance);
			if (string.IsNullOrEmpty (singleLine))
				return;
			if (singleLine.Equals ("List of devices attached", StringComparison.OrdinalIgnoreCase))
				return;
			// Ignore stderr
			if (messageImportance == MessageImportance.High)
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
	}
}

