using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidSignPackage : AndroidToolTask
	{
		bool hasWarnings;

		[Required]
		public string UnsignedApk { get; set; }

		[Required]
		public string SignedApkDirectory { get; set; }

		[Required]
		[Output]
		public string KeyStore { get; set; }
		
		[Required]
		public string KeyAlias { get; set; }
		
		[Required]
		public string KeyPass { get; set; }
		
		[Required]
		public string StorePass { get; set; }

		public string TimestampAuthorityUrl { get; set; }

		public string TimestampAuthorityCertificateAlias { get; set; }

		public string SigningAlgorithm { get; set; }

		protected override string DefaultErrorCode => "ANDJS0000";

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-tsa ", TimestampAuthorityUrl);
			cmd.AppendSwitchIfNotNull ("-tsacert ", TimestampAuthorityCertificateAlias);
			cmd.AppendSwitchIfNotNull ("-keystore ", KeyStore);
			cmd.AppendSwitchIfNotNull ("-storepass ", StorePass);
			cmd.AppendSwitchIfNotNull ("-keypass ", KeyPass);
			cmd.AppendSwitchIfNotNull ("-digestalg ", "SHA1");
			cmd.AppendSwitchIfNotNull ("-sigalg ", string.IsNullOrWhiteSpace (SigningAlgorithm) ? "md5withRSA" : SigningAlgorithm);
			cmd.AppendSwitchIfNotNull ("-signedjar ", String.Format ("{0}{1}{2}-Signed-Unaligned.apk", SignedApkDirectory, Path.DirectorySeparatorChar, Path.GetFileNameWithoutExtension (UnsignedApk)));

			cmd.AppendFileNameIfNotNull (UnsignedApk);
			cmd.AppendSwitch (KeyAlias);

			return cmd.ToString ();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance importance)
		{
			singleLine = singleLine.Trim ();
			if (singleLine.Length == 0)
				return;

			if (singleLine.StartsWith ("Warning:")) {
				hasWarnings = true;
				return;
			}

			if (hasWarnings)
				Log.LogCodedWarning (DefaultErrorCode, GenerateFullPathToTool (), 0, singleLine);
			else
				Log.LogMessage (singleLine, importance);
		}

		protected override string ToolName
		{
			get { return IsWindows ? "jarsigner.exe" : "jarsigner"; }
		}
	}
}

