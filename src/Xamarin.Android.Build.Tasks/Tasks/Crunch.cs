using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.Android.Tools;
using Xamarin.Android.Tools.Aidl;
using ThreadingTasks = System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	public class Crunch : AsyncTask
	{
		// Aapt errors looks like this:
		//   C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\res\layout\main.axml:7: error: No resource identifier found for attribute 'id2' in package 'android' (TaskId:22)
		// Look for them and convert them to MSBuild compatible errors.
		private const string ErrorRegexString = @"(?<file>.*)\s*:\s*(?<line>\d*)\s*:\s*error:\s*(?<error>.+)";
		private Regex ErrorRegEx = new Regex (ErrorRegexString, RegexOptions.Compiled);

		[Required]
		public ITaskItem[] SourceFiles { get; set; }

		public string ToolPath { get; set; }

		public string ToolExe { get; set; }

		protected string ToolName { get { return OS.IsWindows ? "aapt.exe" : "aapt"; } }

		void DoExecute (IGrouping<string, ITaskItem> imageGroup)
		{
			var tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
			var tempOutputDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempOutputDirectory);
			try {
				LogDebugMessage ("Crunch Processing : {0}", imageGroup.Key);
				LogDebugTaskItems ("  Items :", imageGroup.ToArray ());
				foreach (var item in imageGroup) {
					var dest = Path.GetFullPath (item.ItemSpec).Replace (imageGroup.Key, tempDirectory);
					Directory.CreateDirectory (Path.GetDirectoryName (dest));
					MonoAndroidHelper.CopyIfChanged (item.ItemSpec, dest);
				}

				// crunch them
				if (!RunAapt (GenerateCommandLineCommands (tempDirectory, tempOutputDirectory))) {
					return;
				}

				// copy them back
				foreach (var item in imageGroup) {
					var dest = Path.GetFullPath (item.ItemSpec).Replace (imageGroup.Key, tempOutputDirectory);
					var srcmodifiedDate = File.GetLastWriteTimeUtc (item.ItemSpec);
					if (!File.Exists (dest))
						continue;
					MonoAndroidHelper.CopyIfChanged (dest, item.ItemSpec);
					// reset the Dates so MSBuild/xbuild doesn't think they changed.
					MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (item.ItemSpec, srcmodifiedDate, Log);
				}
			}
			finally {
				Directory.Delete (tempDirectory, recursive: true);
				Directory.Delete (tempOutputDirectory, recursive: true);
			}
			return;
		}

		public override bool Execute ()
		{
			Yield ();
			try {
				var task = ThreadingTasks.Task.Run ( () => {
					DoExecute ();
				}, Token);

				task.ContinueWith (Complete);

				base.Execute ();
			} finally {
				Reacquire ();
			}

			return !Log.HasLoggedErrors;
		}

		void DoExecute ()
		{
			LogDebugMessage ("Crunch Task");
			LogDebugTaskItems ("  SourceFiles:", SourceFiles);
			// copy the changed files over to a temp location for processing
			var imageFiles = SourceFiles.Where (x => string.Equals (Path.GetExtension (x.ItemSpec),".png", StringComparison.OrdinalIgnoreCase));

			if (!imageFiles.Any ())
				return;

			ThreadingTasks.ParallelOptions options = new ThreadingTasks.ParallelOptions {
				CancellationToken = Token,
				TaskScheduler = ThreadingTasks.TaskScheduler.Default,
			};

			var imageGroups = imageFiles.GroupBy (x => Path.GetDirectoryName (Path.GetFullPath (x.ItemSpec)));

			ThreadingTasks.Parallel.ForEach (imageGroups, options, DoExecute);

			return;
		}


		protected string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, string.IsNullOrEmpty (ToolExe) ? ToolName : ToolExe);
		}

		protected string GenerateCommandLineCommands (string tempDirectory, string tempOutputDirectory)
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitch ("c");
			cmd.AppendSwitchIfNotNull ("-S ", tempDirectory);
			cmd.AppendSwitchIfNotNull ("-C ", tempOutputDirectory);

			return cmd.ToString ();
		}

		protected void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			// Aapt errors looks like this:
			//   C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\res\layout\main.axml:7: error: No resource identifier found for attribute 'id2' in package 'android' (TaskId:22)
			// Look for them and convert them to MSBuild compatible errors.
			if (string.IsNullOrEmpty (singleLine))
				return;

			var match = ErrorRegEx.Match (singleLine);

			if (match.Success) {
				var file = match.Groups ["file"].Value;
				var line = int.Parse (match.Groups ["line"].Value) + 1;
				var error = match.Groups ["error"].Value;

				// Strip any "Error:" text from aapt's output
				if (error.StartsWith ("error: ", StringComparison.InvariantCultureIgnoreCase))
					error = error.Substring ("error: ".Length);

				singleLine = string.Format ("{0}({1}): error APT0000: {2}", file, line, error);
				messageImportance = MessageImportance.High;
			}

			// Handle additional error that doesn't match the regex
			LogMessage (singleLine, messageImportance);
		}

		bool RunAapt (string commandLine)
		{
			var psi = new ProcessStartInfo () {
				FileName = GenerateFullPathToTool (),
				Arguments = commandLine,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				WorkingDirectory = WorkingDirectory,
			};

			var proc = new Process ();
			proc.OutputDataReceived += (sender, e) => {
				LogEventsFromTextOutput (e.Data, MessageImportance.Normal);
			};
			proc.ErrorDataReceived += (sender, e) => {
				LogEventsFromTextOutput (e.Data, MessageImportance.Normal);
			};
			proc.StartInfo = psi;
			proc.Start ();
			proc.BeginOutputReadLine ();
			proc.BeginErrorReadLine ();
			Token.Register (() => {
				try {
					proc.Kill ();
				}
				catch (Exception) {
				}
			});
			LogDebugMessage ("Executing {0}", commandLine);
			proc.WaitForExit ();
			return proc.ExitCode == 0;
		}
	}
}
