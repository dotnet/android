using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Diagnostics;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AdjustJavacVersionArguments : ToolTask
	{
		[Required]
		public string ToolPath { get; set; }

		public bool EnableProguard { get; set; }

		public bool EnableMultiDex { get; set; }

		[Output]
		public string TargetVersion { get; set; }

		[Output]
		public string SourceVersion { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "javac.exe" : "javac"; }
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ToolPath: {0}", ToolPath);
			Log.LogDebugMessage ("ToolExe: {0}", ToolExe);
			Log.LogDebugMessage ("EnableProguard: {0}", EnableProguard);
			Log.LogDebugMessage ("EnableMultiDex: {0}", EnableMultiDex);

			// so far only proguard matters.
			if (!EnableProguard && !EnableMultiDex)
				return true;

			var psi = new ProcessStartInfo (Path.Combine (ToolPath, ToolExe ?? ToolName), "-version") {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				};
			var proc = Process.Start (psi);
			proc.WaitForExit ();
			var line = proc.StandardError.ReadLine ();
			if (!line.StartsWith ("javac "))
				// otherwise ignore.
				return true;

			var version = line.Substring (6);

			if (version.StartsWith ("1.8")) {
				TargetVersion = SourceVersion = "1.7";
				Log.LogDebugMessage ("Javac TargetVersion adjusted to: {0}", TargetVersion);
				Log.LogDebugMessage ("Javac SourceVersion adjusted to: {0}", SourceVersion);
			}

			return true;
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}
	}
}

