using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class BrewRunner : ToolRunner
	{
		static readonly Version sudoVersion = new Version (1, 1);
		static readonly char[] lineSplit = new [] { '\n' };

		bool? needSudo;

		protected override string DefaultToolExecutableName => GetDefaultExecutableName ();
		protected override string ToolName                  => "Homebrew";

		string BrewPath => Context.Instance?.Tools?.BrewPath ?? "brew";

		public BrewRunner (Context context, Log log = null, string toolPath = null)
			: base (context, log, toolPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (30);
		}

		string GetDefaultExecutableName ()
		{
			if (!needSudo.HasValue) {
				// MUST use Context.Instance as the `Context` property might be null here
				var os = Context.Instance.OS as MacOS;
				if (os == null)
					throw new InvalidOperationException ($"BrewRunner does not suppport {Context.Instance.OS.Name}");

				needSudo = os.HomebrewVersion != null && os.HomebrewVersion < sudoVersion;
			}

			return needSudo.Value ? "sudo" : BrewPath;
		}

		public async Task<bool> Tap (string tapName, bool echoOutput = true, bool echoError = true)
		{
			if (String.IsNullOrEmpty (tapName))
				throw new ArgumentException ("must not be null or empty", nameof (tapName));

			return await RunBrew (echoOutput, echoError, "tap", tapName);
		}

		public async Task<bool> Install (string packageName, bool echoOutput = true, bool echoError = true)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			return await RunBrew (echoOutput, echoError, "install", packageName);
		}

		public async Task<bool> Upgrade (string packageName, bool echoOutput = true, bool echoError = true)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			return await RunBrew (echoOutput, echoError, "upgrade", packageName);
		}

		public async Task<bool> Pin (string packageName, bool echoOutput = false, bool echoError = false)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			return await RunBrew (echoOutput, echoError, "pin", packageName);
		}

		public async Task<bool> UnPin (string packageName, bool echoOutput = false, bool echoError = false)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			return await RunBrew (echoOutput, echoError, "unpin", packageName);;
		}

		public bool List (string packageName, out List<string> lines)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			lines = null;
			string listing = CaptureBrewOutput ("ls", packageName)?.Trim ();
			if (String.IsNullOrEmpty (listing))
				return false;

			lines = new List <string> (listing.Split (lineSplit, StringSplitOptions.RemoveEmptyEntries));
			return true;
		}

		string CaptureBrewOutput (params string[] parameters)
		{
			return Utilities.GetStringFromStdout (GetBrewRunner (false, true, parameters));
		}

		async Task<bool> RunBrew (bool echoOutput, bool echoError, params string[] parameters)
		{
			ProcessRunner runner = GetBrewRunner (echoOutput, echoError, parameters);
			bool success = await RunTool (() => runner.Run ());
			if (!success) {
				var os = Context.Instance.OS as MacOS;
				os.HomebrewErrors = true;
			}

			return success;
		}

		ProcessRunner GetBrewRunner (bool echoOutput, bool echoError, params string[] parameters)
		{
			ProcessRunner runner = CreateProcessRunner ();

			if (needSudo.HasValue && needSudo.Value) {
				runner.AddArgument (BrewPath);
			}

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
