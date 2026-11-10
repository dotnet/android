#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public partial class MonoAndroidHelper
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
}
