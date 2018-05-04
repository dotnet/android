using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class AndroidApkSigner : JavaToolTask
	{
		[Required]
		public string ApkSignerJar { get; set; }

		[Required]
		public string ApkToSign { get; set; }

		[Required]
		public ITaskItem ManifestFile { get; set; }

		[Required]
		public string KeyStore { get; set; }

		[Required]
		public string KeyAlias { get; set; }

		[Required]
		public string KeyPass { get; set; }

		[Required]
		public string StorePass { get; set; }

		public string AdditionalArguments { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("AndroidApkSigner:");
			Log.LogDebugMessage ("  ApkSignerJar: {0}", ApkSignerJar);
			Log.LogDebugMessage ("  ApkToSign: {0}", ApkToSign);
			Log.LogDebugMessage ("  ManifestFile: {0}", ManifestFile);
			Log.LogDebugMessage ("  AdditionalArguments: {0}", AdditionalArguments);

			if (!File.Exists (GenerateFullPathToTool ())) {
				Log.LogError ($"'{GenerateFullPathToTool ()}' does not exist. You need to install android-sdk build-tools 26.0.1 or above.");
				return false;
			}

			return base.Execute ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			var manifest = AndroidAppManifest.Load (ManifestFile.ItemSpec, MonoAndroidHelper.SupportedVersions);
			int minSdk = MonoAndroidHelper.SupportedVersions.MinStableVersion.ApiLevel;
			int maxSdk = MonoAndroidHelper.SupportedVersions.MaxStableVersion.ApiLevel;
			if (manifest.MinSdkVersion.HasValue)
				minSdk = manifest.MinSdkVersion.Value;

			if (manifest.TargetSdkVersion.HasValue)
				maxSdk = manifest.TargetSdkVersion.Value;

			minSdk = Math.Min (minSdk, maxSdk);

			cmd.AppendSwitchIfNotNull ("-jar ", ApkSignerJar);
			cmd.AppendSwitch ("sign");
			cmd.AppendSwitchIfNotNull ("--ks ", KeyStore);
			cmd.AppendSwitchIfNotNull ("--ks-pass pass:", StorePass);
			cmd.AppendSwitchIfNotNull ("--ks-key-alias ", KeyAlias);
			cmd.AppendSwitchIfNotNull ("--key-pass pass:", KeyPass);
			cmd.AppendSwitchIfNotNull ("--min-sdk-version ", minSdk.ToString ());
			cmd.AppendSwitchIfNotNull ("--max-sdk-version ", maxSdk.ToString ());
		
			if (!string.IsNullOrEmpty (AdditionalArguments))
				cmd.AppendSwitch (AdditionalArguments);

			cmd.AppendSwitchIfNotNull (" ", Path.GetFullPath (ApkToSign));

			return cmd.ToString ();
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance importance)
		{
			singleLine = singleLine.Trim ();
			if (singleLine.Length == 0)
				return;

			if (singleLine.StartsWith ("Warning:", StringComparison.OrdinalIgnoreCase)) {
				Log.LogCodedWarning ("ANDAS0000", ApkSignerJar, 0, singleLine);
				return;
			}

			Log.LogMessage (singleLine, importance);
		}

		protected override string ToolName {
			get { return OS.IsWindows ? "java.exe" : "java"; }
		}
	}
}
