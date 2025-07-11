#nullable enable

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class AndroidApkSigner : JavaToolTask
	{
		public override string TaskPrefix => "AAS";

		[Required]
		public string ApkSignerJar { get; set; } = "";

		[Required]
		public string ApkToSign { get; set; } = "";

		[Required]
		public ITaskItem ManifestFile { get; set; } = null!;

		public string? KeyStore { get; set; }

		public string? KeyAlias { get; set; }

		public string? PlatformKey { get; set; }

		public string? PlatformCert { get; set; }

		/// <summary>
		/// The Password for the Key.
		/// You can use the raw password here, however if you want to hide your password in logs
		/// you can use a preview of env: or file: to point it to an Environment variable or
		/// a file.
		///
		///   env:<PasswordEnvironentVariable>
		///   file:<PasswordFile>
		/// </summary>
		public string KeyPass { get; set; } = "";

		/// <summary>
		/// The Password for the Keystore.
		/// You can use the raw password here, however if you want to hide your password in logs
		/// you can use a preview of env: or file: to point it to an Environment variable or
		/// a file.
		///
		///   env:<PasswordEnvironentVariable>
		///   file:<PasswordFile>
		/// </summary>
		public string StorePass { get; set; } = "";

		public string? AdditionalArguments { get; set; }

		void AddStorePass (CommandLineBuilder cmd, string cmdLineSwitch, string value)
		{
			string pass = value.Replace ("env:", "")
				.Replace ("file:", "")
				.Replace ("pass:", "");
			if (value.StartsWith ("env:", StringComparison.Ordinal)) {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} env:", pass);
			}
			else if (value.StartsWith ("file:", StringComparison.Ordinal)) {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} file:", pass);
			} else {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} pass:", pass);
			}
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

			if (!PlatformKey.IsNullOrEmpty () && !PlatformCert.IsNullOrEmpty ()) {
				cmd.AppendSwitchIfNotNull ("--key ", PlatformKey);
				cmd.AppendSwitchIfNotNull ("--cert ", PlatformCert);
			} else {
				cmd.AppendSwitchIfNotNull ("--ks ", KeyStore);
				AddStorePass (cmd, "--ks-pass", StorePass);
				cmd.AppendSwitchIfNotNull ("--ks-key-alias ", KeyAlias);
				AddStorePass (cmd, "--key-pass", KeyPass);
			}

			cmd.AppendSwitchIfNotNull ("--min-sdk-version ", minSdk.ToString ());
			cmd.AppendSwitchIfNotNull ("--max-sdk-version ", maxSdk.ToString ());


			if (!AdditionalArguments.IsNullOrEmpty ())
				cmd.AppendSwitch (AdditionalArguments);

			cmd.AppendSwitchIfNotNull (" ", ApkToSign);

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

		protected override bool ValidateParameters ()
		{
			if (!PlatformKey.IsNullOrEmpty () && !PlatformCert.IsNullOrEmpty ()) {
				if (!File.Exists (PlatformKey)) {
					Log.LogCodedError ("XA4310", Properties.Resources.XA4310, "$(AndroidSigningPlatformKey)", PlatformKey);
					return false;
				}
				if (!File.Exists (PlatformCert)) {
					Log.LogCodedError ("XA4310", Properties.Resources.XA4310, "$(AndroidSigningPlatformCert)", PlatformCert);
					return false;
				}
			} else {
				if (!KeyStore.IsNullOrEmpty () && !File.Exists (KeyStore)) {
					Log.LogCodedError ("XA4310", Properties.Resources.XA4310, "$(AndroidSigningKeyStore)", KeyStore);
					return false;
				}
				if (KeyPass.IsNullOrEmpty ()) {
					Log.LogCodedError ("XA4314", Properties.Resources.XA4314, "$(AndroidSigningKeyPass)");
					return false;
				}
				if (StorePass.IsNullOrEmpty ()) {
					Log.LogCodedError ("XA4314", Properties.Resources.XA4314, "$(AndroidSigningStorePass)");
					return false;
				}
			}
			return base.ValidateParameters ();
		}
	}
}
