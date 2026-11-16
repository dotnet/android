#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

// Helpers compile-linked into both Xamarin.Android.Build.Tasks and Microsoft.Android.Build.Tasks.
partial class MonoAndroidHelper
{
	public static int RunProcess (string name, string args, DataReceivedEventHandler onOutput, DataReceivedEventHandler onError, Dictionary<string, string>? environmentVariables = null)
	{
		return RunProcess (TaskEnvironment.Fallback, name, args, onOutput, onError, environmentVariables);
	}

	public static int RunProcess (TaskEnvironment taskEnvironment, string name, string args, DataReceivedEventHandler onOutput, DataReceivedEventHandler onError, Dictionary<string, string>? environmentVariables = null)
	{
		if (taskEnvironment == null)
			throw new ArgumentNullException (nameof (taskEnvironment));

		var psi = taskEnvironment.GetProcessStartInfo ();
		psi.FileName = name;
		psi.Arguments = args;
		psi.UseShellExecute = false;
		psi.RedirectStandardOutput = true;
		psi.RedirectStandardError = true;
		psi.CreateNoWindow = true;
		psi.WindowStyle = ProcessWindowStyle.Hidden;
		if (environmentVariables != null) {
			foreach (var pair in environmentVariables) {
				psi.EnvironmentVariables [pair.Key] = pair.Value;
			}
		}
		Process p = new Process ();
		p.StartInfo = psi;

		p.OutputDataReceived += onOutput;
		p.ErrorDataReceived += onError;
		p.Start ();
		p.BeginErrorReadLine ();
		p.BeginOutputReadLine ();
		p.WaitForExit ();
		try {
			return p.ExitCode;
		} finally {
			p.Close ();
		}
	}

	public static Dictionary<string, string> ItemGroupToCaseMap (ITaskItem [] items)
	{
		var map = new Dictionary<string, string> (items.Length, StringComparer.Ordinal);
		foreach (var item in items)
			map [item.ItemSpec] = item.GetMetadata ("OriginalName");
		return map;
	}

	public static string FixUpAndroidResourcePath (string file, string resourceDirectory, string resourceDirectoryFullPath, Dictionary<string, string> resource_name_case_map)
	{
		string? newfile = null;
		if (file.StartsWith (resourceDirectory, StringComparison.InvariantCultureIgnoreCase)) {
			newfile = file.Substring (resourceDirectory.Length).TrimStart (Path.DirectorySeparatorChar);
		}
		if (!resourceDirectoryFullPath.IsNullOrEmpty () && file.StartsWith (resourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)) {
			newfile = file.Substring (resourceDirectoryFullPath.Length).TrimStart (Path.DirectorySeparatorChar);
		}
		if (!newfile.IsNullOrEmpty ()) {
			if (resource_name_case_map.TryGetValue (newfile, out string? value))
				newfile = value;
			newfile = Path.Combine ("Resources", newfile);
			return newfile;
		}
		return string.Empty;
	}
}
