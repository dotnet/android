//
// AndroidTasks.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright (c) 2011 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;

using Mono.AndroidTools.Internal;
using Mono.AndroidTools.Adb;
using System.Threading;

namespace Mono.AndroidTools
{
	public class AdbServer
	{
		static AdbServer instance = new AdbServer ();

		IPAddress address;
		int port;

		public AdbServer ()
			: this (IPAddress.Loopback, AdbClient.ADB_PORT)
		{
		}

		public AdbServer (IPAddress address, int port)
		{
			this.address = address;
			this.port = port;
		}

		public static AdbServer Default {
			get {
				return instance;
			}
		}

		internal AdbClient CreateClient (CancellationToken cancellationToken)
		{
			var client = new AdbClient (address, port);
			if (cancellationToken.CanBeCanceled)
				client.MakeCancellable (cancellationToken);
			return client;
		}

		internal AdbSyncClient CreateSyncClient (CancellationToken cancellationToken)
		{
			var client = new AdbSyncClient (address, port);
			if (cancellationToken.CanBeCanceled)
				client.MakeCancellable (cancellationToken);
			return client;
		}

		internal Task<string> RunCommandWithMessage (string command, CancellationToken cancellationToken)
		{
			var client = CreateClient (cancellationToken);
			return client.ConnectAsync ()
				.ContinueWith (t => {
					t.Wait ();
					return client.WriteCommandWithMessageAsync (command);
				}, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
				.Unwrap ()
				.Cleanup (client, cancellationToken);
		}

		internal Task RunCommandWithStatus (string command, CancellationToken cancellationToken)
		{
			var client = CreateClient (cancellationToken);
			return client.ConnectAsync ()
				.ContinueWith (t => {
					t.Wait ();
					return client.WriteCommandWithStatusAsync (command);
				}, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
				.Unwrap ()
				.Cleanup (client, cancellationToken);
		}

		/// <summary>
		/// Run the command, first connecting using the specified device serial for the transport. Some commands,
		/// like reverse, don't support the host-serial prefix, requiring using this instead to specify the device.
		/// </summary>
		internal Task TransportRunCommandWithStatus (string deviceID, string command, CancellationToken cancellationToken)
		{
			var client = CreateClient (cancellationToken);
			return client.ConnectTransportAsync (deviceID)
				.ContinueWith (t => {
					t.Wait ();
					return client.WriteCommandWithStatusAsync (command);
				}, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
				.Unwrap ()
				.Cleanup (client, cancellationToken);
		}

		const string SUPPORTED_PROTOCOL = "001a";

		public Task<string> GetProtocolVersion ()
		{
			return GetProtocolVersion (CancellationToken.None);
		}

		public Task<string> GetProtocolVersion (CancellationToken cancellationToken)
		{
			return RunCommandWithMessage ("host:version", cancellationToken);
		}

		public Task CheckProtocolVersion ()
		{
			return CheckProtocolVersion (CancellationToken.None);
		}

		public Task CheckProtocolVersion (CancellationToken cancellationToken)
		{
			return GetProtocolVersion (cancellationToken).ContinueWith (t => {
				if (t.Result != SUPPORTED_PROTOCOL)
					throw new AdbException ("Unsupported adb protocol: " + t.Result);
			}, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}

		public Task<List<AndroidDevice>> GetDevices ()
		{
			return GetDevices (CancellationToken.None);
		}

		public async Task<List<AndroidDevice>> GetDevices (CancellationToken cancellationToken)
		{
			var result = await RunCommandWithMessage ("host:devices-l", cancellationToken).ConfigureAwait (false);
			cancellationToken.ThrowIfCancellationRequested ();
			return AdbOutputParsing.ParseDeviceList(result);
		}

		public Task KillServer ()
		{
			var client = CreateClient (CancellationToken.None);
			return client.ConnectAsync ()
				.ContinueWith (t => {
					t.Wait ();
					return client.WriteCommandAsync ("host:kill");
				}, TaskContinuationOptions.ExecuteSynchronously)
				.Unwrap ()
				.Cleanup (client, CancellationToken.None);
		}

		public Task TrackDevices (Action<List<AndroidDevice>> action)
		{
			return TrackDevices (action, CancellationToken.None);
		}

		public Task TrackDevices (Action<List<AndroidDevice>> action, CancellationToken cancellationToken)
		{
			var client = CreateClient (cancellationToken);
			return new TrackDeviceTask (client, action, cancellationToken).Start ();
		}

		public Task ForwardPort (AndroidDevice device, int localPort, int remotePort)
		{
			return ForwardPort (device, localPort, remotePort, CancellationToken.None);
		}

		public Task ForwardPort (AndroidDevice device, int localPort, int remotePort, CancellationToken cancellationToken)
		{
			var command = string.Format ("host-serial:{0}:forward:tcp:{1};tcp:{2}", device.ID, localPort, remotePort);
			return RunCommandWithStatus (command, cancellationToken);
		}

		public Task ForwardPort (AndroidDevice device, string localProtocol, int localPort, string remoteProtocol, int remotePort, CancellationToken cancellationToken)
		{
			var command = $"host-serial:{device.ID}:forward:{localProtocol}:{localPort};{remoteProtocol}:{remotePort}";
			return RunCommandWithStatus (command, cancellationToken);
		}

		public Task KillForward (AndroidDevice device, string localProtocol, int localPort, CancellationToken cancellationToken)
		{
			var command = $"host-serial:{device.ID}:killforward:{localProtocol}:{localPort}";
			return RunCommandWithStatus (command, cancellationToken);
		}

		public Task ReversePort (AndroidDevice device, int remotePort, int localPort, CancellationToken cancellationToken)
		{
			var command = $"reverse:forward:tcp:{remotePort};tcp:{localPort}";
			return TransportRunCommandWithStatus (device.ID, command, cancellationToken);
		}

		public Task ReversePort (AndroidDevice device, string remoteProtocol, int remotePort, string localProtocol, int localPort, CancellationToken cancellationToken)
		{
			var command = $"reverse:forward:{remoteProtocol}:{remotePort};{localProtocol}:{localPort}";
			return TransportRunCommandWithStatus (device.ID, command, cancellationToken);
		}

		public Task KillReverse (AndroidDevice device, string remoteProtocol, int remotePort, CancellationToken cancellationToken)
		{
			var command = $"reverse:killforward:{remoteProtocol}:{remotePort}";
			return TransportRunCommandWithStatus (device.ID, command, cancellationToken);
		}
	}
}
