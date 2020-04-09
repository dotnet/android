using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ProcessPlotCSVFile
{
	internal static class Constants
	{
		public const string AzureTimestampFormatUtc = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
	}
	class Program
	{
		static int Main (string [] args)
		{
			try {
				var currentUtcTime = DateTime.UtcNow;

				var settings = new Settings ();
				var result = settings.ProcessArgs (args);

				if (settings.BuildDateUtc.Equals (default)) {
					settings.BuildDateUtc = currentUtcTime;
				}

				settings.Display (Console.Out);

				if (!string.IsNullOrEmpty (result.Message)) {
					Console.WriteLine (result.Message);
					Console.WriteLine ();
				}

				if (result.Status.HasFlag (Status.ShowHelp) || result.Status != Status.OK) {
					Console.WriteLine ("ProcessPlotCSVFile [OPTIONS] PlotFilename.csv");
					Console.WriteLine ();
					Console.WriteLine ("Options:");
					settings.Options.WriteOptionDescriptions (Console.Out);
					Console.WriteLine ();
					return 1;
				}

				var plots = ProcessCSVFile (settings.CsvPathAndFilename, Console.Out);

				var appInsightsClient = new AppInsights (settings.AppInsightsTelemetryKey);
				var eventName = GetEventName (settings.Environment);

				Console.WriteLine ($"Sending telemetry. Event name: {eventName} / number of plots: {plots.Count ()}");
				SendTelemtry (appInsightsClient, eventName, plots, settings, Console.Out);

				appInsightsClient.Flush ();
			} catch (Exception e) {
				Console.WriteLine ($"EXCEPTION: {e.Message}");
				return 2;
			}

			return 0;
		}

		static Dictionary<string, decimal> ProcessCSVFile (string csvPathAndFilename, TextWriter tw)
		{
			if (string.IsNullOrEmpty (csvPathAndFilename)) {
				throw new ArgumentNullException (nameof (csvPathAndFilename));
			}

			if (!File.Exists (csvPathAndFilename)) {
				throw new FileNotFoundException (csvPathAndFilename);
			}

			var plots = new Dictionary<string, decimal> (StringComparer.OrdinalIgnoreCase);

			var lines = File.ReadAllLines (csvPathAndFilename);
			lines = lines.Where (line => !string.IsNullOrWhiteSpace (line)).ToArray ();             // Remove any empty lines
			var duplicateLabelsHavingDifferentValues = new List<string> ();
			if (lines.Length == 2) {
				var labels = lines [0].Split (new [] { ',' });
				var values = lines [1].Split (new [] { ',' });

				if (labels.Length == values.Length) {
					var index = 0;
					foreach (var label in labels) {
						if (decimal.TryParse (values [index], out var value)) {
							if (plots.TryGetValue (label, out var existingValue)) {
								if (value != existingValue) {
									tw.WriteLine ($"WARNING: {nameof (ProcessCSVFile)}: The plot file contains a duplicate plot for '{label}' having different values. value1: {existingValue} / value2: {value}.  File: {csvPathAndFilename}");
									duplicateLabelsHavingDifferentValues.Add (label);
								}
							} else {
								plots [label] = value;                          // The CSV file might contain redundant information
							}
						} else {
							throw new Exception ($"{nameof (ProcessCSVFile)}: Failed to parse the value '{values [index]}' for the {label} label in 0-based column {index}. Expecting a numeric value");
						}
						index++;
					}

					if (duplicateLabelsHavingDifferentValues.Any ()) {
						var distinctLabels = duplicateLabelsHavingDifferentValues.Distinct ();
						throw new Exception ($"{nameof (ProcessCSVFile)}: Duplicate plot labels having different values: {string.Join (',', distinctLabels)}");
					}
				} else {
					throw new Exception ($"{nameof (ProcessCSVFile)}: Mismatch between plot labels (line 1) and values (line 2). Count of labels and values should be identical. Number of labels: {labels.Length} / number of values: {values.Length}");
				}
			} else {
				throw new Exception ($"{nameof (ProcessCSVFile)}: Plot CSV file is expected to have two lines. First line should include the comma-delimited labels where the second line should include the comma-limited values. Instead the CSV file has {lines.Length} line(s)");
			}


			return plots;
		}

		static string GetEventName (string environment)
		{
			var eventName = "xamarin-android.plot";

			if (string.IsNullOrEmpty (environment)) {
				environment = "Dev";
			}

			if (!environment.Equals ("Production", StringComparison.OrdinalIgnoreCase)) {
				eventName = $"{eventName}.{environment.ToLowerInvariant ()}";
			}

			return eventName;
		}

		static void SendTelemtry (AppInsights appInsightsClient, string eventName, Dictionary<string, decimal> plots, Settings settings, TextWriter tw)
		{
			var schemaVersion = "1.0";
			var sessionTimestamp = DateTime.UtcNow.ToString (Constants.AzureTimestampFormatUtc);

			var ctPlot = 0;
			foreach (var plot in plots) {
				var properties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
					{ "Version", schemaVersion },
					{ "Environment", settings.Environment },
					{ "SessionTimestamp", sessionTimestamp },
					{ "BuildSystem", settings.BuildSystem },
					{ "BuildPipeline", settings.BuildPipelineName },
					{ "BuildPipelineUrl", settings.BuildPipelineUrl },
					{ "BuildType", settings.BuildType },
					{ "Title", settings.PlotTitle },
					{ "Group", settings.PlotGroup },
					{ "BuildNumber", settings.BuildNumber },
					{ "BuildDate", settings.BuildDateUtc.ToString(Constants.AzureTimestampFormatUtc) },
					{ "SeriesLabel", plot.Key },
					{ "Filename", Path.GetFileName(settings.CsvPathAndFilename) }
				};

				var metrics = new Dictionary<string, double> (StringComparer.OrdinalIgnoreCase) {
					{ "value", (double) plot.Value }
				};

				appInsightsClient.SendTelemetry (eventName, properties, metrics);

				ctPlot++;
				tw.WriteLine ($"Telemetry sent: Plot {ctPlot}");
				tw.WriteLine ($"  Event name: {eventName}");
				foreach (var property in properties.OrderBy (x => x.Key)) {
					tw.WriteLine ($"  {property.Key}: {property.Value}");
				}

				foreach (var metric in metrics.OrderBy (x => x.Key)) {
					tw.WriteLine ($"  {metric.Key}: {metric.Value}");
				}
			}
		}
	}
}
