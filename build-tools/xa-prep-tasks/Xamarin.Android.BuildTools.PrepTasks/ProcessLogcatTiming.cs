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

		public override bool Execute ()
		{
			LoadDefinitions ();
			using (var reader = new StreamReader (InputFilename)) {
				string line;
				int pid = -1;
				var procIdentification = string.IsNullOrEmpty (Activity) ? $"added application {ApplicationPackageName}" : $"activity {Activity}";
				var procStartRegex = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+.*ActivityManager: Start proc.*for {procIdentification}: pid=(?<pid>\d+)");
				Regex timingRegex = null;
				DateTime start = DateTime.Now;
				DateTime last = start;

				while ((line = reader.ReadLine ()) != null) {
					if (pid == -1) {
						var match = procStartRegex.Match (line);
						if (!match.Success)
							continue;

						last = start = ParseTime (match.Groups ["timestamp"].Value);
						pid = Int32.Parse (match.Groups ["pid"].Value);
						Log.LogMessage (MessageImportance.Low, $"Time:      0ms process start, application: '{ApplicationPackageName}' PID: {pid}");
						timingRegex = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+{pid}\s+(?<message>.*)$");
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

				if (pid != -1) {
					Log.LogMessage (MessageImportance.Normal, " -- Performance summary --");
					Log.LogMessage (MessageImportance.Normal, $"Last timing message: {(last - start).TotalMilliseconds}ms");

					WriteResults ("times");
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
