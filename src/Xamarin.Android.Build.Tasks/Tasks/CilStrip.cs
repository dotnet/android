using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CilStrip : Task
	{
		[Required]
		public string AndroidAotMode { get; set; }

		[Required]
		public string ToolPath { get; set; }

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		public CilStrip ()
		{
		}

		public override bool Execute ()
		{
			try {
				return DoExecute ();
			} catch (Exception e) {
				Log.LogCodedError ("XA3001", "{0}", e);
				return false;
			}
		}

		bool DoExecute () {
			Log.LogDebugTaskItems ("  Targets:", ResolvedAssemblies);

			AotMode aotMode;
			bool hasValidAotMode = Aot.GetAndroidAotMode (AndroidAotMode, out aotMode);
			if (!hasValidAotMode) {
				Log.LogCodedError ("XA3001", "Invalid AOT mode: {0}", AndroidAotMode);
				return false;
			}

			// Create a directory to move the original non IL-stripped assemblies.
			string assembliesDir = Path.GetDirectoryName (ResolvedAssemblies.First ().ItemSpec);
			string nonstripDir = Path.Combine (assembliesDir, "non-stripped");

			if (!Directory.Exists (nonstripDir))
				Directory.CreateDirectory (nonstripDir);

			foreach (var assembly in ResolvedAssemblies) {
				string assemblyPath = Path.GetFullPath (assembly.ItemSpec);
				string nonstripPath = Path.Combine (nonstripDir, Path.GetFileName (assemblyPath));

				File.Move (assemblyPath, nonstripPath);

				if (!RunCilStrip (nonstripPath, assemblyPath)) {
					Log.LogCodedError ("XA3001", "Could not strip IL of assembly: {0}", assembly.ItemSpec);
					return false;
				}
			}

			return true;
		}
	
		bool RunCilStrip (string assembly, string output)
		{
			Log.LogMessage (MessageImportance.High, "[cil-strip] " + assembly);

			var arguments = string.Format("{0} {1}", assembly, output);

			var psi = new ProcessStartInfo () {
				FileName = Path.Combine (ToolPath, "cil-strip.exe"),
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			var proc = new Process ();
			proc.OutputDataReceived += OnOutputData;
			proc.ErrorDataReceived += OnErrorData;
			proc.StartInfo = psi;
			proc.Start ();

			proc.BeginOutputReadLine ();
			proc.BeginErrorReadLine ();
			proc.WaitForExit ();

			return proc.ExitCode == 0;
		}

		void OnOutputData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				Log.LogMessage ("[cil-strip stdout] {0}", e.Data);
		}

		void OnErrorData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				Log.LogMessage ("[cil-strip stderr] {0}", e.Data);
		}
	}
}
