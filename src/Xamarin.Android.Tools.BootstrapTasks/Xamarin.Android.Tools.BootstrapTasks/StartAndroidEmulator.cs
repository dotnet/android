using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class StartAndroidEmulator : Task
	{
		[Output]
		public                  string          AdbTarget       {get; set;}

		[Output]
		public                  int             EmulatorProcessId       {get; set;}

		public                  string          AndroidSdkHome  {get; set;}
		public                  string          Port            {get; set;}
		public                  string          ImageName       {get; set;} = "XamarinAndroidUnitTestRunner";
		public                  string          ToolPath        {get; set;}
		public                  string          ToolExe         {get; set;}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (StartAndroidEmulator)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidSdkHome)}: {AndroidSdkHome}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (ImageName)}: {ImageName}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Port)}: {Port}");

			Run (GetEmulatorPath ());

			if (!string.IsNullOrEmpty (Port)) {
				AdbTarget   = $"-s emulator-{Port}";
			}

			return !Log.HasLoggedErrors;
		}

		string GetEmulatorPath ()
		{
			if (string.IsNullOrEmpty (ToolExe))
				ToolExe = "emulator";

			var dirs = string.IsNullOrEmpty (ToolPath)
				? null
				: new [] { ToolPath };
			string filename;
			var path = Which.GetProgramLocation (ToolExe, out filename, dirs);
			if (path == null) {
				Log.LogError ($"Could not find `emulator`. Please set the `{nameof (StartAndroidEmulator)}.{nameof (ToolExe)}` property appropriately.");
				return null;
			}
			return path;
		}

		void Run (string emulator)
		{
			if (emulator == null)
				return;

			var port = string.IsNullOrEmpty (Port) ? "" : $" -port {Port}";
			var arguments = $"-avd {ImageName}{port}";
			Log.LogMessage (MessageImportance.Low, $"Tool {emulator} execution started with arguments: {arguments}");
			var psi = new ProcessStartInfo () {
				FileName                = emulator,
				Arguments               = arguments,
				UseShellExecute         = false,
				CreateNoWindow          = true,
				WindowStyle             = ProcessWindowStyle.Hidden,
			};
			Log.LogMessage (MessageImportance.Low, $"Environment variables being passed to the tool:");
			var p = new Process () {
				StartInfo = psi,
			};
			p.Start ();
			EmulatorProcessId = p.Id;
		}
	}
}
