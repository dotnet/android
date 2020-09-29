using Mono.Options;
using System;
using System.IO;

namespace Xamarin.Android.Tools.Plots
{
	internal class Settings
	{
		public bool ShowHelp { get; set; }
		public string Environment { get; set; } = "Dev";
		public string BuildSystem { get; set; } = "Azure DevOps";
		public string BuildRepo { get; set; }
		public string BuildType { get; set; } = "Desktop";
		public string BuildPipelineName { get; set; }
		public string BuildId { get; set; }
		public string BuildNumber { get; set; }
		public string BuildUrl { get; set; }
		public DateTime BuildDateUtc { get; set; } = default;
		public string BuildCommit { get; set; }
		public string PlotGroup { get; set; }
		public string PlotTitle { get; set; }
		public string AppInsightsTelemetryKey { get; set; }
		public string CsvPathAndFilename { get; set; }

		public OptionSet Options {
			get {
				// README/packaging: https://github.com/xamarin/XamarinComponents/tree/master/XPlat/Mono.Options
				// Source: https://github.com/mono/mono/tree/master/mcs/class/Mono.Options
				// Note: Use '=' after items with associated values; otherwise, the 'v' parameter will hold the name of the option and not associated value for the argument
				var options = new OptionSet {
					{ "h|help", "Show help",
						v => ShowHelp = true },
					{ "e|environment=", "Environment: Production, Test, Dev. Default: Dev",
						v => Environment = v },
					{ "s|build-system=", "Build system such as Azure DevOps or Jenkins. Default: Azure DevOps",
						v => BuildSystem = v },
					{ "r|repo=", "Repo being built",
						v => BuildRepo = v },
					{ "t|build-type=", "Build type: PR, CI, Manual or Desktop. Default: Desktop",
						v => BuildType = v },
					{ "p|build-pipeline-name=", "Build pipeline (definition) name such as Xamarin.Android",
						v => BuildPipelineName = v },
					{ "i|build-id=", "Build id",
						v => BuildId = v },
					{ "n|build-number=", "Build number",
						v => BuildNumber = v },
					{ "u|build-url=", "Url link to the build",
						v => BuildUrl = v },
					{ "d|build-date=", "Build date in UTC time. Default: Current UTC time when this app is first executed",
						v => BuildDateUtc = DateTime.Parse(v).ToUniversalTime() },
					{ "c|commit=", "Commid id for the commit being built",
						v => BuildCommit = v },
					{ "pg|plot-group=", "Plot group associated with the plot",
						v => PlotGroup = v },
					{ "pt|plot-title=", "Title for the plot chart",
						v => PlotTitle = v },
					{ "k|telemetry-key=", "Application Insights telemetry key",
						v => AppInsightsTelemetryKey =v },
					{ "<>", v => CsvPathAndFilename = v }
				};

				return options;
			}
		}

		internal Result ProcessArgs (string [] args)
		{
			var result = new Result ();

			if (args == null || args.Length == 0) {
				result.Status = Status.MissingArgument;
				result.Message = "Input arguments required";
			} else {
				try {
					Options.Parse (args);
				} catch (OptionException e) {
					result.Status = Status.Error;
					result.Message = $"EXCEPTION: Processing input arguments: {e.Message}";
				}

				if (result.Status == Status.OK) {
					if (ShowHelp) {
						result.Status = Status.ShowHelp;
					} else {
						result = Validate ();
					}
				}
			}

			return result;
		}

		private Result Validate ()
		{
			var result = new Result ();
			var setting = string.Empty;

			// Environment is not required: Default: Dev
			// BuildSystem is not required: Default: Azure DevOps
			// BuildType is not required: Default: Desktop
			if (string.IsNullOrWhiteSpace (BuildRepo)) {
				setting = "repo";
			} else if (string.IsNullOrWhiteSpace (BuildPipelineName)) {
				setting = "build-pipeline-name";
			} else if (string.IsNullOrWhiteSpace (BuildId)) {
				setting = "build-id";
			} else if (string.IsNullOrWhiteSpace (BuildNumber)) {
				setting = "build-number";
			} else if (string.IsNullOrWhiteSpace (BuildUrl)) {
				setting = "build-url";
			} else if (string.IsNullOrWhiteSpace (BuildCommit)) {
				setting = "commit";
			} else if (string.IsNullOrWhiteSpace (PlotGroup)) {
				setting = "plot-group";
			} else if (string.IsNullOrWhiteSpace (PlotTitle)) {
				setting = "plot-title";
			} else if (string.IsNullOrWhiteSpace (AppInsightsTelemetryKey)) {
				setting = "telemetry-key";
			} else if (string.IsNullOrWhiteSpace (CsvPathAndFilename)) {
				setting = "CSV path & filename";
			}

			if (!string.IsNullOrEmpty (setting)) {
				result.Message = $"Required argument '{setting}' not set";
				result.Status = Status.MissingArgument;
			}

			return result;
		}

		internal void Display (TextWriter tw)
		{
			tw.WriteLine ("Settings:");
			tw.WriteLine ($"  Environment: {Environment}");
			tw.WriteLine ($"  BuildSystem: {BuildSystem}");
			tw.WriteLine ($"  BuildRepo: {BuildRepo}");
			tw.WriteLine ($"  BuildType: {BuildType}");
			tw.WriteLine ($"  BuildPipelineName: {BuildPipelineName}");
			tw.WriteLine ($"  BuildId: {BuildId}");
			tw.WriteLine ($"  BuildNumber: {BuildNumber}");
			tw.WriteLine ($"  BuildUrl: {BuildUrl}");
			tw.WriteLine ($"  BuildDateUtc: {BuildDateUtc.ToString (Constants.AzureTimestampFormatUtc)}");
			tw.WriteLine ($"  BuildCommit: {BuildCommit}");
			tw.WriteLine ($"  PlotGroup: {PlotGroup}");
			tw.WriteLine ($"  PlotTitle: {PlotTitle}");
			var appInsightsTelemetryKey = !string.IsNullOrEmpty (AppInsightsTelemetryKey) ? "[hidden]" : "[empty]";
			tw.WriteLine ($"  AppInsightsTelemetryKey: {appInsightsTelemetryKey}");
			tw.WriteLine ($"  Plot CSV file: {CsvPathAndFilename}");
		}
	}

}
