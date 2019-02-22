using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class DexUtils
	{
		/// <summary>
		/// Runs the dexdump command to see if a class exists in a dex file
		///     dexdump returns data of the form:
		///     Class descriptor  : 'Landroid/app/ActivityTracker;'
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		public static bool ContainsClass (string className, string dexFile, string androidSdkDirectory)
		{
			bool containsClass = false;
			DataReceivedEventHandler handler = (s, e) => {
				if (e.Data != null && e.Data.Contains ("Class descriptor") && e.Data.Contains (className))
					containsClass = true;
			};

			var androidSdk = new AndroidSdkInfo ((l, m) => {
					Console.WriteLine ($"{l}: {m}");
					if (l == TraceLevel.Error) {
						throw new Exception (m);
					}
				}, androidSdkDirectory);
			var buildToolsPath = androidSdk.GetBuildToolsPaths ().FirstOrDefault ();
			if (string.IsNullOrEmpty (buildToolsPath)) {
				throw new Exception ($"Unable to find build-tools in `{androidSdkDirectory}`!");
			}

			var psi = new ProcessStartInfo {
				FileName = Path.Combine (buildToolsPath, "dexdump"),
				Arguments = $"\"{dexFile}\"",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
			};
			var builder = new StringBuilder ();
			using (var p = new Process { StartInfo = psi }) {
				p.ErrorDataReceived += handler;
				p.OutputDataReceived += handler;

				p.Start ();
				p.BeginErrorReadLine ();
				p.BeginOutputReadLine ();
				p.WaitForExit ();
			}

			return containsClass;
		}
	}
}
