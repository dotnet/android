using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ProcessPlotCSVFile
{
	internal static class Constants
	{
		public const string AzureTimestampFormatUtc = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
		public const string TelemetryEventName = "xamarin-android.plot";
		public const string TelemetryEventName_Warning = "xamarin-android.plot.warning";
		public const string TelemetryEventName_Error = "xamarin-android.plot.error";
	}

	class Program
	{
		static int Main (string [] args)
		{
			var retCode = 0;
			var errMessage = string.Empty;
			var settings = new Settings ();

			try {
				var currentUtcTime = DateTime.UtcNow;

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

				var plots = ProcessCSVFile (settings.CsvPathAndFilename, out var warningMessages, Console.Out);

				var appInsightsClient = new AppInsights (settings.AppInsightsTelemetryKey);
				var eventName = GetEventName (settings.Environment, Constants.TelemetryEventName);

				Console.WriteLine ($"Sending telemetry. Event name: {eventName} / number of plots: {plots.Count ()}");
				SendTelemtry (appInsightsClient, eventName, plots, settings, Console.Out);
				appInsightsClient.Flush ();

				var eventName_Warning = GetEventName (settings.Environment, Constants.TelemetryEventName_Warning);
				SendTelemetry_Warnings (appInsightsClient, eventName_Warning, warningMessages, settings, Console.Out);
				appInsightsClient.Flush ();

			} catch (Exception e) {
				errMessage = e.Message;
				Console.WriteLine ($"EXCEPTION: {errMessage}");
				Console.WriteLine (e.StackTrace);
				retCode = 2;
			}

			try {
				if (!string.IsNullOrEmpty (errMessage)) {
					var appInsightsClient = new AppInsights (settings.AppInsightsTelemetryKey);
					var eventName_Error = GetEventName (settings.Environment, Constants.TelemetryEventName_Error);

					SendTelemetry_Error (appInsightsClient, eventName_Error, errMessage, settings, Console.Out);
					appInsightsClient.Flush ();
				}
			} catch (Exception e) {
				Console.WriteLine ($"EXCEPTION: Sending error telemetry: {e.Message}");
				Console.WriteLine (e.StackTrace);
				retCode = 3;
			}

			return retCode;
		}

		static Dictionary<string, decimal> ProcessCSVFile (string csvPathAndFilename, out List<string> warningMessages, TextWriter tw)
		{
			warningMessages = new List<string> ();

			if (string.IsNullOrEmpty (csvPathAndFilename)) {
				throw new ArgumentNullException (nameof (csvPathAndFilename));
			}

			if (!File.Exists (csvPathAndFilename)) {
				throw new FileNotFoundException (csvPathAndFilename);
			}

			if (tw == null) {
				throw new ArgumentNullException (nameof (tw));
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
								var csvFilename = Path.GetFileName (csvPathAndFilename);
								var warningMessage = $"The plot file contains a duplicate plot for '{label}'. value1: {existingValue} / value2: {value}. File: {csvFilename}";
								warningMessages.Add (warningMessage);
								tw.WriteLine ($"WARNING: {nameof (ProcessCSVFile)}: {warningMessage}");

								if (value != existingValue) {
									tw.WriteLine ($"ERROR: {nameof (ProcessCSVFile)}: The duplicate plots have different values. value1: {existingValue} / value2: {value}. File: {csvFilename}");
									duplicateLabelsHavingDifferentValues.Add (label);	// This will result in an exception being thrown
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
						throw new Exception ($"{nameof (ProcessCSVFile)}: Duplicate plot labels having different values: {string.Join (",", distinctLabels)}");
					}
				} else {
					throw new Exception ($"{nameof (ProcessCSVFile)}: Mismatch between plot labels (line 1) and values (line 2). Count of labels and values should be identical. Number of labels: {labels.Length} / number of values: {values.Length}");
				}
			} else {
				throw new Exception ($"{nameof (ProcessCSVFile)}: Plot CSV file is expected to have two lines. First line should include the comma-delimited labels where the second line should include the comma-limited values. Instead the CSV file has {lines.Length} line(s)");
			}

			return plots;
		}

		static string GetEventName (string environment, string eventName)
		{
			if (string.IsNullOrEmpty (eventName)) {
				throw new ArgumentNullException (nameof (eventName));
			}

			if (string.IsNullOrEmpty (environment)) {
				environment = "Dev";
			}

			if (!environment.Equals ("Production", StringComparison.OrdinalIgnoreCase)) {
				eventName = $"{eventName}.{environment.ToLowerInvariant ()}";
			}

			return eventName;
		}

		static Dictionary<string, string> GetBaseTelemetryProperties (Settings settings)
		{
			if (settings == null) {
				throw new ArgumentNullException (nameof (settings));
			}

			var schemaVersion = "1.0";
			var sessionTimestamp = DateTime.UtcNow.ToString (Constants.AzureTimestampFormatUtc);

			var properties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
					{ "Version", schemaVersion },
					{ "Environment", settings.Environment },
					{ "SessionTimestamp", sessionTimestamp },
					{ "BuildSystem", settings.BuildSystem },
					{ "BuildPipeline", settings.BuildPipelineName },
					{ "BuildType", settings.BuildType },
					{ "BuildId", settings.BuildId },
					{ "BuildNumber", settings.BuildNumber },
					{ "BuildUrl", settings.BuildUrl },
					{ "BuildDate", settings.BuildDateUtc.ToString(Constants.AzureTimestampFormatUtc) },
					{ "Filename", Path.GetFileName(settings.CsvPathAndFilename) },
					{ "Title", settings.PlotTitle },
					{ "Group", settings.PlotGroup }
				};

			return properties;
		}

		static void SendTelemtry (AppInsights appInsightsClient, string eventName, Dictionary<string, decimal> plots, Settings settings, TextWriter tw)
		{
			if (appInsightsClient == null) {
				throw new ArgumentNullException (nameof (appInsightsClient));
			}

			if (string.IsNullOrEmpty (eventName)) {
				throw new ArgumentNullException (nameof (eventName));
			}

			if (plots == null || !plots.Any()) {
				throw new ArgumentNullException (nameof (plots));
			}

			if (settings == null) {
				throw new ArgumentNullException (nameof (settings));
			}

			if (tw == null) {
				throw new ArgumentNullException (nameof (tw));
			}

			var ctPlot = 0;
			foreach (var plot in plots) {

				var properties = GetBaseTelemetryProperties (settings);
				var plotProperties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
					{ "SeriesLabel", plot.Key }
				};

				// Add plot information to the properties dictionary
				plotProperties.ToList ().ForEach (x => properties [x.Key] = x.Value);

				var metrics = new Dictionary<string, double> (StringComparer.OrdinalIgnoreCase) {
					{ "value", (double) plot.Value }
				};

				appInsightsClient.SendTelemetry (eventName, properties, metrics);

				ctPlot++;
				tw.WriteLine ($"Telemetry sent: Plot {ctPlot}");
				tw.WriteLine ($"  Event name: {eventName}");
				tw.WriteLine ("    Properties:");
				foreach (var property in properties.OrderBy (x => x.Key)) {
					tw.WriteLine ($"      {property.Key}: {property.Value}");
				}

				tw.WriteLine ("    Metrics:");
				foreach (var metric in metrics.OrderBy (x => x.Key)) {
					tw.WriteLine ($"      {metric.Key}: {metric.Value}");
				}
			}
		}

		static void SendTelemetry_Warnings (AppInsights appInsightsClient, string eventName, List<string> warnings, Settings settings, TextWriter tw)
		{
			if (appInsightsClient == null) {
				throw new ArgumentNullException (nameof (appInsightsClient));
			}

			if (string.IsNullOrEmpty (eventName)) {
				throw new ArgumentNullException (nameof (eventName));
			}

			if (warnings == null) {         // Note: It's okay if warnings is empty
				throw new ArgumentNullException (nameof (warnings));
			}

			if (settings == null) {
				throw new ArgumentNullException (nameof (settings));
			}

			if (tw == null) {
				throw new ArgumentNullException (nameof (tw));
			}

			foreach (var warning in warnings) {
				var properties = GetBaseTelemetryProperties (settings);
				var warningProperties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
								{ "Message", warning }
				};

				// Add warning to the properties dictionary
				warningProperties.ToList ().ForEach (x => properties [x.Key] = x.Value);

				var metrics = new Dictionary<string, double> (StringComparer.OrdinalIgnoreCase);

				appInsightsClient.SendTelemetry (eventName, properties, metrics);

				tw.WriteLine ($"Telemetry sent: Warning: {warning}");
				tw.WriteLine ($"  Event name: {eventName}");
				tw.WriteLine ("    Properties:");
				foreach (var property in properties.OrderBy (x => x.Key)) {
					tw.WriteLine ($"      {property.Key}: {property.Value}");
				}
			}
		}

		static void SendTelemetry_Error (AppInsights appInsightsClient, string eventName, string errMessage, Settings settings, TextWriter tw)
		{
			if (appInsightsClient == null) {
				throw new ArgumentNullException (nameof (appInsightsClient));
			}

			if (string.IsNullOrEmpty(eventName)) {
				throw new ArgumentNullException (nameof (eventName));
			}

			if (string.IsNullOrEmpty(errMessage)) {
				throw new ArgumentNullException (nameof (errMessage));
			}

			if (settings == null) {
				throw new ArgumentNullException (nameof (settings));
			}

			if (tw == null) {
				throw new ArgumentNullException (nameof(tw));
			}

			var properties = GetBaseTelemetryProperties (settings);
			var errorProperties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
							{ "Message", errMessage }
			};

			// Add error to the properties dictionary
			errorProperties.ToList ().ForEach (x => properties [x.Key] = x.Value);

			var metrics = new Dictionary<string, double> (StringComparer.OrdinalIgnoreCase);

			appInsightsClient.SendTelemetry (eventName, properties, metrics);

			tw.WriteLine ($"Telemetry sent: Error: {errMessage}");
			tw.WriteLine ($"  Event name: {eventName}");
			tw.WriteLine ("    Properties:");
			foreach (var property in properties.OrderBy (x => x.Key)) {
				tw.WriteLine ($"      {property.Key}: {property.Value}");
			}
		}
	}
}
