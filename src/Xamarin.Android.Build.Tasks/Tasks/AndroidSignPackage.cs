using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidSignPackage : AndroidRunToolTask
	{
		public override string TaskPrefix => "ASP";

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
		
		/// <summary>
		/// The Password for the Key.
		/// You can use the raw password here, however if you want to hide your password in logs
		/// you can use a preview of env: or file: to point it to an Environment variable or 
		/// a file.
		///
		///   env:<PasswordEnvironentVariable>
		///   file:<PasswordFile> 
		/// </summary>
		[Required]
		public string KeyPass { get; set; }
		
		/// <summary>
		/// The Password for the Keystore.
		/// You can use the raw password here, however if you want to hide your password in logs
		/// you can use a preview of env: or file: to point it to an Environment variable or 
		/// a file.
		///
		///   env:<PasswordEnvironentVariable>
		///   file:<PasswordFile> 
		/// </summary>
		[Required]
		public string StorePass { get; set; }

		public string TimestampAuthorityUrl { get; set; }

		public string TimestampAuthorityCertificateAlias { get; set; }

		/// <summary>
		/// -sigalg switch, which is SHA256withRSA by default. Previous versions of XA was md5withRSA.
		/// </summary>
		[Required]
		public string SigningAlgorithm { get; set; }

		/// <summary>
		/// -digestalg switch, which is SHA-256 by default. Previous versions of XA was SHA1.
		/// </summary>
		[Required]
		public string DigestAlgorithm { get; set; }

		public string FileSuffix { get; set; }

		protected override string DefaultErrorCode => "ANDJS0000";

		void AddStorePass (CommandLineBuilder cmd, string cmdLineSwitch, string value)
		{
			string pass = value.Replace ("env:", string.Empty).Replace ("file:", string.Empty);
			if (value.StartsWith ("env:", StringComparison.Ordinal)) {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch}:env ", pass);
			}
			else if (value.StartsWith ("file:", StringComparison.Ordinal)) {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch}:file ", pass);
			} else {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} ", pass);
			}
		}

		protected override string GenerateCommandLineCommands ()
		{
			var fileName = Path.GetFileNameWithoutExtension (UnsignedApk);
			var extension = Path.GetExtension (UnsignedApk);
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("-tsa ", TimestampAuthorityUrl);
			cmd.AppendSwitchIfNotNull ("-tsacert ", TimestampAuthorityCertificateAlias);
			cmd.AppendSwitchIfNotNull ("-keystore ", KeyStore);
			AddStorePass (cmd, "-storepass", StorePass);
			AddStorePass (cmd, "-keypass", KeyPass);
			cmd.AppendSwitchIfNotNull ("-digestalg ", DigestAlgorithm);
			cmd.AppendSwitchIfNotNull ("-sigalg ", SigningAlgorithm);
			cmd.AppendSwitchIfNotNull ("-signedjar ", Path.Combine (SignedApkDirectory, $"{fileName}{FileSuffix}{extension}" ));

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

