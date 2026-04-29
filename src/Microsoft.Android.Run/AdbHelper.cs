using System.Diagnostics;
using Xamarin.Android.Tools;

static class AdbHelper
{
	public static ProcessStartInfo CreateStartInfo (string adbPath, string? adbTarget, string arguments)
	{
		var fullArguments = string.IsNullOrEmpty (adbTarget) ? arguments : $"{adbTarget} {arguments}";
		return new ProcessStartInfo {
			FileName = adbPath,
			Arguments = fullArguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};
	}

	public static async Task<(int ExitCode, string Output, string Error)> RunAsync (string adbPath, string? adbTarget, string arguments, CancellationToken cancellationToken, bool verbose = false)
	{
		var psi = CreateStartInfo (adbPath, adbTarget, arguments);

		if (verbose)
			Console.WriteLine ($"Running: adb {psi.Arguments}");

		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken);

		return (exitCode, stdout.ToString (), stderr.ToString ());
	}
}
