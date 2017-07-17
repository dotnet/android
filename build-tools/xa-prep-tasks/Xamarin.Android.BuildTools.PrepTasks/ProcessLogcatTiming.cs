using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class ProcessLogcatTiming : Task
	{
		[Required]
		public string LogcatFilename { get; set; }

		[Required]
		public string ApplicationPackageName { get; set; }

		public string ResultsFilename { get; set; }

		public override bool Execute ()
		{
			using (var reader = new StreamReader (LogcatFilename)) {
				string line;
				int pid = -1;
				var procStartRegex = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+.*ActivityManager: Start proc.*for added application {ApplicationPackageName}: pid=(?<pid>\d+)");
				Regex timingRegex = null;
				var runtimeInitRegex = new Regex (@"Runtime\.init: end native-to-managed");
				DateTime start = DateTime.Now;
				DateTime last = start;
				DateTime initEnd = start;

				while ((line = reader.ReadLine ()) != null) {
					if (pid == -1) {
						var match = procStartRegex.Match (line);
						if (!match.Success)
							continue;

						last = start = ParseTime (match.Groups ["timestamp"].Value);
						pid = Int32.Parse (match.Groups ["pid"].Value);
						Log.LogMessage (MessageImportance.Low, $"Time:      0ms process start, application: '{ApplicationPackageName}' PID: {pid}");
						timingRegex = new Regex ($@"^(?<timestamp>\d+-\d+\s+[\d:\.]+)\s+{pid}\s+.*I monodroid-timing:\s(?<message>.*)$");
					} else {
						var match = timingRegex.Match (line);
						if (!match.Success)
							continue;

						var time = ParseTime (match.Groups ["timestamp"].Value);
						var span = time - start;
						Log.LogMessage (MessageImportance.Low, $"Time: {span.TotalMilliseconds.ToString ().PadLeft (6)}ms Message: {match.Groups ["message"].Value}");

						match = runtimeInitRegex.Match (match.Groups ["message"].Value);
						if (match.Success)
							initEnd = time;
						last = time;
					}
				}

				if (pid != -1) {
					Log.LogMessage (MessageImportance.Normal, " -- Performance summary --");
					Log.LogMessage (MessageImportance.Normal, $"Runtime init end: {(initEnd - start).TotalMilliseconds}ms");
					Log.LogMessage (MessageImportance.Normal, $"Last timing message: {(last - start).TotalMilliseconds}ms");

					if (ResultsFilename != null)
						File.WriteAllText (Path.Combine (Path.GetDirectoryName (ResultsFilename), $"Test-{ApplicationPackageName}-times.csv"),
						                   $"init,last\n{(initEnd - start).TotalMilliseconds},{(last - start).TotalMilliseconds}");
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
