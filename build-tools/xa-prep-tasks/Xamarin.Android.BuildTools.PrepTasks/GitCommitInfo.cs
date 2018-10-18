using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using IOFile = System.IO.File;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	// This class should be kept in sync with the build-tools/scripts/get-git-branch.sh script
	public sealed class GitCommitInfo : Task
	{
		[Required]
		public string WorkingDirectory { get; set; }

		[Required]
		public string XASourceDirectory { get; set; }

		public string SubmoduleName { get; set; }
		public string GitRemoteName { get; set; }
		public string GitPath { get; set; }

		public int ProcessTimeout { get; set; } = 120; // seconds
		public int OutputTimeout { get; set; } = 30; // seconds

		[Output]
		public string CommitInfo { get; set; }

		public override bool Execute ()
		{
			if (String.IsNullOrEmpty (GitPath?.Trim ()))
				GitPath = "git";

			bool isSubmodule = !String.IsNullOrEmpty (SubmoduleName);
			string submoduleInfo = isSubmodule ? $" submodule {SubmoduleName}" : String.Empty;

			Log.LogMessage (MessageImportance.Normal, $"Gathering GIT commit info{submoduleInfo} in directory '{WorkingDirectory}'");

			var stdoutLines = new List<string> ();
			var stderrLines = new List<string> ();
			if (!RunGit ("log -n 1 --pretty=%D HEAD", stdoutLines, stderrLines)) {
				goto outOfHere;
			}

			string gitModules = Path.Combine (XASourceDirectory, ".gitmodules");
			string branchFull = stdoutLines [0].Trim ();
			string branch;
			if (branchFull.StartsWith ("HEAD, ", StringComparison.Ordinal)) {
				Log.LogMessage (MessageImportance.Low, "  Detached HEAD, branch information available");
				// Detached HEAD with branch information
				// Sample format:
				//
				//    HEAD, origin/master, origin/d16-0-p1, origin/HEAD, master
				//
				branch = branchFull.Substring (6);
			} else if (branchFull.StartsWith ("HEAD -> ", StringComparison.Ordinal)) {
				Log.LogMessage (MessageImportance.Low, "  Normal branch");
				// Normal branch
				// Sample format:
				//
				//     HEAD -> bundle-ndk16-fix, origin/pr/1105
				//
				branch = branchFull.Substring (8);
			} else if (String.Compare ("HEAD", branchFull, StringComparison.Ordinal) == 0) {
				Log.LogMessage (MessageImportance.Low, "  Detached HEAD, no branch information");
				// Detached HEAD without branch information
				if (isSubmodule) {
					if (!RunGit ($"config -f {gitModules} --get \"submodule.{SubmoduleName}.branch\"", stdoutLines, stderrLines)) {
						goto outOfHere;
					}
					branch = stdoutLines [0];
				} else {
					Log.LogWarning ($"Unable to determine branch name from detached head state in directory {WorkingDirectory}");
					branch = "unknown";
				}
			} else {
				Log.LogError ($"Unable to parse branch name from: {branchFull}");
				branch = null;
				goto outOfHere;
			}

			int separator = branch.IndexOf (',');
			if (separator >= 0) {
				// We choose the first branch from the list
				branch = branch.Substring (0, separator).Trim ();
			}

			separator = branch.IndexOf ('/');
			if (separator >= 0) {
				branch = branch.Substring (separator + 1).Trim ();
			}

			if (branch.StartsWith ("tag: ", StringComparison.Ordinal)) {
				branch = branch.Substring (5).Trim ();
			}

			if (String.IsNullOrEmpty (branch)) {
				Log.LogError ($"Unsupported branch information format: '{branchFull}'");
				goto outOfHere;
			}

			Log.LogMessage (MessageImportance.Low, $"  Branch: {branch}");
			if (!RunGit ("log -n 1 --pretty=%h HEAD", stdoutLines, stderrLines)) {
				goto outOfHere;
			}
			string commit = stdoutLines [0].Trim ();
			Log.LogMessage (MessageImportance.Low, $"  Commit hash: {commit}");

			string url;
			if (isSubmodule) {
				if (!RunGit ($"config -f {gitModules} --get \"submodule.{SubmoduleName}.url\"", stdoutLines, stderrLines)) {
					goto outOfHere;
				}
				url = stdoutLines [0].Trim ();
			} else {
				string remoteName = String.IsNullOrEmpty (GitRemoteName) ? "origin" : GitRemoteName;
				if (!RunGit ($"config --local --get \"remote.{remoteName}.url\"", stdoutLines, stderrLines)) {
					goto outOfHere;
				}

				url = stdoutLines [0].Trim ();
			}
			Log.LogMessage (MessageImportance.Low, $"  Repository URL: {url}");

			string organization;
			string repo;
			bool urlParsed;
			if (url.StartsWith ("git@", StringComparison.Ordinal)) {
				urlParsed = ParseGitURL (url, out organization, out repo);
			} else if (url.StartsWith ("https://", StringComparison.Ordinal)) {
				urlParsed = ParseHttpsURL (url, out organization, out repo);
			} else if (url.StartsWith ("../../", StringComparison.Ordinal)) {
				// Special case for monodroid (although it will most likely return the git@ URL anyway)
				urlParsed = ParseRelativePathURL (url, out organization, out repo);
			} else {
				Log.LogError ($"Unknown URL schema in '{url}' for directory '{WorkingDirectory}'");
				goto outOfHere;
			}

			Log.LogMessage (MessageImportance.Low, $"  Organization: {organization}");
			Log.LogMessage (MessageImportance.Low, $"  Repository: {repo}");

			CommitInfo = $"{organization}/{repo}/{branch}@{commit}";

		  outOfHere:
			return !Log.HasLoggedErrors;
		}

		bool ParseGitURL (string url, out string organization, out string repo)
		{
			int colon = url.IndexOf (':');
			if (colon <= 4) {
				organization = repo = null;
				Log.LogError ($"Invalid GIT URL format for '{url}' in directory '{WorkingDirectory}'");
				return false;
			}

			return ParseURL (url.Substring (4, colon - 4), url.Substring (colon + 1), out organization, out repo);
		}

		bool ParseHttpsURL (string url, out string organization, out string repo)
		{
			int slash = url.IndexOf ('/', 8);
			if (slash <= 8) {
				organization = repo = null;
				Log.LogError ($"Invalid GIT URL format for '{url}' in directory '{WorkingDirectory}'");
				return false;
			}

			return ParseURL (url.Substring (8, slash - 8), url.Substring (slash + 1), out organization, out repo);
		}

		bool ParseRelativePathURL (string url, out string organization, out string repo)
		{
			return ParseURL (null, url.Substring (6), out organization, out repo);
		}

		bool ParseURL (string host, string path, out string organization, out string repo)
		{
			if (String.IsNullOrEmpty (host))
				host = "github.com"; // for monodroid, if ever

			string[] parts = path.Split (new [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2) {
				organization = repo = null;
				Log.LogError ($"Unknown URL path format '{path}'");
				return false;
			}

			if (String.Compare ("github.com", host, StringComparison.Ordinal) == 0) {
				organization = parts [0];
				repo = parts [1];
				if (repo.EndsWith (".git", StringComparison.Ordinal))
					repo = repo.Substring (0, repo.Length - 4);
				return true;
			}

			Log.LogWarning ($"Unknown GIT host '{host}', making assumptions about the URL");
			organization = parts [0];
			repo = String.Join ("/", parts, 1, parts.Length - 1);

			return true;
		}

		bool RunGit (string arguments, List<string> stdoutLines, List<string> stderrLines)
		{
			stdoutLines?.Clear ();
			stderrLines?.Clear ();

			bool canContinue = true;
			int exitCode = RunCommand (GitPath, arguments, stdoutLines, stderrLines);

			if (exitCode != 0) {
				canContinue = false;
				Log.LogError ($"'{GitPath} {arguments}' exited with code {exitCode}");
			}

			if (stderrLines.Count > 0) {
				canContinue = false;
				foreach (string line in stderrLines) {
					Log.LogError (line);
				}
			}

			if (stdoutLines.Count == 0) {
				canContinue = false;
				Log.LogError ($"'{GitPath} {arguments}' produced no output");
			}

			return canContinue;
		}

		int RunCommand (string commandPath, string arguments, List<string> stdoutLines, List<string> stderrLines)
		{
			var si = new ProcessStartInfo (commandPath) {
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = WorkingDirectory,
				RedirectStandardOutput = stdoutLines != null,
				RedirectStandardError = stderrLines != null,
				StandardOutputEncoding = Encoding.Default,
				StandardErrorEncoding = Encoding.Default,
				Arguments = arguments,
			};
			si.EnvironmentVariables.Add ("LC_LANG", "C");

			ManualResetEvent stdout_completed = null;
			if (!si.RedirectStandardError)
				si.StandardErrorEncoding = null;
			else
				stdout_completed = new ManualResetEvent (false);

			ManualResetEvent stderr_completed = null;
			if (!si.RedirectStandardOutput)
				si.StandardOutputEncoding = null;
			else
				stderr_completed = new ManualResetEvent (false);

			var p = new Process {
				StartInfo = si
			};
			p.Start ();

			var outputLock = new Object ();

			if (si.RedirectStandardOutput) {
				p.OutputDataReceived += (sender, e) => {
					if (e.Data != null)
						stdoutLines.Add (e.Data);
					else
						stdout_completed.Set ();
				};
				p.BeginOutputReadLine ();
			}

			if (si.RedirectStandardError) {
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data != null)
						stderrLines.Add (e.Data);
					else
						stderr_completed.Set ();
				};
				p.BeginErrorReadLine ();
			}

			TimeSpan outputTimeout = TimeSpan.FromSeconds (OutputTimeout <= 0 ? 1 : OutputTimeout);
			int processTimeout = ProcessTimeout < 0 ? -1 : ProcessTimeout * 1000;
			bool needToWait = true;
			bool exited = true;
			if (processTimeout > 0) {
				exited = p.WaitForExit (processTimeout);
				if (!exited) {
					Log.LogWarning ($"  Process '{commandPath} {si.Arguments}' failed to exit within the timeout of {ProcessTimeout}s, killing the process");
					p.Kill ();
				}

				// We need to call the parameter-less WaitForExit only if any of the standard output
				// streams have been redirected (see
				// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.7.2#System_Diagnostics_Process_WaitForExit)
				//
				if (!si.RedirectStandardOutput && !si.RedirectStandardError)
					needToWait = false;
			}

			if (needToWait)
				p.WaitForExit ();

			if (si.RedirectStandardError && stderr_completed != null)
				stderr_completed.WaitOne (outputTimeout);
			if (si.RedirectStandardOutput && stdout_completed != null)
				stdout_completed.WaitOne (outputTimeout);

			return exited ? p.ExitCode : -1;
		}
	}
}
