﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

#if !APP
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#endif  // !APP

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
#if !APP
	public partial class RenameTestCases : Task
	{
		public                  bool                DeleteSourceFiles           { get; set; }
		public                  string              Configuration               { get; set; }
		[Required]
		public                  string              SourceFile                  { get; set; }
		[Required]
		public                  string              DestinationFolder           { get; set; }

		[Output]
		public                  ITaskItem[]         CreatedFiles                { get; set; }

		public override bool Execute ()
		{
			var createdFiles    = new List<ITaskItem> ();
			var testNameSuffix  = string.IsNullOrWhiteSpace (Configuration)
				? ""
				: $" / {Configuration}";
			var dest            = GetFixedUpPath (SourceFile, testNameSuffix);
			var fixedUp         = false;

			try {
				FixupTestResultFile (SourceFile, dest, testNameSuffix);
				fixedUp = true;
			}
			catch (Exception e) {
				Log.LogWarning ($"Unable to process `{SourceFile}`.  Is it empty?  (Did a unit test runner SIGSEGV?)");
				Log.LogWarningFromException (e);
				CreateErrorResultsFile (SourceFile, dest, Configuration, e);
			}

			if (DeleteSourceFiles && Path.GetFullPath (SourceFile) != Path.GetFullPath (dest)) {
				File.Delete (SourceFile);
			}

			var item    = new TaskItem (dest);
			item.SetMetadata ("SourceFile", SourceFile);
			if (!fixedUp) {
				item.SetMetadata ("Invalid", "True");
			}
			createdFiles.Add (item);

			CreatedFiles    = createdFiles.ToArray ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (CreatedFiles)}:");
			foreach (var f in CreatedFiles) {
				Log.LogMessage (MessageImportance.Low, $"    [Output] {f}:");
			}

			return true;
		}

		string GetFixedUpPath (string source, string testNameSuffix)
		{
			var destFilename = Path.GetFileNameWithoutExtension (source) +
				(string.IsNullOrWhiteSpace (Configuration) ? "" : "-" + Configuration) +
				Path.GetExtension (source);
			var dest = Path.Combine (DestinationFolder, destFilename);
			return dest;
		}

		void FixupTestResultFile (string source, string dest, string testNameSuffix)
		{
			var doc = XDocument.Load (source);
			switch (doc.Root.Name.LocalName) {
			case "test-results":
				FixupNUnit2Results (doc, testNameSuffix);
				break;
			}

			doc.Save (dest);
		}

		void FixupNUnit2Results (XDocument doc, string testNameSuffix)
		{
			foreach (var e in doc.Descendants ("test-case")) {
				var name = (string) e.Attribute ("name");
				if (name.EndsWith (testNameSuffix, StringComparison.OrdinalIgnoreCase))
					continue;
				name += testNameSuffix;
				e.SetAttributeValue ("name", name);
			}
		}
	}
#endif  // !APP

	partial class RenameTestCases {

		static void CreateErrorResultsFile (string sourceFile, string destFile, string config, Exception e)
		{
			GetTestCaseInfo (sourceFile, Path.GetDirectoryName (destFile), config, out var testSuiteName, out var testCaseName, out var logcatPath);
			var contents  = new StringBuilder ();
			if (File.Exists (sourceFile)) {
				contents.Append (File.ReadAllText (sourceFile));
			}
			if (logcatPath != null) {
				if (contents.Length > 0) {
					contents.AppendLine ();
					contents.AppendLine ();
				}
				contents.AppendLine ("---`adb logcat`---");
				var logcat      = File.ReadAllText (logcatPath);
				var inBinRun    = false;
				foreach (var c in logcat) {
					if (!char.IsControl (c) || char.IsWhiteSpace (c)) {
						if (inBinRun) {
							contents.Append ("]");
						}
						inBinRun        = false;
						contents.Append (c);
						continue;
					}
					if (!inBinRun) {
						inBinRun        = true;
						contents.Append ("[!binary-block");
					}
					// Invalid XML char such as 0x0f; escape it
					contents.AppendFormat (" {0:x2}", (int) c);
				}
				if (inBinRun) {
					contents.Append ("]");
				}
			}

			var message = $"Error processing `{sourceFile}`.  " +
				$"Check the build log for execution errors.{Environment.NewLine}" +
				$"File contents:{Environment.NewLine}";

			ErrorResultsHelper.CreateErrorResultsFile (destFile, testSuiteName, testCaseName, e, message, contents.ToString ());
		}

		// Example `SourceFile`:
		//   /Users/builder/jenkins/workspace/xamarin-android-pr-builder-release/xamarin-android/bin/TestRelease/TestResult-Mono.Android_Tests.xml
		// Example `DestinationFolder`:
		//   /Users/builder/jenkins/workspace/xamarin-android-pr-builder-release/xamarin-android/
		// Example `adb logcat`:
		//   /Users/builder/jenkins/workspace/xamarin-android-pr-builder-release/xamarin-android/tests/logcat-Release-Mono.Android_Tests.txt
		//
		// We need to extract the "base" test name from `SourceFile`, and use that to construct `logcatPath`
		static void GetTestCaseInfo (string sourceFile, string destinationFolder, string config, out string testSuiteName, out string testCaseName, out string logcatPath)
		{
			var name        = Path.GetFileNameWithoutExtension (sourceFile);
			if (name.StartsWith ("TestResult-"))
				name    = name.Substring ("TestResult-".Length);
			testSuiteName   = name;
			testCaseName    = $"Possible Crash / {config}";
			logcatPath      = Path.Combine (destinationFolder, "tests", $"logcat-{config}-{name}.txt");
			if (!File.Exists (logcatPath)) {
				logcatPath      = null;
			}
		}
	}

#if APP
	// Compile:
	//   csc build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks/RenameTestCases.cs /out:test.exe /d:APP /r:System.Xml.Linq.dll /r:bin/BuildDebug/xa-prep-tasks.dll
	// Run:
	//   MONO_PATH=bin/BuildDebug mono test.exe test.xml
	// Validate:
	//   curl -o Results.xsd https://nunit.org/docs/files/Results.xsd
	//   MONO_XMLTOOL_ERROR_DETAILS=yes mono-xmltool  --validate Results.xsd test.xml
	partial class RenameTestCases {

		public static void Main (string[] args)
		{
			if (args.Length == 0) {
				Console.WriteLine ("RenameTestCases <DESTINATION-FILE> [SOURCE-FILE] [CONFIG]");
				return;
			}
			string destFile       = args [0];
			string sourceFile     = args.Length > 1 ? args [1] : "source.xml";
			string config         = args.Length > 2 ? args [2] : "Debug";
			CreateErrorResultsFile (sourceFile, destFile, config, new Exception ("Wee!!!"));
		}
	}
#endif  // APP
}
