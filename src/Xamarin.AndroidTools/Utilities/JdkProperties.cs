using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.AndroidTools
{
	public class JdkProperties
	{
		readonly static Dictionary<string, JdkProperties> propertiesCache = new Dictionary<string, JdkProperties> ();

		/// <summary>
		/// Enables caching of the results of querying the jsdkPath. This is false by default for backwards compatibility of 
		/// behaviour.
		/// </summary>
		public static bool Cached = false;

		public static bool ValidateBeforeQuery = false;

		public string Vendor { get; set; }

		public string Version { get; set; }
	
		public static JdkProperties Get(string jsdkPath=null)
		{
			var javaPath = GetJavaPath (jsdkPath);

			lock (propertiesCache) {
				// have we already queried this sdk before?
				if (Cached && propertiesCache.ContainsKey(javaPath)) {
					// yep, return this value
					return propertiesCache [javaPath];
				}

				// no, let's query it then, but first lets see if this is the 
				// default macOS java that prompts the user to install java
				if (ValidateBeforeQuery) {
					if (!AnySystemJavasInstalled() && (javaPath == "/usr/bin/java" || javaPath == "java")) {
						// there are no system javas detected, if jsdkPath looks like the default
						// path, then do not attempt to query
						return new JdkProperties ();
					}
				}

				var props = QueryProperties (javaPath);

				propertiesCache [javaPath] = props;
				return props;
			}
		}

		/// <summary>
		/// Resets and clears the cache of properties
		/// </summary>
		public static void Reset()
		{
			lock (propertiesCache) {
				propertiesCache.Clear ();
			}
		}

		static bool AnySystemJavasInstalled()
		{
			if (Mono.AndroidTools.Util.Platform.IsMac) {
				string path = Path.Combine (Path.DirectorySeparatorChar + "System", "Library", "Java", "JavaVirtualMachines");
				if (!Directory.Exists (path)) {
					return false;
				}

				string [] dirs = Directory.GetDirectories (path);
				if (dirs == null || dirs.Length == 0) {
					return false;
				}
			}

			return true;
		}

		static JdkProperties QueryProperties (string javaPath)
		{
			var props = new JdkProperties ();

			var processStartInfo = new ProcessStartInfo (javaPath, "-XshowSettings:properties -version");
			processStartInfo.UseShellExecute = false;
			processStartInfo.RedirectStandardInput = false;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;

			var p = new Process () {
				StartInfo = processStartInfo,
			};

			p.OutputDataReceived += (sender, e) =>
			{
				ProcessData (props, e.Data);
			}; ;

			p.ErrorDataReceived += (sender, e) =>
			{
				ProcessData (props, e.Data);
			};

			using (p) {
				p.Start ();
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();
				p.WaitForExit ();
			}

			return props;
		}

		static void ProcessData(JdkProperties props, string data)
		{
			try
			{
				int eq = data?.IndexOf ("= ", StringComparison.Ordinal) ?? 0;
				var propName = eq > 0
					? data.Substring (0, eq).Trim ()
					: data;
				if (string.Equals ("java.vm.vendor", propName, StringComparison.Ordinal))
					props.Vendor = GetValue (data);
				else if (string.Equals ("java.version", propName, StringComparison.Ordinal) )
					props.Version = GetValue (data);
			} catch {
				// data was discarded
			}
		}

		static string GetValue(string data)
		{
			var start = data.IndexOf("= ") + 2;
			var end = data.Length;
			return data.Substring(start, end - start).Trim();
		}

		static string GetJavaPath(string javaSdkPath)
		{
			string java = "java";

			if (!string.IsNullOrEmpty(javaSdkPath))
			{
				java = Path.Combine(javaSdkPath, "bin");
				java = Path.Combine(java, "java");
				if (!File.Exists(java))
					java += ".exe";
				if (!File.Exists(java))
					java = "java";
			}

			return java;
		}
	}
}
