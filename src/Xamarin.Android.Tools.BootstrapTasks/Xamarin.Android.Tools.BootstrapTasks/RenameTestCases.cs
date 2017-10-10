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
			try {
				var dest    = FixupTestResultFile (SourceFile, testNameSuffix);
				var item    = new TaskItem (dest);
				item.SetMetadata ("SourceFile", SourceFile);
				createdFiles.Add (item);
			}
			catch (Exception e) {
				Log.LogErrorFromException (e);
			}

			CreatedFiles    = createdFiles.ToArray ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (CreatedFiles)}:");
			foreach (var f in CreatedFiles) {
				Log.LogMessage (MessageImportance.Low, $"    [Output] {f}:");
			}

			return !Log.HasLoggedErrors;
		}

		string FixupTestResultFile (string source, string testNameSuffix)
		{
			var doc = XDocument.Load (source);
			switch (doc.Root.Name.LocalName) {
			case "test-results":
				FixupNUnit2Results (doc, testNameSuffix);
				break;
			}
			var destFilename = Path.GetFileNameWithoutExtension (source) +
				(string.IsNullOrWhiteSpace (Configuration) ? "" : "-" + Configuration) +
				Path.GetExtension (source);
			var dest = Path.Combine (DestinationFolder, destFilename);

			doc.Save (dest);
			if (DeleteSourceFiles && Path.GetFullPath (source) != Path.GetFullPath (dest)) {
				File.Delete (source);
			}
			return dest;
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
