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

	/// <summary>
	/// Builds a <see cref="ProcessStartInfo"/> from a pre-split argument list.
	/// <see cref="ProcessStartInfo.ArgumentList"/> quotes each element for us, so
	/// values containing spaces or double quotes survive the trip through the
	/// operating system's command line parser untouched.
	/// </summary>
	public static ProcessStartInfo CreateStartInfo (string adbPath, string? adbTarget, IEnumerable<string> arguments)
	{
		var psi = new ProcessStartInfo {
			FileName = adbPath,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};
		if (!string.IsNullOrEmpty (adbTarget)) {
			// `adbTarget` is an already formatted switch such as `-s emulator-5554`.
			foreach (var part in adbTarget.Split (' ', StringSplitOptions.RemoveEmptyEntries))
				psi.ArgumentList.Add (part);
		}
		foreach (var argument in arguments)
			psi.ArgumentList.Add (argument);
		return psi;
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
