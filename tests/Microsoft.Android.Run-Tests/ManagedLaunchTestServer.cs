// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Android.Run.Tests
{
	internal sealed class ManagedLaunchTestServer : IDisposable, IAsyncDisposable
	{
		readonly TcpListener listener = new TcpListener (IPAddress.Loopback, 0);
		readonly CancellationTokenSource stop = new CancellationTokenSource ();
		readonly List<Task> connections = [];
		readonly Task accepting;

		public ManagedLaunchTestServer (string packageName, string serial = "managed-launch-test")
		{
			Serial = serial;
			State = new ManagedLaunchTestState (packageName);
			listener.Start ();
			accepting = AcceptAsync ();
		}

		public ManagedLaunchTestState State { get; }

		public string Serial { get; }

		public int Port => ((IPEndPoint) listener.LocalEndpoint).Port;

		public ConcurrentQueue<string> Commands => State.Commands;

		public ConcurrentQueue<string> AdbCommands { get; } = new ConcurrentQueue<string> ();

		async Task AcceptAsync ()
		{
			try {
				while (!stop.IsCancellationRequested) {
					var client = await listener.AcceptTcpClientAsync (stop.Token);
					connections.Add (RespondAsync (client));
				}
			} catch (OperationCanceledException) when (stop.IsCancellationRequested) {
			}
		}

		async Task RespondAsync (TcpClient client)
		{
			try {
				using (client) {
					var stream = client.GetStream ();
					string request = await ReadCommandAsync (stream);
					if (!request.StartsWith ("host:transport:", StringComparison.Ordinal)) {
						AdbCommands.Enqueue (request);
						foreach (string target in new [] { $"-s {Serial} ", "-d ", "-e " }) {
							if (request.StartsWith (target, StringComparison.Ordinal)) {
								request = request.Substring (target.Length);
								break;
							}
						}
						if (request.StartsWith ("shell ", StringComparison.Ordinal))
							request = request.Substring (6);

						var reply = await State.RespondToCommandAsync (request, stop.Token);
						var result = reply.Success ? State.CliResult (request) : (ExitCode: 1, Error: reply.Output);
						if (reply.Success && request.StartsWith ("pidof ", StringComparison.Ordinal) &&
								string.IsNullOrWhiteSpace (reply.Output) && result.ExitCode == 0 && string.IsNullOrWhiteSpace (result.Error))
							result = (1, "");

						var cliResponse = new StringBuilder ().Append (result.ExitCode.ToString (CultureInfo.InvariantCulture)).Append ('\n');
						AppendCliOutput (cliResponse, 'O', reply.Success ? reply.Output : "");
						AppendCliOutput (cliResponse, 'E', result.Error);
						await stream.WriteAsync (Encoding.UTF8.GetBytes (cliResponse.ToString ()), stop.Token);
						return;
					}

					if (request != "host:transport:" + Serial)
						throw new InvalidOperationException (request);

					await stream.WriteAsync (Encoding.ASCII.GetBytes ("OKAY"), stop.Token);
					string command = await ReadCommandAsync (stream);
					if (!command.StartsWith ("shell:", StringComparison.Ordinal))
						throw new InvalidOperationException (command);

					var response = await State.RespondToCommandAsync (command.Substring (6), stop.Token);
					string status = response.Success
						? "OKAY"
						: "FAIL" + Encoding.UTF8.GetByteCount (response.Output).ToString ("X4", CultureInfo.InvariantCulture);
					await stream.WriteAsync (Encoding.UTF8.GetBytes (status + response.Output), stop.Token);
				}
			} catch (OperationCanceledException) when (stop.IsCancellationRequested) {
			}
		}

		static void AppendCliOutput (StringBuilder response, char channel, string output)
		{
			string [] lines = output.Split ('\n');
			for (int i = 0; i < lines.Length; i++) {
				bool last = i == lines.Length - 1;
				if (last && lines [i].Length == 0)
					break;
				response.Append (last ? char.ToLowerInvariant (channel) : channel).Append (lines [i]).Append ('\n');
			}
		}

		async Task<string> ReadCommandAsync (NetworkStream stream)
		{
			var header = new byte [4];
			await stream.ReadExactlyAsync (header, stop.Token);
			int length = int.Parse (Encoding.ASCII.GetString (header), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			var data = new byte [length];
			await stream.ReadExactlyAsync (data, stop.Token);
			return Encoding.UTF8.GetString (data);
		}

		public async ValueTask DisposeAsync ()
		{
			stop.Cancel ();
			await accepting;
			listener.Stop ();
			await Task.WhenAll (connections);
			stop.Dispose ();
		}

		public void Dispose ()
		{
			DisposeAsync ().AsTask ().GetAwaiter ().GetResult ();
		}
	}
}
