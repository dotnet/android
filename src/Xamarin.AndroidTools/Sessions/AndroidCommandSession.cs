//
// AndroidCommandConnection.cs
//
// Author:
//       Rodrigo Moya <rodrigo.moya@xamarin.com>
//       Stephen Shaw <stephen.shaw@xamarin.com>
//
// Based on Xamarin.MacDev.IPhoneCommandConnection
// Author:
//	Michael Hutchinson <mhutch@xamarin.com>
//	Rolf Bjarne Kvinge <rolf@xamarin.com>
//
// Copyright (c) 2011-2014 Xamarin Inc.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Mono.AndroidTools;
using Mono.AndroidTools.Util;

namespace Xamarin.AndroidTools
{
	public abstract class AndroidCommandSession : IDisposable
	{
		readonly object commandSessionLock = new object ();
		readonly List<Stream> streams = new List<Stream> ();
		Stream reusableStream;
		bool disposed;
		bool connected_once;

		public IPAddress Address {
			get;
			protected set;
		}

		public int Port {
			get;
			protected set;
		}

		protected AndroidCommandSession (IPAddress ipAddress, int port = 0)
		{
			Address = ipAddress ?? IPAddress.Loopback;
			Port = port;
		}

		#region Internal command execution methods

		protected abstract IAsyncResult BeginConnectStream (AsyncCallback callback, object state);
		protected abstract Stream EndConnectStream (IAsyncResult result);

		IAsyncResult BeginExecuteCommand (string command, bool consumeStream, AsyncCallback callback = null, object state = null)
		{
			var data = Encoding.UTF8.GetBytes (command);
			if (data.Length > byte.MaxValue)
					throw new ArgumentException (string.Format ("Command '{0}' has length {1}, which exceeds maximum length {2}", command, data.Length, byte.MaxValue), "command");

			var buffer = new byte [data.Length + 1];
			buffer [0] = (byte) data.Length;
			Array.Copy (data, 0, buffer, 1, data.Length);

			var ar = new CommandAsyncResult (callback, state) {
				Buffer = buffer,
				ConsumeStream = consumeStream,
			};

			//try to re-use an existing stream
			lock (commandSessionLock) {
				if (reusableStream != null) {
					ar.Stream = reusableStream;
					//if we're going to consume the stream, don't leave it around for others to re-use
					if (consumeStream) {
						reusableStream = null;
					}
				}
			}

			if (ar.Stream != null) {
				ExecuteCommand_BeginWriteCommand (ar);
			} else {
				BeginConnectStream (ExecuteCommand_ConnectedCommandStream, ar);
			}

			return ar;
		}

		void ExecuteCommand_ConnectedCommandStream (IAsyncResult ar)
		{
			var r = (CommandAsyncResult) ar.AsyncState;
			try {
				r.Stream = EndConnectStream (ar);
				ExecuteCommand_BeginWriteCommand (r);
			} catch (Exception ex) {
				r.CompleteWithError (ex);
			}
		}

		static void ExecuteCommand_DiscardStream (Stream stream)
		{
			var discard = new byte [] { 7, (byte) 'd', (byte) 'i', (byte) 's', (byte) 'c', (byte) 'a', (byte) 'r', (byte) 'd' };
			stream.BeginWrite (discard, 0, discard.Length, ar => ((Stream)ar.AsyncState).Dispose (), stream);
		}

		void ExecuteCommand_BeginWriteCommand (CommandAsyncResult r)
		{
			lock (commandSessionLock)
				connected_once = true;
			r.Stream.BeginWrite (r.Buffer, 0, r.Buffer.Length, ExecuteCommand_WroteCommand, r);
		}

		void ExecuteCommand_WroteCommand (IAsyncResult ar)
		{
			var r = (CommandAsyncResult) ar.AsyncState;
			try {
				r.Stream.EndWrite (ar);
				//if the stream can be re-used, keep it
				if (!r.ConsumeStream) {
					lock (commandSessionLock) {
						if (reusableStream == null) {
							reusableStream = r.Stream;
							streams.Add (r.Stream);
							r.Stream = null;
						}
					}
					//if there was already a re-usable stream from another thread, discard this one
					if (r.Stream != null && r.Stream != reusableStream) {
						ExecuteCommand_DiscardStream (r.Stream);
					}
				}
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (ex);
			}
		}

		void CancelExecuteCommand (IAsyncResult asyncResult)
		{
			((CommandAsyncResult)asyncResult).Cancel ();
		}

		Stream EndExecuteCommand (IAsyncResult result)
		{
			var r = (CommandAsyncResult) result;
			r.CheckError ();
			return r.ConsumeStream? r.Stream : null;
		}

		IAsyncResult BeginSendSkipDebugger (AsyncCallback callback = null, object state = null)
		{
			return BeginExecuteCommand ("start debugger: no", consumeStream: false, callback: callback, state: state);
		}

		void WriteProfilerOutputToFile (Stream stream, string outputFile)
		{
			try {
				using (var fs = new FileStream (outputFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) {
					stream.CopyTo (fs);
				}
			} catch (Exception ex) {
				AndroidLogger.LogWarning ("Exception in the profile-data reading thread: {0}", ex);
			}

			Console.WriteLine ("Finished writer thread");
		}

		#endregion

		#region CommandAsyncResult

		class CommandAsyncResult : AggregateAsyncResult
		{
			public CommandAsyncResult (AsyncCallback callback, object state) : base (callback, state)
			{
			}

			public void Cancel ()
			{
				CompleteWithError (new OperationCanceledException ());
				if (Stream != null) {
					Stream.Dispose ();
					Stream = null;
				}
			}

			public byte[] Buffer;
			public bool ConsumeStream;
			public Stream Stream;
		}

		#endregion

		#region Public API

		public bool IsConnected {
			get {
				lock (commandSessionLock) {
					return connected_once;
				}
			}
		}

		public IAsyncResult BeginHandshake (AsyncCallback callback = null, object state = null)
		{
			var r = new AggregateAsyncResult<Stream,byte[]> (callback, state);
			BeginExecuteCommand ("ping", false, Handshake_SentPing, r);
			return r;
		}

		void Handshake_SentPing (IAsyncResult ar)
		{
			var r = (AggregateAsyncResult<Stream,byte[]>) ar.AsyncState;
			try {
				EndExecuteCommand (ar);
				//we know that there will be a reusable string if the ping succeeded
				//and nothing should be touching the tream during our handshake
				r.Arg1 = reusableStream;
				r.Arg2  = new byte[5];
				r.Arg1.BeginReadFull (r.Arg2, 0, r.Arg2.Length, Handshake_ReadPong, r);
			} catch (Exception ex) {
				r.CompleteWithError (ex);
			}
		}

		static void Handshake_ReadPong (IAsyncResult ar)
		{
			var r = (AggregateAsyncResult<Stream,byte[]>) ar.AsyncState;
			try {
				r.Arg1.EndReadFull (ar);
				var pong = Encoding.ASCII.GetString (r.Arg2);
				if (pong != "pong\0")
					throw new Exception ("Bad handshake: '" + pong + "'");
				r.Complete();
			} catch (Exception ex) {
				r.CompleteWithError (ex);
			}
		}

		public void EndHandshake (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<Stream,byte[]>) result;
			r.CheckError ();
		}

		public void StartLogProfiler (string outputFile, string profilerConfiguration, string heapshotMode, AsyncCallback callback = null, object state = null)
		{
			EndExecuteCommand (BeginSendSkipDebugger ());

			var outputDir = Path.GetDirectoryName (outputFile);
			Directory.CreateDirectory (outputDir);

			var stream = EndExecuteCommand (BeginExecuteCommand ("start profiler: " + profilerConfiguration, true, callback, state));
			new Thread (() => {
				try {
					WriteProfilerOutputToFile (stream, outputFile);
				} finally {
					stream.Dispose ();
				}
			}) {
				IsBackground = true,
				Name = "Profiler output writer"
			}.Start ();
		}

		public void Stop ()
		{
			try {
				// Don't try forever to send 'exit process', the user might never have
				// started the app on the device.
				var exit = true;

				lock (commandSessionLock) {
					exit = connected_once;
				}

				if (exit && !disposed) {
					var ar = BeginExecuteCommand ("exit process", consumeStream: true);
					ar.AsyncWaitHandle.WaitOne (100);
					EndExecuteCommand (ar);
				}
			} catch (ObjectDisposedException) {
			} catch (SocketException ex) {
				Console.WriteLine ("Error while requesting the application to exit: " + ex.Message);
			} finally {
				Dispose (); // make sure everything is cleaned up
			}
		}

		#endregion

		#region IDisposable implementation

		~AndroidCommandSession ()
		{
			Dispose (false);
		}

		public void Dispose ()
		{
			if (disposed)
				return;

			lock (commandSessionLock) {
				if (disposed)
					return;
				disposed = true;
				GC.SuppressFinalize (this);
			}

			Dispose (true);
		}

		protected abstract void Dispose (bool disposing);

		#endregion
	}
}
