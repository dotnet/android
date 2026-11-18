#nullable enable

using System;
using System.Globalization;
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
	public class AndroidCreateDebugKey : MultithreadedAndroidToolTask
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

		protected override string ToolName => OS.IsWindows ? "keytool.exe" : "keytool";

		string? previousLine;
		bool alreadyExists;

		public override bool RunTask ()
		{
			string? directory = Path.GetDirectoryName (TaskEnvironment.GetAbsolutePath (KeyStore).Value);
			if (!directory.IsNullOrEmpty ())
				Directory.CreateDirectory (TaskEnvironment.GetAbsolutePath (directory));

			return base.RunTask ();
		}

		protected override string GenerateFullPathToTool () => ToolExe;

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitch ("-genkeypair");
			cmd.AppendSwitchIfNotNull ("-alias ", KeyAlias);
			cmd.AppendSwitchIfNotNull ("-storepass ", StorePass);
			cmd.AppendSwitchIfNotNull ("-keypass ", KeyPass);
			cmd.AppendSwitchIfNotNull ("-keystore ", TaskEnvironment.GetAbsolutePath (KeyStore).Value);
			cmd.AppendSwitch ("-dname \"CN=Android Debug,O=Android,C=US\"");
			cmd.AppendSwitchIfNotNull ("-keyalg ", KeyAlgorithm);
			cmd.AppendSwitchIfNotNull ("-validity ", Validity.ToString(CultureInfo.CurrentCulture));
			cmd.AppendSwitchIfNotNull ("-storetype ", StoreType);
			return cmd.ToString ();
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			// keytool can print "Warning:" and then the warning on the next line
			string text = singleLine.Trim ();
			if (!text.IsNullOrEmpty () && !text.EndsWith (":", StringComparison.Ordinal)) {
				if (previousLine != null && previousLine.EndsWith (":", StringComparison.Ordinal))
					text = previousLine + " " + text;
				// Another build can create the shared debug key after the target's Exists() check:
				// keytool error: java.lang.Exception: Key pair not generated, alias <androiddebugkey> already exists
				if (text.Contains ($"alias <{KeyAlias}> already exists")) {
					alreadyExists = true;
					Log.LogCodedWarning (ErrorCode, text.Replace ("error:", "warning:"));
				} else {
					Log.LogFromStandardError (ErrorCode, text);
				}
			}
			previousLine = text;
		}

		protected override bool HandleTaskExecutionErrors ()
		{
			if (alreadyExists && !Log.HasLoggedErrors)
				return true;
			return base.HandleTaskExecutionErrors ();
		}
	}
}