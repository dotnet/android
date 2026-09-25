using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

string? portText = Environment.GetEnvironmentVariable ("MANAGED_LAUNCH_TEST_ADB_PORT");
if (Environment.GetEnvironmentVariable ("MANAGED_LAUNCH_TEST_CHILD_MODE") == "stderr-spam-then-wait") {
	var line = new string ('x', 1024);
	for (int i = 0; i < 512; i++)
		Console.Error.WriteLine (line);
	await Task.Delay (TimeSpan.FromMinutes (5));
	return 0;
}
if (!int.TryParse (portText, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port <= 0) {
	Console.Error.WriteLine ("Missing MANAGED_LAUNCH_TEST_ADB_PORT.");
	return 1;
}

string command = string.Join (" ", args);
if (Environment.GetEnvironmentVariable ("MANAGED_LAUNCH_TEST_ADB_DAEMON_STARTUP") == "1" &&
		command.Contains ("get-serialno", StringComparison.Ordinal)) {
	Console.Error.WriteLine ("* daemon not running; starting now at tcp:5037");
	Console.Error.WriteLine ("* daemon started successfully");
}

using var client = new TcpClient ();
await client.ConnectAsync ("127.0.0.1", port);
using var stream = client.GetStream ();
byte [] request = Encoding.UTF8.GetBytes (command);
byte [] header = Encoding.ASCII.GetBytes (request.Length.ToString ("x4", CultureInfo.InvariantCulture));
await stream.WriteAsync (header);
await stream.WriteAsync (request);
await stream.FlushAsync ();

using var reader = new StreamReader (stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
string? statusLine = await reader.ReadLineAsync ();
if (!int.TryParse (statusLine, NumberStyles.Integer, CultureInfo.InvariantCulture, out int exitCode)) {
	Console.Error.WriteLine ("Invalid fake ADB response.");
	return 1;
}

while (await reader.ReadLineAsync () is string record) {
	if (record.Length == 0)
		continue;

	string payload = record.Substring (1);
	switch (record [0]) {
	case 'O':
		Console.WriteLine (payload);
		break;
	case 'o':
		Console.Write (payload);
		break;
	case 'E':
		Console.Error.WriteLine (payload);
		break;
	case 'e':
		Console.Error.Write (payload);
		break;
	default:
		Console.Error.WriteLine ("Invalid fake ADB response record.");
		return 1;
	}
}

return exitCode;
