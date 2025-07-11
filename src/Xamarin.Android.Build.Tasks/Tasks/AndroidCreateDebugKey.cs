#nullable enable

using System;
using System.IO;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidCreateDebugKey : KeyTool
	{
		public override string TaskPrefix => "ACD";

		public int Validity { get; set; } = 30 * 365; // 30 years

		public string KeyAlgorithm { get; set; } = "RSA";

		public string StoreType { get; set; } = "pkcs12";

		string dname = "CN=Android Debug,O=Android,C=US";

		public AndroidCreateDebugKey ()
		{
			TaskResources = new ResourceManager ("Microsoft.Build.Utilities.Core.Strings", typeof (ToolTask).Assembly);
		}

		protected override CommandLineBuilder CreateCommandLine ()
		{
			var cmd = base.CreateCommandLine ();
			cmd.AppendSwitchIfNotNull ("-dname ", dname);
			cmd.AppendSwitchIfNotNull ("-keyalg ", KeyAlgorithm);
			cmd.AppendSwitchIfNotNull ("-validity ", Validity.ToString()); 
			cmd.AppendSwitchIfNotNull ("-storetype ", StoreType);
			return cmd;
		}

		bool warnExitCode = false;

		protected override void LogFromStandardError (string text)
		{
			// Downgrade error message to warning:
			// error: java.lang.Exception: Key pair not generated, alias <androiddebugkey> already exists
			if (text.Contains ($"alias <{KeyAlias}> already exists")) {
				text = text.Replace ("error:", "warning:");
				warnExitCode = true;
			}

			base.LogFromStandardError (text);
		}

		protected override bool HandleTaskExecutionErrors ()
		{
			// Warn for the exit code if we get:
			// error: java.lang.Exception: Key pair not generated, alias <androiddebugkey> already exists
			if (warnExitCode && !Log.HasLoggedErrors) {
				Log.LogWarningFromResources ("ToolTask.ToolCommandFailed", ToolExe, ExitCode);
				return true;
			}

			return base.HandleTaskExecutionErrors ();
		}
	}
}

