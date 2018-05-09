using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class KeyTool : AndroidToolTask
	{
		string previousLine;

		[Required]
		public string KeyStore { get; set; }

		[Required]
		public string KeyAlias { get; set; }

		[Required]
		public string KeyPass { get; set; }

		[Required]
		public string StorePass { get; set; }

		[Required]
		public string Command { get; set; }

		public bool Verbose { get; set; }

		protected override string DefaultErrorCode => "ANDKT0000";

		public override bool Execute ()
		{
			Log.LogDebugMessage ("KeyTool : {0}", Command);
			Log.LogDebugMessage ("          {0}",KeyStore);
			// Ensure the path where are going to write the key exists
			var store_dir = Path.GetDirectoryName (KeyStore);

			if (!Directory.Exists (store_dir))
				Directory.CreateDirectory (store_dir);

			return base.Execute ();
		}


		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			//NOTE: keytool tends to print "Warning:" followed by the actual warning on the next line
			//  The goal here is to combine these two lines into a single message
			string text = singleLine.Trim ();
			if (!string.IsNullOrEmpty (text) && !text.EndsWith (":", StringComparison.Ordinal)) {
				if (previousLine != null && previousLine.EndsWith (":", StringComparison.Ordinal)) {
					text = previousLine + " " + text;
				}
				Log.LogFromStandardError (DefaultErrorCode, text);
			}

			previousLine = text;
		}

		protected virtual CommandLineBuilder CreateCommandLine()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitch (Command);
			if (Verbose) {
				cmd.AppendSwitch ("-V");
			}
			cmd.AppendSwitchIfNotNull ("-alias ", KeyAlias);
			cmd.AppendSwitchIfNotNull ("-storepass ", StorePass);
			cmd.AppendSwitchIfNotNull ("-keypass ", KeyPass);
			cmd.AppendSwitchIfNotNull ("-keystore ", KeyStore);
			return cmd;
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = CreateCommandLine ();
			return cmd.ToString ();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string ToolName
		{
			get { return IsWindows ? "keytool.exe" : "keytool"; }
		}
	}
}

