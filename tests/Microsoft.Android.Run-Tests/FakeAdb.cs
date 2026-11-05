using System.Text.Json;

namespace Microsoft.Android.Run.Tests;

// The test executable doubles as adb so the real adapter's process and streaming
// paths can be exercised on every host without an Android device.
static class FakeAdb
{
	public static int Main (string[] args)
	{
		if (args.Length < 2 || args [0] != "-s") {
			Console.Error.WriteLine ("Expected -s followed by the fake adb response file.");
			return 1;
		}

		var path = args [1];
		File.WriteAllText (path + ".args", JsonSerializer.Serialize (args));
		var response = JsonSerializer.Deserialize<Response> (File.ReadAllText (path))
			?? throw new InvalidOperationException ("Missing fake adb response.");
		Console.Write (response.Output);
		Console.Error.Write (response.Error);
		return response.ExitCode;
	}

	internal sealed record Response (string Output, string Error = "", int ExitCode = 0);
}
