using System;
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

		public                  string          ToolPath        {get; set;}
		public                  string          ToolExe         {get; set;}

		public                  string          TargetId        {get; set;}

		public                  string          ImageName       {get; set;} = "XamarinAndroidUnitTestRunner";

		public override bool Execute ()
		{
			if (string.IsNullOrEmpty (TargetId) && !string.IsNullOrEmpty (SdkVersion)) {
				TargetId    = "android-" + SdkVersion;
			}
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (CreateAndroidEmulator)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidAbi)}: {AndroidAbi}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidSdkHome)}: {AndroidSdkHome}");
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
				ToolExe = "android";

			var dirs = string.IsNullOrEmpty (ToolPath)
				? null
				: new [] { ToolPath };
			string filename;
			var path = Which.GetProgramLocation (ToolExe, out filename, dirs);
			if (path == null) {
				Log.LogError ($"Could not find `android`. Please set the `{nameof (CreateAndroidEmulator)}.{nameof (ToolPath)}` property appropriately.");
				return null;
			}
			return path;
		}

		void Run (string android)
		{
			if (android == null)
				return;

			var arguments   = $"create avd --abi \"{AndroidAbi}\" -f -n \"{ImageName}\" -t \"{TargetId}\"";
			foreach (var line in Exec (android, arguments)) {
				stdin.WriteLine ("no");
				Log.LogMessage (MessageImportance.Low, line);
			}
		}

		StreamWriter stdin;

		IEnumerable<string> Exec (string android, string arguments, DataReceivedEventHandler stderr = null)
		{
			Log.LogMessage (MessageImportance.Low, $"Tool {android} execution started with arguments: {arguments}");
			var psi = new ProcessStartInfo () {
				FileName                = android,
				Arguments               = arguments,
				UseShellExecute         = false,
				RedirectStandardInput   = true,
				RedirectStandardOutput  = true,
				RedirectStandardError   = true,
				CreateNoWindow          = true,
				WindowStyle             = ProcessWindowStyle.Hidden,
			};
			Log.LogMessage (MessageImportance.Low, $"Environment variables being passed to the tool:");
			if (!string.IsNullOrEmpty (AndroidSdkHome)) {
				psi.EnvironmentVariables ["ANDROID_SDK_HOME"] = AndroidSdkHome;
				Log.LogMessage (MessageImportance.Low, $"\tANDROID_SDK_HOME=\"{AndroidSdkHome}\"");
			}
			var p = new Process () {
				StartInfo   = psi,
			};
			stderr  = stderr ?? DefaultErrorHandler;
			p.ErrorDataReceived     += stderr;

			using (p) {
				p.StartInfo = psi;
				p.Start ();
				p.BeginErrorReadLine ();

				stdin = p.StandardInput;

				string line;
				while ((line = p.StandardOutput.ReadLine ()) != null) {
					yield return line;
				}

				p.WaitForExit ();
				if (p.ExitCode != 0) {
					Log.LogError ($"Process `{android}` exited with value {p.ExitCode}.");
				}
			}
		}

		void DefaultErrorHandler (object sender, DataReceivedEventArgs e)
		{
			if (string.IsNullOrEmpty (e.Data))
				return;
			Log.LogError ($"{e.Data}");
		}
	}
}
