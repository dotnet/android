using System.Text;
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

		var path = Encoding.UTF8.GetString (Convert.FromBase64String (args [1]));
		File.AppendAllText (path + ".calls", JsonSerializer.Serialize (args) + Environment.NewLine);
		var response = JsonSerializer.Deserialize<Response> (File.ReadAllText (path))
			?? throw new InvalidOperationException ("Missing fake adb response.");
		if (args.Length > 2 && args [2] == "push") {
			if (response.PushExitCode != 0) {
				Console.Error.Write ("fake adb push failed");
				return response.PushExitCode;
			}
			File.Copy (args [3], path + ".uids", overwrite: true);
			return 0;
		}
		if (args.Length > 3 && args [2] == "shell" && args [3] == "rm")
			return 0;
		if (args.Contains (InstrumentationProtocol.TestUidsDirectoryRequest)) {
			if (response.FilterDirectory == null) {
				Console.Write ("INSTRUMENTATION_RESULT: error=File filters are unsupported\nINSTRUMENTATION_CODE: 0\n");
				return 0;
			}
			Console.Write ($"INSTRUMENTATION_RESULT: {InstrumentationProtocol.KeyTestUidsDirectory}={response.FilterDirectory}\nINSTRUMENTATION_CODE: -1\n");
			Console.Error.Write (response.Error);
			return 0;
		}
		File.WriteAllText (path + ".args", JsonSerializer.Serialize (args));
		Console.Write (response.Output);
		Console.Error.Write (response.Error);
		return response.ExitCode;
	}

	internal sealed record Response (string Output, string Error = "", int ExitCode = 0,
		string? FilterDirectory = "/storage/emulated/10/Android/data/test.package/files", int PushExitCode = 0);
}
