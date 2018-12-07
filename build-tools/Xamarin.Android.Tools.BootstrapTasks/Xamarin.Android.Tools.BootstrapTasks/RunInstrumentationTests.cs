using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunInstrumentationTests : Adb
	{
		const                   string              TestResultsPathResult       = "INSTRUMENTATION_RESULT: nunit2-results-path=";
		const                   int                 StateRunInstrumentation     = 0;
		const                   int                 StateGetLogcat              = 1;
		const                   int                 StateClearLogcat            = 2;
		const                   int                 StatePullFiles              = 3;
		const                   int                 MaxState                    = StatePullFiles;

		public                  string              TestFixture                 { get; set; }

		[Required]
		public                  string              Component                   { get; set; }

		public                  string[]            InstrumentationArguments    { get; set; }

		public                  string              NUnit2TestResultsFile       { get; set; }

		[Required]
		public                  string              LogcatFilename              { get; set; }

		[Required]
		public                  string              PackageName                 { get; set; }

		[Output]
		public                  string              FailedToRun                 { get; set; }

		public                  string              LogLevel                    { get; set; }

		int                     currentState = -1;
		string                  targetTestResultsPath;
		TextWriter              logcatWriter;

		public override bool Execute ()
		{
			InstrumentationArguments    = InstrumentationArguments ?? new string [0];
			if (string.IsNullOrEmpty (NUnit2TestResultsFile)) {
				var n = new StringBuilder ("TestResult-").Append (Component);
				foreach (var c in Path.GetInvalidFileNameChars ()) {
					n.Replace (c, '_');
				}
				n.Append (".xml");
				NUnit2TestResultsFile   = n.ToString ();
			}

			base.Execute ();

			if (String.IsNullOrEmpty (targetTestResultsPath)) {
				FailedToRun = Component;
				Log.LogError (
						"Could not find NUnit2 results file after running component `{0}`: " +
						"no `nunit2-results-path` bundle value found in command output!",
						Component);
				return false;
			}

			return !Log.HasLoggedErrors;
		}

		protected override List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} {AdbOptions} shell am instrument {GetRunInstrumentationArguments ()} -w \"{Component}\"",
				},

				new CommandInfo {
					ArgumentsString = $"{AdbTarget} {AdbOptions} logcat -v threadtime -d",
					MergeStdoutAndStderr = false,
					StdoutFilePath = LogcatFilename,
					StdoutAppend = true,
				},

				new CommandInfo {
					ArgumentsString = $"{AdbTarget} {AdbOptions} logcat -c",
				},

				new CommandInfo {
					ArgumentsGenerator = () => $"{AdbTarget} {AdbOptions} pull \"{targetTestResultsPath}\" \"{NUnit2TestResultsFile}\"",
					ShouldRun = () => !String.IsNullOrEmpty (targetTestResultsPath)
				},
			};
		}

		string GetRunInstrumentationArguments ()
		{
			var args = new StringBuilder ();
			foreach (string a in InstrumentationArguments) {
				string[] kvp = a.Split (new [] { '=' }, 2);
				args.Append (" -e \"").Append (kvp [0]).Append ("\" \"");
				args.Append (kvp.Length > 1 ? kvp [1] : "");
				args.Append ("\"");
			}

			if (!String.IsNullOrEmpty (LogLevel)) {
				args.Append ($" -e \"loglevel {LogLevel}\"");
			}

			if (!String.IsNullOrWhiteSpace (TestFixture)) {
				args.Append (" -e suite \"").Append (TestFixture).Append ("\"");
			}

			return args.ToString ();
		}

		protected override void BeforeCommand (int commandIndex, CommandInfo info)
		{
			if (commandIndex < 0 || commandIndex > MaxState)
				throw new ArgumentOutOfRangeException (nameof (commandIndex));

			currentState = commandIndex;
		}

		protected override void ProcessStdout (string line)
		{
			if (currentState != StateRunInstrumentation || String.IsNullOrEmpty (line))
				return;

			int i = line.IndexOf (TestResultsPathResult, StringComparison.OrdinalIgnoreCase);
			if (i < 0)
				return;

			targetTestResultsPath = line.Substring (i + TestResultsPathResult.Length).Trim ();
		}
	}
}
