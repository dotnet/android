using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class ProcessLogcatTiming : ProcessPlotInput
	{
		public string Activity { get; set; }

		public int PID { get; set; } = -1;

		public override bool Execute ()
		{
			LoadDefinitions ();

			if (!CheckInputFile ())
				return false;

			using (var reader = new StreamReader (InputFilename)) {
				string line;
				var procIdentification = string.IsNullOrEmpty (Activity) ? $"added application {ApplicationPackageName}" : $"activity {Activity}";
				var startIdentification = PID > 0 ? $".*: pid={PID}" : $@"{procIdentification}: pid=(?<pid>\d+)";
				var procStartRegex = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+.*ActivityManager: Start proc.*for {startIdentification}");
				var startIdentification2 = PID > 0 ? $"{PID}:" : $@"(?<pid>\d+):.*{procIdentification}";
				var procStartRegex2 = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+.*ActivityManager: Start proc {startIdentification2}");
				Regex timingRegex = null;
				DateTime start = DateTime.Now;
				DateTime last = start;
				bool started = false;

				while ((line = reader.ReadLine ()) != null) {
					if (!started) {
						var match = procStartRegex.Match (line);
						if (!match.Success)
							match = procStartRegex2.Match (line);
						if (!match.Success)
							continue;

						last = start = ParseTime (match.Groups ["timestamp"].Value);
						if (PID < 1)
							PID = Int32.Parse (match.Groups ["pid"].Value);
						Log.LogMessage (MessageImportance.Low, $"Time:      0ms process start, application: '{ApplicationPackageName}' PID: {PID}");
						timingRegex = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+{PID}\s+(?<message>.*)$");
						started = true;
					} else {
						var match = timingRegex.Match (line);
						if (!match.Success)
							continue;

						var time = ParseTime (match.Groups ["timestamp"].Value);
						var span = time - start;

						string message = match.Groups ["message"].Value;
						string logMessage = message;

						foreach (var regex in definedRegexs) {
							var definedMatch = regex.Value.Match (message);
							if (!definedMatch.Success)
								continue;
							results [regex.Key] = span.TotalMilliseconds.ToString ();
							var m = definedMatch.Groups ["message"];
							if (m.Success)
								logMessage = m.Value;

							Log.LogMessage (MessageImportance.Low, $"Time: {span.TotalMilliseconds.ToString ().PadLeft (6)}ms Message: {logMessage}");
							last = time;
						}
					}
				}

				if (PID > 0) {
					Log.LogMessage (MessageImportance.Normal, " -- Performance summary --");
					Log.LogMessage (MessageImportance.Normal, $"Last timing message: {(last - start).TotalMilliseconds}ms");

					WriteResults ();
				} else
					Log.LogWarning ("Wasn't able to collect the performance data");

				reader.Close ();
			}

			return true;
		}

		static Regex timeRegex = new Regex (@"(?<month>\d+)-(?<day>\d+)\s+(?<hour>\d+):(?<minute>\d+):(?<second>\d+)\.(?<millisecond>\d+)");
		DateTime ParseTime (string s)
		{
			var match = timeRegex.Match (s);
			if (!match.Success)
				throw new InvalidOperationException ($"Unable to parse time: '{s}'");

			// we don't handle year boundary here as the logcat timestamp doesn't include year information
			return new DateTime (DateTime.Now.Year,
					     int.Parse (match.Groups ["month"].Value),
					     int.Parse (match.Groups ["day"].Value),
					     int.Parse (match.Groups ["hour"].Value),
					     int.Parse (match.Groups ["minute"].Value),
					     int.Parse (match.Groups ["second"].Value),
					     int.Parse (match.Groups ["millisecond"].Value));
		}
	}
}
