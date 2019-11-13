using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class GitRunner : ToolRunner
	{
		// These are passed to `git` itself *before* the command
		static readonly List<string> standardGlobalOptions = new List<string> {
			"--no-pager"
		};

		protected override string DefaultToolExecutableName => "git";
		protected override string ToolName                  => "Git";

		public GitRunner (Context context, Log log = null, string gitPath = null)
			: base (context, log, gitPath ?? context?.Tools?.GitPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (30);
		}

		public async Task<bool> CheckoutCommit (string repositoryPath, string commitRef, bool force = true)
		{
			if (String.IsNullOrEmpty (repositoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (repositoryPath));
			if (String.IsNullOrEmpty (commitRef))
				throw new ArgumentException ("must not be null or empty", nameof (commitRef));

			var runner = CreateGitRunner (repositoryPath);
			runner.AddArgument ("checkout");
			if (force)
				runner.AddArgument ("--force");
			runner.AddArgument ("--progress");
			runner.AddQuotedArgument (commitRef);

			return await RunGit (runner, $"checkout-{Path.GetFileName (repositoryPath)}-{commitRef}");
		}

		public async Task<bool> Fetch (string repositoryPath)
		{
			if (String.IsNullOrEmpty (repositoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (repositoryPath));

			if (!Directory.Exists (repositoryPath)) {
				Log.ErrorLine ($"Repository {repositoryPath} does not exist");
				return false;
			}

			var runner = CreateGitRunner (repositoryPath);
			runner.AddArgument ("fetch");
			runner.AddArgument ("--all");
			runner.AddArgument ("--no-recurse-submodules");
			runner.AddArgument ("--progress");

			return await RunGit (runner, $"fetch-{Path.GetFileName (repositoryPath)}");
		}

		public async Task<bool> Clone (string url, string destinationDirectoryPath)
		{
			if (String.IsNullOrEmpty (url))
				throw new ArgumentException ("must not be null or empty", nameof (url));
			if (String.IsNullOrEmpty (destinationDirectoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (destinationDirectoryPath));

			string parentDir = Path.GetDirectoryName (destinationDirectoryPath);
			string dirName = Path.GetFileName (destinationDirectoryPath);
			Utilities.CreateDirectory (parentDir);
			var runner = CreateGitRunner (parentDir);;
			runner.AddArgument ("clone");
			runner.AddArgument ("--progress");
			runner.AddQuotedArgument (url);

			return await RunGit (runner, $"clone-{dirName}");
		}

		public async Task<bool> SubmoduleUpdate (string workingDirectory = null, bool init = true, bool recursive = true)
		{
			string runnerWorkingDirectory = DetermineRunnerWorkingDirectory (workingDirectory);

			var runner = CreateGitRunner (runnerWorkingDirectory);;
			runner.AddArgument ("submodule");
			runner.AddArgument ("update");
			if (init)
				runner.AddArgument ("--init");
			if (recursive)
				runner.AddArgument ("--recursive");

			return await RunGit (runner, "submodule-update");
		}

		public string GetTopCommitHash (string workingDirectory = null, bool shortHash = true)
		{
			string runnerWorkingDirectory = DetermineRunnerWorkingDirectory (workingDirectory);

			var runner = CreateGitRunner (runnerWorkingDirectory);
			runner.AddArgument ("rev-parse");
			runner.AddArgument ("HEAD");

			Log.StatusLine (GetLogMessage (runner), CommandMessageColor);

			string hash = null;
			using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
				outputSink.LineCallback = (string line) => {
					if (!String.IsNullOrEmpty (hash))
						return;
					hash = line?.Trim ();
				};

				if (!runner.Run ())
					return null;

				if (shortHash)
					return Utilities.ShortenGitHash (hash);

				return hash;
			}
		}

		public async Task<IList<BlamePorcelainEntry>> Blame (string filePath)
		{
			return await Blame (filePath, gitArguments: null, blameArguments: null, workingDirectory: null);
		}

		public async Task<IList<BlamePorcelainEntry>> Blame (string filePath, List <string> blameArguments)
		{
			return await Blame (filePath, gitArguments: null, blameArguments: blameArguments, workingDirectory: null);
		}

		public async Task<IList<BlamePorcelainEntry>> Blame (string filePath, List<string> gitArguments, List <string> blameArguments, string workingDirectory = null)
		{
			if (String.IsNullOrEmpty(filePath))
				throw new ArgumentException ("must not be null or empty", nameof (filePath));

			var runner = CreateGitRunner (workingDirectory, gitArguments);
			SetCommandArguments (runner, "blame", blameArguments);
			runner.AddArgument ("-p");
			runner.AddQuotedArgument (filePath);

			var parserState = new BlameParserState {
				CurrentEntry = null,
				Entries = new List<BlamePorcelainEntry> ()
			};

			Log.StatusLine (GetLogMessage (runner), CommandMessageColor);
			bool success = await RunTool (
				() => {
					using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
						outputSink.LineCallback = (string line) => ParseBlameLine (line, parserState);
						runner.WorkingDirectory = DetermineRunnerWorkingDirectory (workingDirectory);
						return runner.Run ();
					}
				}
			);

			if (!success)
				return null;

			return parserState.Entries;
		}

		public async Task<bool> IsRepoUrlHttps (string workingDirectory)
		{
			if (!Directory.Exists (workingDirectory))
				throw new ArgumentException ("must exist", nameof (workingDirectory));

			var runner = CreateGitRunner (workingDirectory);
			runner.AddArgument ("config");
			runner.AddArgument ("--get");
			runner.AddArgument ("remote.origin.url");

			bool containsHttps = false;

			bool success = await RunTool (
				() => {
					using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
						outputSink.LineCallback = (string line) => {
							containsHttps = !string.IsNullOrEmpty (line) && line.Contains ("https://");
						};
						runner.WorkingDirectory = DetermineRunnerWorkingDirectory (workingDirectory);
						return runner.Run ();
					}
				}
			);

			if (!success)
				return false;

			return containsHttps;
		}

		ProcessRunner CreateGitRunner (string workingDirectory, List<string> arguments = null)
		{
			var runner = CreateProcessRunner ();
			runner.WorkingDirectory = workingDirectory;
			SetGitArguments (runner, workingDirectory, arguments);

			return runner;
		}

		async Task<bool> RunGit (ProcessRunner runner, string logTag)
		{
			try {
				return await RunTool (
					() => {
						using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
							StartTwiddler ();
							return runner.Run ();
						}
					}
				);
			} finally {
				StopTwiddler ();
			}
		}

		void ParseBlameLine (string line, BlameParserState state)
		{
			if (state.CurrentEntry == null)
				state.CurrentEntry = new BlamePorcelainEntry ();

			if (!state.CurrentEntry.ProcessLine (line))
				return;

			state.Entries.Add (state.CurrentEntry);
			state.CurrentEntry = null;
		}

		protected override TextWriter CreateLogSink (string logFilePath)
		{
			return new OutputSink (Log, logFilePath);
		}

		void SetCommandArguments (ProcessRunner runner, string command, List<string> commandArguments)
		{
			runner.AddArgument (command);
			if (commandArguments == null || commandArguments.Count == 0)
				return;
			AddArguments (runner, commandArguments);
		}

		string DetermineRunnerWorkingDirectory (string workingDirectory)
		{
			if (!String.IsNullOrEmpty (workingDirectory))
				return workingDirectory;

			return BuildPaths.XamarinAndroidSourceRoot;
		}

		void SetGitArguments (ProcessRunner runner, string workingDirectory, List<string> gitArguments)
		{
			foreach (string arg in standardGlobalOptions) {
				runner.AddArgument (arg);
			}

			if (!String.IsNullOrEmpty (workingDirectory)) {
				runner.AddArgument ("-C");
				runner.AddQuotedArgument (workingDirectory);
			}

			AddArguments (runner, gitArguments);
		}
	}
}
