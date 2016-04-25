using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class KeyTool : AndroidToolTask
	{
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

