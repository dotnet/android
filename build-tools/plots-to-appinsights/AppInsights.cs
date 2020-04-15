using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tools
{
	internal class AppInsights
	{
		private TelemetryClient TelemetryClient;

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
			lock (TelemetryClient) {
				TelemetryClient.TrackEvent (eventName, properties, metrics);
			}
		}

		public void Flush () => TelemetryClient.Flush ();
	}
}
