#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Runs `keytool -genkeypair` to create the debug signing key.
	/// </summary>
	[MSBuildMultiThreadableTask]
	public class AndroidCreateDebugKey : MultithreadedAndroidTask
	{
		const string ErrorCode = "ANDKT0000";

		public override string TaskPrefix => "ACD";

		[Required]
		public string KeyStore { get; set; } = "";

		[Required]
		public string KeyAlias { get; set; } = "";

		[Required]
		public string KeyPass { get; set; } = "";

		[Required]
		public string StorePass { get; set; } = "";

		public int Validity { get; set; } = 30 * 365; // 30 years

		public string KeyAlgorithm { get; set; } = "RSA";

		public string StoreType { get; set; } = "pkcs12";

		public string? ToolPath { get; set; }

		public string? ToolExe { get; set; }

		public override bool RunTask ()
		{
			string keyStore = TaskEnvironment.GetAbsolutePath (KeyStore).Value;
			string? directory = Path.GetDirectoryName (keyStore);
			if (!directory.IsNullOrEmpty ())
				Directory.CreateDirectory (TaskEnvironment.GetAbsolutePath (directory));

			string toolExe = ToolExe.IsNullOrEmpty () ? (OS.IsWindows ? "keytool.exe" : "keytool") : ToolExe;
			string tool = ToolPath.IsNullOrEmpty () ? toolExe : TaskEnvironment.GetAbsolutePath (Path.Combine (ToolPath, toolExe)).Value;

			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("-genkeypair");
			cmd.AppendSwitchIfNotNull ("-alias ", KeyAlias);
			cmd.AppendSwitchIfNotNull ("-storepass ", StorePass);
			cmd.AppendSwitchIfNotNull ("-keypass ", KeyPass);
			cmd.AppendSwitchIfNotNull ("-keystore ", keyStore);
			cmd.AppendSwitch ("-dname \"CN=Android Debug,O=Android,C=US\"");
			cmd.AppendSwitchIfNotNull ("-keyalg ", KeyAlgorithm);
			cmd.AppendSwitchIfNotNull ("-validity ", Validity.ToString ());
			cmd.AppendSwitchIfNotNull ("-storetype ", StoreType);

			var output = new List<string> ();
			int exitCode = MonoAndroidHelper.RunProcess (TaskEnvironment, tool, cmd.ToString (),
				(s, e) => { if (e.Data != null) lock (output) output.Add (e.Data); },
				(s, e) => { if (e.Data != null) lock (output) output.Add (e.Data); });

			// Another build can create the shared debug key after the target's Exists() check:
			// keytool error: java.lang.Exception: Key pair not generated, alias <androiddebugkey> already exists
			bool alreadyExists = false;
			string? previousLine = null;
			foreach (var line in output) {
				// keytool can print "Warning:" and then the warning on the next line
				string text = line.Trim ();
				if (!text.IsNullOrEmpty () && !text.EndsWith (":", StringComparison.Ordinal)) {
					if (previousLine != null && previousLine.EndsWith (":", StringComparison.Ordinal))
						text = previousLine + " " + text;
					if (text.Contains ($"alias <{KeyAlias}> already exists")) {
						alreadyExists = true;
						Log.LogCodedWarning (ErrorCode, text.Replace ("error:", "warning:"));
					} else {
						Log.LogFromStandardError (ErrorCode, text);
					}
				}
				previousLine = text;
			}

			if (exitCode != 0 && !alreadyExists && !Log.HasLoggedErrors)
				Log.LogCodedError (ErrorCode, $"`{tool}` exited with code {exitCode}.");

			return !Log.HasLoggedErrors;
		}
	}
}