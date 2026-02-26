using System.Diagnostics;

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

	public static (int ExitCode, string Output, string Error) Run (string adbPath, string? adbTarget, string arguments, bool verbose = false)
	{
		var psi = CreateStartInfo (adbPath, adbTarget, arguments);

		if (verbose)
			Console.WriteLine ($"Running: adb {psi.Arguments}");

		using var process = Process.Start (psi);
		if (process == null)
			return (-1, "", "Failed to start process");

		// Read both streams asynchronously to avoid potential deadlock
		var outputTask = process.StandardOutput.ReadToEndAsync ();
		var errorTask = process.StandardError.ReadToEndAsync ();

		process.WaitForExit ();

		return (process.ExitCode, outputTask.Result, errorTask.Result);
	}
}
