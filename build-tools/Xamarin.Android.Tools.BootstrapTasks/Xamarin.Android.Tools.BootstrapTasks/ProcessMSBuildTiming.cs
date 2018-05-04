using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	// This tasks takes in XML file output from TimingLogger and aggregates the total build time for each build into a CSV file to be used on Jenkins
	public class ProcessMSBuildTiming : Task
	{
		[Required]
		public ITaskItem[] InputFiles { get; set; }

		public string ResultsFilename { get; set; }

		public bool AddResults { get; set; }

		public string LabelSuffix { get; set; }

		Dictionary<string, string> results = new Dictionary<string, string> ();

		public override bool Execute ()
		{
			foreach (var file in InputFiles) {
				var element = XElement.Load (file.ItemSpec);
				var build = element.Element ("build");
				var id = build.Attribute ("id")?.Value;
				var elapsed = build.Attribute ("elapsed")?.Value;
				if (TimeSpan.TryParse (elapsed, out TimeSpan result)) {
					results [id] = result.TotalMilliseconds.ToString ();
				}
			}
			WriteResults ();

			return !Log.HasLoggedErrors;
		}

		protected void WriteResults ()
		{
			if (ResultsFilename != null) {
				string line1 = null, line2 = null;
				if (AddResults && File.Exists (ResultsFilename)) {
					using (var reader = new StreamReader (ResultsFilename)) {
						try {
							line1 = reader.ReadLine ();
							line2 = reader.ReadLine ();
						} catch (Exception e) {
							Log.LogWarning ($"unable to read previous results from {ResultsFilename}\n{e}");
							line1 = line2 = null;
						}
					}
				}
				using (var resultsFile = new StreamWriter (ResultsFilename)) {
					WriteValues (resultsFile, results.Keys, line1, LabelSuffix);
					WriteValues (resultsFile, results.Values, line2);
					resultsFile.Close ();
				}
			}
		}

		void WriteValues (StreamWriter writer, ICollection<string> values, string line, string suffix = null)
		{
			bool first;
			if (string.IsNullOrEmpty (line))
				first = true;
			else {
				writer.Write (line);
				first = false;
			}
			foreach (var key in values) {
				if (!first)
					writer.Write (',');
				writer.Write (key);
				if (!string.IsNullOrEmpty (suffix))
					writer.Write (suffix);
				first = false;
			}
			writer.WriteLine ();
		}
	}
}
