using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Invokes `bundletool` to create an APK set (.apks file)
	/// 
	/// Usage: bundletool build-apks --bundle=foo.aab --output=foo.apks
	/// </summary>
	public class BuildApkSet : BundleTool
	{
		public override string TaskPrefix => "BAS";

		[Required]
		public string AppBundle { get; set; }

		[Required]
		public string Output { get; set; }

		/// <summary>
		/// This is used to detect the attached device and generate an APK set specifically for it
		/// </summary>
		[Required]
		public string AdbToolPath { get; set; }

		public string AdbToolExe { get; set; }

		public string AdbToolName => OS.IsWindows ? "adb.exe" : "adb";

		[Required]
		public string Aapt2ToolPath { get; set; }

		public string Aapt2ToolExe { get; set; }

		public string Aapt2ToolName => OS.IsWindows ? "aapt2.exe" : "aapt2";

		[Required]
		public string KeyStore { get; set; }

		[Required]
		public string KeyAlias { get; set; }

		[Required]
		public string KeyPass { get; set; }

		[Required]
		public string StorePass { get; set; }

		public override bool RunTask ()
		{
			//NOTE: bundletool will not overwrite
			if (File.Exists (Output))
				File.Delete (Output);

			base.RunTask ();

			return !Log.HasLoggedErrors;
		}

		void AddStorePass (CommandLineBuilder cmd, string cmdLineSwitch, string value)
		{
			if (value.StartsWith ("env:", StringComparison.Ordinal)) {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} ", value);
			} else if (value.StartsWith ("file:", StringComparison.Ordinal)) {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} file:", value.Replace ("file:", string.Empty));
			} else {
				cmd.AppendSwitchIfNotNull ($"{cmdLineSwitch} pass:", value);
			}
		}

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var adb   = string.IsNullOrEmpty (AdbToolExe) ? AdbToolName : AdbToolExe;
			var aapt2 = string.IsNullOrEmpty (Aapt2ToolExe) ? Aapt2ToolName : Aapt2ToolExe;
			var cmd   = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("build-apks");
			cmd.AppendSwitch ("--connected-device");
			cmd.AppendSwitchIfNotNull ("--bundle ", AppBundle);
			cmd.AppendSwitchIfNotNull ("--output ", Output);
			cmd.AppendSwitchIfNotNull ("--mode ", "default");
			cmd.AppendSwitchIfNotNull ("--adb ", Path.Combine (AdbToolPath, adb));
			cmd.AppendSwitchIfNotNull ("--aapt2 ", Path.Combine (Aapt2ToolPath, aapt2));
			cmd.AppendSwitchIfNotNull ("--ks ", KeyStore);
			cmd.AppendSwitchIfNotNull ("--ks-key-alias ", KeyAlias);
			AddStorePass (cmd, "--key-pass", KeyPass);
			AddStorePass (cmd, "--ks-pass", StorePass);
			return cmd;
		}
	}
}
