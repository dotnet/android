using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

static class BoundaryChecks
{
	public static void ProjectToolsIsLibrary ()
	{
		Equal<System.Reflection.MethodInfo> (null, typeof (DownloadedCache).Assembly.EntryPoint);
	}

	public static void CachedDownload (string directory, Func<string, DownloadedCache> createCache)
	{
		Directory.CreateDirectory (directory);
		string file = Path.Combine (directory, "cached.txt");
		File.WriteAllText (file, "cached");
		var cache = createCache (directory);
		Equal (file, cache.GetAsFile ("http://127.0.0.1:1/cached.txt"));
		Equal ("cached", File.ReadAllText (file));
	}

	public static void ProcessEnvironment (Action<ProcessStartInfo, string, string> setEnvironmentVariable)
	{
		var startInfo = new ProcessStartInfo ();
		setEnvironmentVariable (startInfo, "HOST_BOUNDARY_TEST", "value");
		Equal ("value", startInfo.EnvironmentVariables ["HOST_BOUNDARY_TEST"]);
		setEnvironmentVariable (startInfo, "HOST_BOUNDARY_TEST", null);
		Equal (null, startInfo.EnvironmentVariables ["HOST_BOUNDARY_TEST"]);
	}

	public static async Task TransientDownload (string directory, Func<string, DownloadedCache> createCache)
	{
		await DownloadResponse (directory, createCache, 503, "");
		throw new InvalidOperationException ("A transient failure must not return a cache filename.");
	}

	public static async Task<string> DownloadResponse (string directory, Func<string, DownloadedCache> createCache, int status, string body)
	{
		using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (10));
		using var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		string url = $"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}/file";
		async Task Respond ()
		{
			using var client = await listener.AcceptTcpClientAsync (cancellation.Token);
			using var stream = client.GetStream ();
			using var reader = new StreamReader (stream, leaveOpen: true);
			while (!string.IsNullOrEmpty (await reader.ReadLineAsync (cancellation.Token))) {
			}
			string reason = status == 503 ? "Service Unavailable" : status == 200 ? "OK" : "Not Found";
			await stream.WriteAsync (Encoding.ASCII.GetBytes ($"HTTP/1.1 {status} {reason}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}"), cancellation.Token);
		}
		var server = Respond ();
		try {
			return createCache (directory).GetAsFile (url);
		} finally {
			await server;
		}
	}

	public static async Task IsolatedDownloadCallbacks (string directory)
	{
		async Task Run (string name)
		{
			var messages = new List<string> ();
			var expected = new IOException (name);
			try {
				await TransientDownload (Path.Combine (directory, name), path => new DownloadedCache (
					path, messages.Add, _ => throw expected));
			} catch (IOException error) when (ReferenceEquals (expected, error)) {
				Equal (true, messages.Any (message => message.Contains ("HTTP Status Code: 503 (ServiceUnavailable)")));
				return;
			}
			throw new InvalidOperationException ("The download did not use its own inconclusive callback.");
		}
		await Task.WhenAll (Task.Run (() => Run ("one")), Task.Run (() => Run ("two")));
	}

	public static void TestNames ()
	{
		Equal ("BuildTruearm64v8a", TestNameUtilities.GetDirectoryName ("Build(True,\"arm64_v8a\")"));
		Equal ("abc", TestNameUtilities.GetDirectoryName ("a_{b}$:'\";,=|c"));
		Equal ("row1", TestNameUtilities.GetDirectoryName ("row_1"));
		Equal ("row2", TestNameUtilities.GetDirectoryName ("row_2"));
	}

	public static void Cleanup (string directory, bool delete, Action<string, string> attach)
	{
		string root = Path.Combine (directory, "temp", "row");
		string nested = Path.Combine (root, "project", "obj");
		string sibling = Path.Combine (directory, "temp", "other-row");
		FileSystemUtils.SetDirectoryWriteable (root);
		Directory.CreateDirectory (nested);
		Directory.CreateDirectory (sibling);
		string log = Path.Combine (nested, "build.log");
		string binlog = Path.Combine (root, "build.binlog");
		File.WriteAllText (log, "log");
		File.WriteAllText (binlog, "binlog");
		File.WriteAllText (Path.Combine (root, "not-an-attachment.txt"), "keep");
		File.SetAttributes (log, File.GetAttributes (log) | FileAttributes.ReadOnly);
		var attachments = new List<string> ();
		using var output = new StringWriter ();
		FileSystemUtils.CleanupTestOutputDirectory (nested, delete, output, (file, description) => {
			Equal ("row", description);
			attachments.Add (file);
			attach (file, description);
		});
		Equal (!delete, Directory.Exists (root));
		Equal (true, Directory.Exists (sibling));
		Equal ("", output.ToString ());
		Equal (delete ? 0 : 2, attachments.Count);
		if (!delete) {
			Equal (true, attachments.SequenceEqual ([log, binlog]));
			Equal ("log", File.ReadAllText (log));
		}
	}

	public static void BuildEngineOutput (Action<string> log)
	{
		using var output = new StringWriter ();
		var errors = new List<BuildErrorEventArgs> ();
		var warnings = new List<BuildWarningEventArgs> ();
		var messages = new List<BuildMessageEventArgs> ();
		var engine = new MockBuildEngine (output, errors, warnings, messages);
		var error = new BuildErrorEventArgs ("", "XA0001", "", 0, 0, 0, 0, "error", "", "");
		var warning = new BuildWarningEventArgs ("", "XA0002", "", 0, 0, 0, 0, "warning", "", "");
		var message = new BuildMessageEventArgs ("message", "", "", MessageImportance.Normal);
		((IBuildEngine) engine).LogErrorEvent (error);
		((IBuildEngine) engine).LogWarningEvent (warning);
		((IBuildEngine) engine).LogMessageEvent (message);
		Equal ($"Error: error{Environment.NewLine}Warning: warning{Environment.NewLine}Message: message{Environment.NewLine}", output.ToString ());
		Equal (error, errors [0]);
		Equal (warning, warnings [0]);
		Equal (message, messages [0]);
		var objects = (IBuildEngine4) engine;
		objects.RegisterTaskObject ("key", engine, RegisteredTaskObjectLifetime.Build, false);
		Equal (engine, objects.GetRegisteredTaskObject ("key", RegisteredTaskObjectLifetime.Build));
		Equal (engine, objects.UnregisterTaskObject ("key", RegisteredTaskObjectLifetime.Build));
		Equal (null, objects.GetRegisteredTaskObject ("key", RegisteredTaskObjectLifetime.Build));
		log (output.ToString ());
	}

	public static void Bootstrap (string directory)
	{
		WriteBootstrap (directory);
		var result = EnvironmentHelper.ReadCoreClrBootstrap (directory);
		Equal ("package", result.Config.android_package_name);
		Equal ("value", result.EnvironmentVariables ["NAME"]);
		Equal (0, result.Preloads.Entries.Count);
		Equal (0, result.SystemProperties.Count);
	}

	public static void WriteBootstrap (string directory)
	{
		Directory.CreateDirectory (directory);
		byte [] data = new byte [112];
		void Write (int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian (data.AsSpan (offset, 4), value);
		Write (0, 0x47464358);
		Write (4, 2);
		Write (8, (uint) data.Length);
		Write (12, 1);
		Write (40, 84);
		Write (60, 92);
		Write (80, 104);
		Write (84, 93);
		Write (88, 98);
		System.Text.Encoding.UTF8.GetBytes ("NAME\0value\0package\0").CopyTo (data, 93);
		File.WriteAllBytes (Path.Combine (directory, "coreclr-bootstrap.bin"), data);
	}

	public static void CorruptBootstrap (string directory, int offset, uint value)
	{
		WriteBootstrap (directory);
		string path = Path.Combine (directory, "coreclr-bootstrap.bin");
		byte [] data = File.ReadAllBytes (path);
		BinaryPrimitives.WriteUInt32LittleEndian (data.AsSpan (offset, 4), value);
		File.WriteAllBytes (path, data);
	}

	public static void Equal<T> (T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals (expected, actual)) {
			throw new InvalidOperationException ($"Expected '{expected}', got '{actual}'.");
		}
	}
}
