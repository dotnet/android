using System;
using Android.App;
using Android.Widget;
using Android.OS;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace HelloWorld
{
	[Activity (
		Icon            = "@mipmap/icon",
		Label           = "HelloWorld",
		MainLauncher    = true,
		Name            = "example.MainActivity")]
	public class MainActivity : Activity
	{
		const string TypemapInstrumentationName = "Microsoft.Android.Runtime.TrimmableTypeMap";
		const string OtlpHeadersExtra = "OTEL_EXPORTER_OTLP_HEADERS";

		static readonly object TelemetryLock = new object ();
		static TracerProvider tracerProvider;
		static MeterProvider meterProvider;
		static bool telemetryConfigured;

		int count = 1;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			ConfigureTelemetry (Intent?.GetStringExtra (OtlpHeadersExtra));
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.myButton);

			button.Click += delegate {
				button.Text = string.Format ("{0} clicks!", count++);
			};
		}

		static void ConfigureTelemetry (string otlpHeaders)
		{
			lock (TelemetryLock) {
				if (telemetryConfigured)
					return;

				var endpoint = System.Environment.GetEnvironmentVariable ("OTEL_EXPORTER_OTLP_ENDPOINT");
				if (string.IsNullOrEmpty (endpoint))
					endpoint = "http://127.0.0.1:4318";

				var resourceBuilder = ResourceBuilder.CreateDefault ()
					.AddService ("helloworld-android");

				tracerProvider = Sdk.CreateTracerProviderBuilder ()
					.SetResourceBuilder (resourceBuilder)
					.AddSource (TypemapInstrumentationName)
					.AddOtlpExporter (options => {
						options.Protocol = OtlpExportProtocol.HttpProtobuf;
						options.Endpoint = GetOtlpEndpoint (endpoint, "v1/traces");
						if (!string.IsNullOrEmpty (otlpHeaders))
							options.Headers = otlpHeaders;
					})
					.Build ();

				meterProvider = Sdk.CreateMeterProviderBuilder ()
					.SetResourceBuilder (resourceBuilder)
					.AddMeter (TypemapInstrumentationName)
					.AddOtlpExporter (options => {
						options.Protocol = OtlpExportProtocol.HttpProtobuf;
						options.Endpoint = GetOtlpEndpoint (endpoint, "v1/metrics");
						if (!string.IsNullOrEmpty (otlpHeaders))
							options.Headers = otlpHeaders;
					})
					.Build ();

				telemetryConfigured = true;
				FlushBufferedTypemapEvents ();
			}
		}

		static void FlushBufferedTypemapEvents ()
		{
			var telemetryType = typeof (Android.Runtime.JNIEnv).Assembly.GetType ("Microsoft.Android.Runtime.TrimmableTypeMapTelemetry");
			var flushMethod = telemetryType?.GetMethod ("FlushBufferedEvents", BindingFlags.Static | BindingFlags.NonPublic);
			flushMethod?.Invoke (null, null);
		}

		static Uri GetOtlpEndpoint (string endpoint, string signalPath)
		{
			var builder = new UriBuilder (endpoint);
			var path = builder.Path;
			if (string.IsNullOrEmpty (path) || path == "/")
				builder.Path = signalPath;
			return builder.Uri;
		}
	}
}
