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
		bool? needArch;

		protected override string DefaultToolExecutableName => GetDefaultExecutableName ();
		protected override string ToolName                  => "Homebrew";

		string BrewPath => Context.Instance?.Tools?.BrewPath ?? "brew";

		public BrewRunner (Context context, Log? log = null, string? toolPath = null)
			: base (context, log, toolPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (30);
			EchoStandardError = true;
			EchoStandardOutput = true;
		}

		string GetDefaultExecutableName ()
		{
			if (!needSudo.HasValue) {
				// MUST use Context.Instance as the `Context` property might be null here
				var os = Context.Instance.OS as MacOS;
				if (os == null)
					throw new InvalidOperationException ($"BrewRunner does not suppport {Context.Instance.OS.Name}");

				needSudo = os.HomebrewVersion != null && os.HomebrewVersion < sudoVersion;
				needArch = os.ProcessIsTranslated;
			}

			if (needSudo ?? false)
				return "sudo";
			if (needArch ?? false)
				return "arch";

			return BrewPath;
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

		public async Task<bool> Uninstall (string packageName, bool ignoreDependencies = false, bool force = false, bool echoOutput = true, bool echoError = true)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			var arguments = new List<string> {
				"uninstall"
			};
			
			
			if (ignoreDependencies)
				arguments.Add ("--ignore-dependencies");
			if (force)
				arguments.Add ("--force");
			arguments.Add (packageName);
			
			return await RunBrew (echoOutput, echoError, arguments);
		}

		public async Task<bool> Unlink (string packageName, bool echoOutput = true, bool echoError = true)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			return await RunBrew (echoOutput, echoError, "unlink", packageName);
		}

		public async Task<bool> Link (string packageName, bool echoOutput = true, bool echoError = true)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			return await RunBrew (echoOutput, echoError, "link", packageName);
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

		public bool List (string packageName, out List<string>? lines)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));

			lines = null;
			string? listing = CaptureBrewOutput ("ls", packageName)?.Trim ();
			if (String.IsNullOrEmpty (listing))
				return false;

			lines = new List <string> (listing!.Split (lineSplit, StringSplitOptions.RemoveEmptyEntries));
			return true;
		}

		string CaptureBrewOutput (string command, string packageName)
		{
			return CaptureBrewOutput (new List<string> { command, packageName });
		}

		string CaptureBrewOutput (List<string> arguments)
		{
			return Utilities.GetStringFromStdout (GetBrewRunner (false, true, arguments));
		}

		async Task<bool> RunBrew (bool echoOutput, bool echoError, string command, string packageName)
		{
			return await RunBrew (echoOutput, echoError, new List<string> { command, packageName });
		}

		async Task<bool> RunBrew (bool echoOutput, bool echoError, List<string> arguments)
		{
			ProcessRunner runner = GetBrewRunner (echoOutput, echoError, arguments);
			bool success = await RunTool (() => runner.Run ());
			if (!success) {
				var os = Context.Instance.OS as MacOS;
				if (os == null) {
					throw new InvalidOperationException ("Context.Instance.OS is not MacOS!");
				}
				os.HomebrewErrors = true;
			}

			return success;
		}

		ProcessRunner GetBrewRunner (bool echoOutput, bool echoError, List<string> arguments)
		{
			ProcessRunner runner = CreateProcessRunner ();

			if ((needSudo ?? false) && (needArch ?? false)) {
				// So we run `sudo arch -arch x86_64 brew …`
				runner.AddArgument ("arch");
			}

			if (needArch ?? false) {
				runner.AddArgument ("-arch");
				runner.AddArgument ("x86_64");
			}

			if ((needSudo ?? false) || (needArch ?? false)) {
				runner.AddArgument (BrewPath);
			}

			AddArguments (runner, arguments);

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
