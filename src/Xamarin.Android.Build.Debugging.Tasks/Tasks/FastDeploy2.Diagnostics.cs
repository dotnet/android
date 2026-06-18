using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xamarin.Android.Tasks
{
	public abstract partial class FastDeploy2Base
	{
		internal class DiagnosticData {
			[JsonPropertyName ("Task")]
			public string Task { get; set; } = nameof (FastDeploy2);

			[JsonPropertyName ("Properties")]
			public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string> () {
				{ "target.prop.ro.product.build.version.sdk", "" },
				{ "target.prop.ro.product.cpu.abilist", "" },
				{ "target.prop.ro.product.manufacturer", "" },
				{ "target.prop.ro.product.model", "" },
				{ "target.prop.ro.product.cpu.abi", "" },
				{ "deploy.error.code", "" },
				{ "deploy.tool", "adb push" },
				{ "deploy.result", "Success" },
				{ "deploy.supports.fastdev", "True" },
				{ "deploy.systemapp", "False" },
				{ "deploy.duration.ms", "0" },
				{ "deploy.fastdeploy2.adb.pushed.files", "" },
				{ "deploy.fastdeploy2.adb.skipped.files", "" },
				{ "deploy.fastdeploy2.changed.files", "" },
				{ "deploy.fastdeploy2.stale.files", "" },
				{ "deploy.fastdeploy2.local.stage.ms", "" },
				{ "deploy.fastdeploy2.remote.mkdir.ms", "" },
				{ "deploy.fastdeploy2.remote.staging.cleanup.ms", "" },
				{ "deploy.fastdeploy2.upload.ms", "" },
				{ "deploy.fastdeploy2.staging.stat.ms", "" },
				{ "deploy.fastdeploy2.override.stat.ms", "" },
				{ "deploy.fastdeploy2.compare.ms", "" },
				{ "deploy.fastdeploy2.stale.remove.ms", "" },
				{ "deploy.fastdeploy2.override.mkdir.ms", "" },
				{ "deploy.fastdeploy2.override.copy.ms", "" },
				{ "deploy.orchestration.ensure-properties.ms", "" },
				{ "deploy.orchestration.property-checks.ms", "" },
				{ "deploy.orchestration.package-check.ms", "" },
				{ "deploy.orchestration.package-timestamp.ms", "" },
				{ "deploy.orchestration.install.ms", "" },
				{ "deploy.orchestration.terminate.ms", "" },
				{ "deploy.orchestration.empty-check.ms", "" },
				{ "deploy.execute.parse-target.ms", "" },
				{ "deploy.execute.no-abi-check.ms", "" },
				{ "deploy.execute.upload-flag-stat.ms", "" },
				{ "deploy.execute.task-cache.ms", "" },
				{ "deploy.orchestration.property-capture.ms", "" },
				{ "deploy.orchestration.redirect-stdio-check.ms", "" },
				{ "deploy.orchestration.run-as-disabled-check.ms", "" },
				{ "deploy.orchestration.package-check.ensure-user.ms", "" },
				{ "deploy.orchestration.package-check.run-as-pwd.ms", "" },
				{ "deploy.orchestration.package-check.run-as-pwd-pidof.ms", "" },
				{ "deploy.orchestration.package-check.readlink.ms", "" },
				{ "deploy.orchestration.package-check.system-app.ms", "" },
				{ "deploy.orchestration.package-check.evaluate.ms", "" },
				{ "deploy.orchestration.package-timestamp.path-stat.ms", "" },
				{ "deploy.orchestration.install.push-install.ms", "" },
				{ "deploy.orchestration.install.retry-delete.ms", "" },
				{ "deploy.orchestration.install.retry-uninstall.ms", "" },
				{ "deploy.orchestration.install.retry-reinstall.ms", "" },
				{ "deploy.orchestration.terminate.get-pid.ms", "" },
				{ "deploy.orchestration.terminate.kill.ms", "" },
				{ "deploy.app.file.transfer.mode", "" },
				{ "deploy.fastdeploy2.bulk.batches", "" },
				{ "deploy.symlink.created.files", "" },
				{ "deploy.symlink.removed.files", "" },
				{ "deploy.symlink.shell.update.ms", "" },
				{ "pii.deploy.error", "" },
				{ "pii.deploy.file", "" },
			};

			internal void SetProperty (string key, bool? value)
			{
				Properties [key] = value?.ToString () ?? "False";
			}

			internal void SetProperty (string key, int? value)
			{
				Properties [key] = value?.ToString () ?? "-1";
			}

			internal void SetProperty (string key, long? value)
			{
				Properties [key] = value?.ToString () ?? "-1";
			}

			internal void SetProperty (string key, string value)
			{
				Properties [key] = value ?? "unknown";
			}
		}

		protected void SetDiagnosticElapsed (string key, Stopwatch stopwatch)
		{
			diagnosticData.SetProperty (key, stopwatch.ElapsedMilliseconds);
		}

		protected void AddDiagnosticElapsed (string key, Stopwatch stopwatch)
		{
			if (!long.TryParse (diagnosticData.Properties [key], out long current)) {
				current = 0;
			}
			diagnosticData.SetProperty (key, current + stopwatch.ElapsedMilliseconds);
		}

		protected void SetDiagnosticProperty (string key, int value)
		{
			diagnosticData.SetProperty (key, value);
		}

		protected void SetDiagnosticProperty (string key, string value)
		{
			diagnosticData.SetProperty (key, value);
		}

		protected void LogDiagnostic (string message)
		{
			if (DiagnosticLogging) {
				LogDebugMessage (message);
				return;
			}
			lock (diagnosticLogsLock) {
				diagnosticLogs.Enqueue (message);
			}
		}

		void PrintDiagnostics ()
		{
			while (true) {
				string message;
				lock (diagnosticLogsLock) {
					if (diagnosticLogs.Count == 0) {
						break;
					}
					message = diagnosticLogs.Dequeue ();
				}
				LogMessage (message);
			}
			LogMessage ($"{diagnosticData.Task}");
			foreach (var t in diagnosticData.Properties) {
				LogMessage ($"\t{t.Key}: {t.Value}");
			}
		}

		void LogDiagnosticDataError (string errorCode, string error, string file = "")
		{
			diagnosticData.SetProperty ("deploy.result", "Failed");
			if (!string.IsNullOrEmpty (file))
				diagnosticData.SetProperty ("pii.deploy.file", file);
			diagnosticData.SetProperty ("pii.deploy.error", error);
			diagnosticData.SetProperty ("deploy.error.code", errorCode);
		}

		void SaveDiagnosticData (long ms)
		{
			diagnosticData.SetProperty ("deploy.duration.ms", ms);
			string newPath = Path.Combine (IntermediateOutputPath, "diagnostics", $"{GetType ().Name.ToLowerInvariant ()}.json");
			File.WriteAllText (newPath, JsonSerializer.Serialize (
				diagnosticData,
				typeof (DiagnosticData),
				FastDeploy2JsonSerializerContext.Default));
		}
	}
}
