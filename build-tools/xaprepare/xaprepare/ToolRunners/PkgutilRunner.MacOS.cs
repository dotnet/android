using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class PkgutilRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => Context.Instance?.Tools?.PkgutilPath ?? "pkgutil";
		protected override string ToolName                  => "PkgUtil";

		public PkgutilRunner (Context context, Log log = null, string toolPath = null)
			: base (context, log, toolPath)
		{}

#pragma warning disable CS1998
		public async Task<bool> LogPackagesInstalled ()
		{
			Log.StatusLine ("Installed packages:");
			Log.StatusLine (Utilities.GetStringFromStdout (GetRunner (true, true, "--pkgs")));
			return true;
		}

		public async Task<Version> GetPackageVersion (string packageId, bool echoOutput = false, bool echoError = true)
		{
			if (String.IsNullOrEmpty (packageId))
				throw new ArgumentException ("must not be null or empty", nameof (packageId));

			string output = Utilities.GetStringFromStdout (GetRunner (echoOutput, echoError, "--pkg-info", packageId))?.Trim ();
			if (String.IsNullOrEmpty (output))
				return null;

			string v = null;
			foreach (string l in output.Split ('\n')) {
				if (GetFieldValue (l.Trim (), "version:", out v))
					break;
			}

			if (String.IsNullOrEmpty (v)) {
				Log.WarningLine ($"Package info for {packageId} is empty");
				return null;
			}

			if (!Version.TryParse (v, out Version pkgVer)) {
				Log.ErrorLine ($"Failed to parse package {packageId} version from '{v}'");
				return null;
			}

			return pkgVer;
		}
#pragma warning restore CS1998

		bool GetFieldValue (string line, string name, out string v)
		{
			v = null;
			if (String.IsNullOrEmpty (line))
				return false;

			if (!line.StartsWith (name, StringComparison.Ordinal))
				return false;

			v = line.Substring (name.Length).Trim ();
			return true;
		}

		ProcessRunner GetRunner (bool echoOutput, bool echoError, params string[] parameters)
		{
			ProcessRunner runner = CreateProcessRunner ();

			AddArguments (runner, parameters);

			if (!echoOutput) {
				runner.EchoStandardOutputLevel = ProcessStandardStreamWrapper.LogLevel.Debug;
			}

			if (!echoError) {
				runner.EchoStandardErrorLevel = ProcessStandardStreamWrapper.LogLevel.Debug;
			}

			return runner;
		}
	}
}
