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
		public                  string              AdbTarget                   { get; set; }
		public                  string              AdbOptions                  { get; set; }

		public                  string              TestFixture                 { get; set; }

		[Required]
		public                  string              Component                   { get; set; }

		public                  string[]            InstrumentationArguments    { get; set; }

		public                  string              NUnit2TestResultsFile       { get; set; }

		[Output]
		public                  string              FailedToRun                 { get; set; }

		protected   override    bool                LogTaskMessages {
			get { return false; }
		}

		enum ExecuteState {
			RunInstrumentation,
			PullFiles,
		}

		ExecuteState            executionState;
		string                  targetTestResultsPath;

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

			Log.LogMessage (MessageImportance.Low, $"Task {nameof (RunInstrumentationTests)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbTarget)}: {AdbTarget}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AdbOptions)}: {AdbOptions}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Component)}: {Component}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (InstrumentationArguments)}:");
			foreach (var a in InstrumentationArguments) {
				Log.LogMessage (MessageImportance.Low, $"    {a}:");
			}
			Log.LogMessage (MessageImportance.Low, $"  {nameof (NUnit2TestResultsFile)}: {NUnit2TestResultsFile}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (TestFixture)}: {TestFixture}");

			executionState  = ExecuteState.RunInstrumentation;
			base.Execute ();

			if (string.IsNullOrEmpty (targetTestResultsPath)) {
				FailedToRun = Component;
				Log.LogError (
						"Could not find NUnit2 results file after running component `{0}`: " +
						"no `nunit2-results-path` bundle value found in command output!",
						Component);
				// Can return false once we use MSBuild and not xbuild
				// return false;
				return true;
			}

			executionState  = ExecuteState.PullFiles;
			base.Execute ();

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			switch (executionState) {
			case ExecuteState.RunInstrumentation:
				var args = new StringBuilder ();
				foreach (var a in InstrumentationArguments) {
					var kvp = a.Split (new [] { '=' }, 2);
					args.Append (" -e \"").Append (kvp [0]).Append ("\" \"");
					args.Append (kvp.Length > 1 ? kvp [1] : "");
					args.Append ("\"");
				}
				if (!string.IsNullOrWhiteSpace (TestFixture)) {
					args.Append (" -e suite \"").Append (TestFixture).Append ("\"");
				}
				return $"{AdbTarget} {AdbOptions} shell am instrument {args.ToString ()} -w \"{Component}\"";
			case ExecuteState.PullFiles:
				return $"{AdbTarget} {AdbOptions} pull \"{targetTestResultsPath}\" \"{NUnit2TestResultsFile}\"";
			}
			throw new InvalidOperationException ($"Invalid state `{executionState}`!");
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			const string TestResultsPathResult  = "INSTRUMENTATION_RESULT: nunit2-results-path=";

			base.LogEventsFromTextOutput (singleLine, messageImportance);

			if (string.IsNullOrEmpty (singleLine))
				return;
			if (!singleLine.Contains (TestResultsPathResult))
				return;
			var i = singleLine.IndexOf (TestResultsPathResult);
			targetTestResultsPath   = singleLine.Substring (i + TestResultsPathResult.Length).Trim ();
		}
	}
}
