﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class CreateAndroidEmulator : Task
	{
		public                  string          SdkVersion      {get; set;}
		public                  string          AndroidAbi      {get; set;}
		public                  string          AndroidSdkHome  {get; set;}
		public                  string          JavaSdkHome     {get; set;}

		public                  string          ToolPath        {get; set;}
		public                  string          ToolExe         {get; set;}

		public                  string          TargetId        {get; set;}

		public                  string          ImageName           {get; set;} = "XamarinAndroidTestRunner";

		public                  string          DataPartitionSizeMB {get; set;} = "2048";
		public                  string          RamSizeMB           {get; set;} = "2048";


		public override bool Execute ()
		{
			if (string.IsNullOrEmpty (TargetId) && !string.IsNullOrEmpty (SdkVersion)) {
				TargetId    = "system-images;android-" + SdkVersion + ";default;" + AndroidAbi;
			}
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (CreateAndroidEmulator)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidAbi)}: {AndroidAbi}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidSdkHome)}: {AndroidSdkHome}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (JavaSdkHome)}: {JavaSdkHome}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (ImageName)}: {ImageName}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (SdkVersion)}: {SdkVersion}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (TargetId)}: {TargetId}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (ToolExe)}: {ToolExe}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (ToolPath)}: {ToolPath}");

			Run (GetAndroidPath ());

			return !Log.HasLoggedErrors;
		}

		string GetAndroidPath ()
		{
			if (string.IsNullOrEmpty (ToolExe))
				ToolExe = "avdmanager";

			var dirs = string.IsNullOrEmpty (ToolPath)
				? null
				: new [] { ToolPath };
			string filename;
			var path = Which.GetProgramLocation (ToolExe, out filename, dirs);
			if (path == null) {
				Log.LogError ($"Could not find `avdmanager`. Please set the `{nameof (CreateAndroidEmulator)}.{nameof (ToolPath)}` property appropriately.");
				return null;
			}
			return path;
		}

		void Run (string android)
		{
			if (android == null)
				return;

			var arguments   = $"create avd --abi {AndroidAbi} -f -n {ImageName} --package \"{TargetId}\"";
			Exec (android, arguments);

			string configPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), ".android", "avd", $"{ImageName}.avd", "config.ini");
			if (!File.Exists (configPath)) {
				Log.LogWarning ($"Config file for AVD '{ImageName}' not found at {configPath}");
				Log.LogWarning ($"AVD '{ImageName}' will use default emulator settings (memory and data partition size)");
				return;
			}

			ulong diskSize;
			if (!UInt64.TryParse (DataPartitionSizeMB, out diskSize))
				Log.LogError ($"Invalid data partition size '{DataPartitionSizeMB}' - must be a positive integer value expressing size in megabytes");

			ulong ramSize;
			if (!UInt64.TryParse (RamSizeMB, out ramSize))
				Log.LogError ($"Invalid RAM size '{RamSizeMB}' - must be a positive integer value expressing size in megabytes");

			if (Log.HasLoggedErrors)
				return;

			File.AppendAllLines (configPath, new string[] {
				$"disk.dataPartition.size={diskSize}M",
				$"hw.ramSize={ramSize}"
			});
		}

		StreamWriter stdin;

		void Exec (string android, string arguments, DataReceivedEventHandler stderr = null)
		{
			Log.LogMessage (MessageImportance.Low, $"Tool {android} execution started with arguments: {arguments}");
			var psi = new ProcessStartInfo () {
				FileName                = android,
				Arguments               = arguments,
				UseShellExecute         = false,
				RedirectStandardInput   = true,
				RedirectStandardOutput  = false,
				RedirectStandardError   = false,
				CreateNoWindow          = true,
				WindowStyle             = ProcessWindowStyle.Hidden,
			};
			Log.LogMessage (MessageImportance.Low, $"Environment variables being passed to the tool:");
			if (!string.IsNullOrEmpty (AndroidSdkHome)) {
				psi.EnvironmentVariables ["ANDROID_SDK_HOME"] = AndroidSdkHome;
				Log.LogMessage (MessageImportance.Low, $"\tANDROID_SDK_HOME=\"{AndroidSdkHome}\"");
			}
			if (!string.IsNullOrEmpty (JavaSdkHome)) {
				psi.EnvironmentVariables ["JAVA_HOME"] = JavaSdkHome;
				Log.LogMessage (MessageImportance.Low, $"\tJAVA_HOME=\"{JavaSdkHome}\"");
			}
			var p = new Process () {
				StartInfo   = psi,
			};
			stderr  = stderr ?? DefaultErrorHandler;
			p.ErrorDataReceived     += stderr;

			using (p) {
				p.StartInfo = psi;
				p.Start ();
				stdin = p.StandardInput;

				while (!p.HasExited) {
					stdin.WriteLine ();
					p.WaitForExit (1000);
				}
				if (p.ExitCode != 0) {
					Log.LogError ($"Process `{android}` exited with value {p.ExitCode}.");
				}
			}
		}

		void DefaultErrorHandler (object sender, DataReceivedEventArgs e)
		{
			if (string.IsNullOrEmpty (e.Data))
				return;
			if (e.Data.StartsWith ("Warning:", StringComparison.Ordinal))
				Log.LogMessage ($"{e.Data}");
			else
				Log.LogError ($"{e.Data}");
		}
	}
}
