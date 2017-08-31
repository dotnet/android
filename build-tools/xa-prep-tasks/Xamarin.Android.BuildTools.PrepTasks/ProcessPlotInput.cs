using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class ProcessPlotInput : Task
	{
		[Required]
		public string InputFilename { get; set; }

		[Required]
		public string ApplicationPackageName { get; set; }

		[Required]
		public string DefinitionsFilename { get; set; }

		public string ResultsFilename { get; set; }

		public bool AddResults { get; set; }

		protected Dictionary<string, Regex> definedRegexs = new Dictionary<string, Regex> ();
		protected Dictionary<string, string> results = new Dictionary<string, string> ();

		protected void LoadDefinitions ()
		{
			using (var reader = new StreamReader (DefinitionsFilename)) {
				string line;

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith ("#", StringComparison.Ordinal))
						continue;
					int index = line.IndexOf ('=');
					if (index < 1 || index == line.Length)
						continue;
					var label = line.Substring (0, index);
					var pattern = line.Substring (index + 1);
					Regex regex;
					try {
						regex = new Regex (pattern);
					} catch (Exception e) {
						Log.LogWarning ($"unable to create regex for label: {label} from pattern: {pattern}\n{e}");
						continue;
					}
					if (definedRegexs.ContainsKey (label))
						Log.LogWarning ($"label '{label}' is defined multiple times. the last definition will be used");
					definedRegexs [label] = regex;
				}
			}
		}

		public override bool Execute ()
		{
			LoadDefinitions ();
			using (var reader = new StreamReader (InputFilename)) {
				string line;

				while ((line = reader.ReadLine ()) != null) {
					foreach (var regex in definedRegexs) {
						var definedMatch = regex.Value.Match (line);
						if (!definedMatch.Success)
							continue;
						string logMessage = definedMatch.Value;
						var v = definedMatch.Groups ["value"];
						if (!v.Success)
							continue;
						results [regex.Key] = v.Value;
						var m = definedMatch.Groups ["message"];
						if (m.Success)
							logMessage = m.Value;

						Log.LogMessage (MessageImportance.Low, $"Message: {logMessage} Value: {v.Value}");
					}
				}

				WriteResults ("values");

				reader.Close ();
			}

			return true;
		}

		protected void WriteResults (string filenameEnd)
		{
			if (ResultsFilename != null) {
				string filename = Path.Combine (Path.GetDirectoryName (ResultsFilename), $"{Path.GetFileNameWithoutExtension (ResultsFilename)}-{filenameEnd}.csv");
				string line1 = null, line2 = null;
				if (AddResults && File.Exists (filename))
					using (var reader = new StreamReader (filename)) {
						try {
							line1 = reader.ReadLine ();
							line2 = reader.ReadLine ();
						} catch (Exception e) {
							Log.LogWarning ($"unable to read previous results from {filename}\n{e}");
							line1 = line2 = null;
						}
					}
				using (var resultsFile = new StreamWriter (filename)) {
					WriteValues (resultsFile, results.Keys, line1);
					WriteValues (resultsFile, results.Values, line2);
					resultsFile.Close ();
				}
			}
		}

		void WriteValues (StreamWriter writer, ICollection<string> values, string line)
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
				first = false;
			}
			writer.WriteLine ();
		}
	}
}
