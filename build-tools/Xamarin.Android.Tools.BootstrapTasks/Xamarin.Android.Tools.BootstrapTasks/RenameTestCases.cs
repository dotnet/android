using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

#if !APP
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#endif  // !APP

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
				CreateErrorResultsFile (SourceFile, dest, e);
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

		static void CreateErrorResultsFile (string source, string dest, Exception e)
		{
			var contents  = File.Exists (source)
				? File.ReadAllText (source)
				: "";

			var doc       = new XDocument (
				new XElement ("test-results",
					new XAttribute ("date", DateTime.Now.ToString ("yyyy-MM-dd")),
					new XAttribute ("errors", "1"),
					new XAttribute ("failures", "0"),
					new XAttribute ("ignored", "0"),
					new XAttribute ("inconclusive", "0"),
					new XAttribute ("invalid", "0"),
					new XAttribute ("name", dest),
					new XAttribute ("not-run", "0"),
					new XAttribute ("skipped", "0"),
					new XAttribute ("time", DateTime.Now.ToString ("HH:mm:ss")),
					new XAttribute ("total", "1"),
					new XElement ("environment",
						new XAttribute ("nunit-version", "3.6.0.0"),
						new XAttribute ("clr-version", "4.0.30319.42000"),
						new XAttribute ("os-version", "Unix 15.6.0.0"),
						new XAttribute ("platform", "Unix"),
						new XAttribute ("cwd", Environment.CurrentDirectory),
						new XAttribute ("machine-name", Environment.MachineName),
						new XAttribute ("user", Environment.UserName),
						new XAttribute ("user-domain", Environment.MachineName)),
					new XElement ("culture-info",
						new XAttribute ("current-culture", "en-US"),
						new XAttribute ("current-uiculture", "en-US")),
					new XElement ("test-suite",
						new XAttribute ("type", "APK-File"),
						new XAttribute ("name", dest),
						new XAttribute ("executed", "True"),
						new XAttribute ("result", "Failure"),
						new XAttribute ("success", "False"),
						new XAttribute ("time", "0"),
						new XAttribute ("asserts", "0"),
						new XElement ("results",
							new XElement ("test-case",
								new XAttribute ("name", Path.GetFileName (dest)),
								new XAttribute ("executed", "True"),
								new XAttribute ("result", "Error"),
								new XAttribute ("success", "False"),
								new XAttribute ("time", "0.0"),
								new XAttribute ("asserts", "1"),
								new XElement ("failure",
									new XElement ("message",
										$"Error processing `{source}`.  " +
										$"Check the build log for execution errors.{Environment.NewLine}" +
										$"File contents:{Environment.NewLine}",
										new XCData (contents)),
									new XElement ("stack-trace", e.ToString ())))))));
			doc.Save (dest);
		}
	}

#if APP
	// Compile:
	//   csc build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks/RenameTestCases.cs /out:test.exe /d:APP /r:System.Xml.Linq.dll
	// Run:
	//   mono test.exe test.xml
	// Validate:
	//   curl -o Results.xsd https://nunit.org/docs/files/Results.xsd
	//   MONO_XMLTOOL_ERROR_DETAILS=yes mono-xmltool  --validate Results.xsd test.xml
	partial class RenameTestCases {

		public static void Main (string[] args)
		{
			foreach (var file in args) {
				CreateErrorResultsFile ("source.xml", file, new Exception ("Wee!!!"));
			}
		}
	}
#endif  // APP
}
