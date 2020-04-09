using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;

namespace ProcessPlotCSVFile
{
	internal class AppInsights
	{
		private TelemetryClient TelemetryClient { get; set; } = null;

		public AppInsights (string appInsightsInstrumentationKey)
		{
			if (string.IsNullOrEmpty (appInsightsInstrumentationKey)) {
				throw new ArgumentNullException (nameof (appInsightsInstrumentationKey));
			}

			// https://docs.microsoft.com/en-us/azure/azure-functions/functions-monitoring#log-custom-telemetry-in-c-functions
			var telemetryConfiguration = new TelemetryConfiguration (appInsightsInstrumentationKey);
			TelemetryClient = new TelemetryClient (telemetryConfiguration)  // Note: Direct creation of the TelemetryClient using the default constructor has been deprecated. Use dependency injection instead.
			{
				InstrumentationKey = appInsightsInstrumentationKey
			};
		}

		public void SendTelemetry (string eventName, Dictionary<string, string> properties, Dictionary<string, double> metrics)
		{
			if (TelemetryClient != null && !string.IsNullOrWhiteSpace (TelemetryClient.InstrumentationKey)) {
				lock (TelemetryClient) {
					TelemetryClient.TrackEvent (eventName, properties, metrics);
				}
			} else {
				throw new Exception ("TelemetryClient not initialized or AppInsights instrumentation key not set");
			}
		}

		public void Flush () => TelemetryClient.Flush ();
	}
}
