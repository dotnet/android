using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RenameTestCases : Task
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
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (RenameTestCases)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Configuration)}: {Configuration}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (DeleteSourceFiles)}: {DeleteSourceFiles}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (DestinationFolder)}: {DestinationFolder}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (SourceFile)}: {SourceFile}");

			var createdFiles    = new List<ITaskItem> ();
			var testNameSuffix  = string.IsNullOrWhiteSpace (Configuration)
				? ""
				: $" / {Configuration}";
			var dest            = GetFixedUpPath (SourceFile, testNameSuffix);

			try {
				FixupTestResultFile (SourceFile, dest, testNameSuffix);
			}
			catch (Exception e) {
				Log.LogError ($"Unable to process `{SourceFile}`.  Is it empty?  (Did a unit test runner SIGSEGV?)");
				CreateErrorResultsFile (dest, e);
				Log.LogErrorFromException (e);
			}

			var item    = new TaskItem (dest);
			item.SetMetadata ("SourceFile", SourceFile);
			createdFiles.Add (item);

			CreatedFiles    = createdFiles.ToArray ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (CreatedFiles)}:");
			foreach (var f in CreatedFiles) {
				Log.LogMessage (MessageImportance.Low, $"    [Output] {f}:");
			}

			return !Log.HasLoggedErrors;
		}

		string GetFixedUpPath (string source, string testNameSuffix)
		{
			var destFilename = Path.GetFileNameWithoutExtension (source) +
				(string.IsNullOrWhiteSpace (Configuration) ? "" : "-" + Configuration) +
				Path.GetExtension (source);
			var dest = Path.Combine (DestinationFolder, destFilename);
			return dest;
		}

		void CreateErrorResultsFile (string dest, Exception e)
		{
			var doc = new XDocument (
				new XElement ("test-results",
					new XAttribute ("date", DateTime.Now.ToString ("yyyy-MM-dd")),
					new XAttribute ("errors", "1"),
					new XAttribute ("failures", "0"),
					new XAttribute ("ignored", "0"),
					new XAttribute ("inconclusive", "0"),
					new XAttribute ("invalid", "0"),
					new XAttribute ("name", SourceFile),
					new XAttribute ("not-run", "0"),
					new XAttribute ("skipped", "0"),
					new XAttribute ("time", DateTime.Now.ToString ("HH:mm:ss")),
					new XAttribute ("total", "1"),
					new XElement ("test-suite",
						new XAttribute ("type", "Assembly"),
						new XAttribute ("name", SourceFile),
						new XAttribute ("executed", "True"),
						new XAttribute ("result", "Failure"),
						new XAttribute ("success", "False"),
						new XAttribute ("time", "0"),
						new XAttribute ("asserts", "0"),
						new XElement ("failure",
							new XElement ("message", $"Error processing `{SourceFile}`.  Check the build log for execution errors."),
							new XElement ("stack-trace", e.ToString ())))));
			doc.Save (dest);
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
			if (DeleteSourceFiles && Path.GetFullPath (source) != Path.GetFullPath (dest)) {
				File.Delete (source);
			}
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
}
