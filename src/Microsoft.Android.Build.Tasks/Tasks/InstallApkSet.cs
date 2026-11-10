#nullable enable
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Microsoft.Android.Tasks
{
	/// <summary>
	/// Invokes `bundletool` to install an APK set to an attached device
	///
	/// Usage: bundletool install-apks --apks=foo.apks
	/// </summary>
	public partial class InstallApkSet : BundleToolAdbTask
	{
		public override string TaskPrefix => "IAS";

		public override string DefaultErrorCode => "BT0000";

		[Required]
		public string ApkSet { get; set; } = "";

		public string[]? Modules  { get; set; }

		internal override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("install-apks");
			cmd.AppendSwitchIfNotNull ("--apks ", ApkSet);
			AppendAdbOptions (cmd);
			cmd.AppendSwitch ("--allow-downgrade");

			// --modules: List of modules to be installed, or "_ALL_" for all modules.
			// Defaults to modules installed during first install, i.e. not on-demand.
			// .NET for Android won't support on-demand modules yet.
			if (Modules == null)
				cmd.AppendSwitchIfNotNull ("--modules ", "_ALL_");
			else
				cmd.AppendSwitchIfNotNull ("--modules ", $"\"{string.Join ("\",\"", Modules)}\"");

			return cmd;
		}

		[GeneratedRegex (@"(?<exception>com.android.tools.build.bundletool.model.exceptions.CommandExecutionException):(?<error>.+)")]
		private static partial Regex InstallErrorRegEx { get; }

		protected override IEnumerable<Regex> GetCustomExpressions ()
		{
			yield return InstallErrorRegEx;
		}

		internal override bool ProcessOutput (string singleLine, AssemblyIdentityMap assemblyMap)
		{
			var match = InstallErrorRegEx.Match (singleLine);
			if (match.Success) {
				// error message
				var error = match.Groups ["error"].Value;
				var exception = match.Groups ["exception"].Value;
				SetFileLineAndColumn (ApkSet, line: 1, column: 0);
				AppendTextToErrorText (exception);
				AppendTextToErrorText (error);
				return LogFromException (exception, error);;
			}
			return base.ProcessOutput (singleLine, assemblyMap);
		}
	}
}
