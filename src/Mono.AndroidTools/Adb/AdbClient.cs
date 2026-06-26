//
// AdbClient.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright (c) 2010-2011 Novell, Inc.
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
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using Mono.AndroidTools.Util;
using System.Globalization;

namespace Mono.AndroidTools.Adb
{
	/// <summary>
	/// Client for the ADB server.
	/// </summary>
	/// <remarks>Can be accessed by multiple threads, but can only perform one operation at once.</remarks>
	public sealed class AdbClient : IDisposable
	{
		const int MAX_COMMAND = 4 * 1024;
		internal const int ADB_PORT = 5037;

		const int STATUS_OKAY = ((int)'O') << 24 | ((int)'K') << 16 | ((int)'A') << 8 | ((int)'Y'); // 'OKAY'
		const int STATUS_FAIL = ((int)'F') << 24 | ((int)'A') << 16 | ((int)'I') << 8 | ((int)'L'); // 'FAIL'

		internal static Encoding TextEncoding = new UTF8Encoding (false); //no BOM

		int port;
		IPAddress address;

		bool disposed;
		NetworkStream stream;
		TcpClient client;
		string deviceID;

		/// <summary>
		/// Creates an ADB client that connects to the local ADB server on the default port.
		/// </summary>
		public AdbClient ()
			: this (IPAddress.Loopback, ADB_PORT)
		{
		}

		/// <summary>
		/// Creates an ADB client that connects to the IP endpoint.
		/// </summary>
		public AdbClient (IPEndPoint endpoint) : this (endpoint.Address, endpoint.Port)
		{
		}

		/// <summary>
		/// Creates an ADB client that connects to the specified address and port.
		/// </summary>
		public AdbClient (IPAddress address, int port)
		{
			this.address = address;
			this.port = port;
		}

		/// <summary>
		/// Creates an ADB client using the specified stream.
		/// </summary>
		public AdbClient (NetworkStream stream)
		{
			this.stream = stream;
		}

		void CreateClient ()
		{
			CheckDisposed ();
			if (client != null || stream != null)
				throw new InvalidOperationException ("Already connected");
			client = new TcpClient ();
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("AdbClient");
		}

		void CheckConnected ()
		{
			CheckDisposed ();
			if (stream == null)
				throw new InvalidOperationException ("Not connected");
		}

		/// <summary>
		/// The network stream for the connection.
		/// </summary>
		public NetworkStream Stream {
			get {
				CheckConnected ();
				return stream;
			}
		}

		#region Connect

		public IAsyncResult BeginConnect (AsyncCallback callback, object state)
		{
			CreateClient ();
			return client.BeginConnect (address, port, callback, state);
		}

		public void EndConnect (IAsyncResult asyncResult)
		{
			try {
				CheckDisposed ();
				client.EndConnect (asyncResult);
				stream = client.GetStream ();
			} catch (Exception) {
				if (disposed && token.IsCancellationRequested)
					throw new OperationCanceledException (token);
				else throw;
			}
		}

		/// <summary>
		/// Connects to the ADB server.
		/// </summary>
		public void Connect ()
		{
			CreateClient ();
			client.Connect (address, port);
			stream = client.GetStream ();
		}

		#endregion

		#region ConnectTransport

		public IAsyncResult BeginConnectTransport (string deviceID, AsyncCallback callback, object state)
		{
			string transportCommand = GetTransportCommand (deviceID);
			var ar = new AggregateAsyncResult<string> (transportCommand, callback, state);
			BeginConnect (ConnectTransport_OnConnected, ar);
			return ar;
		}

		string GetTransportCommand (string deviceID)
		{
			this.deviceID = deviceID;
			if (deviceID == null)
				throw new ArgumentNullException ("device");

			if (deviceID == "any")
				return "host:transport-any";
			if (deviceID == "local")
				return "host:transport-local";
			if (deviceID == "usb")
				return "host:transport-usb";
			return "host:transport:" + deviceID;
		}

		void ConnectTransport_OnConnected (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<string>) result.AsyncState;
			try {
				EndConnect (result);
				BeginWriteCommandWithStatus (r.Arg, ConnectTransport_OnGotTransport, r);
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		void ConnectTransport_OnGotTransport (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result.AsyncState;
			try {
				EndWriteCommandWithStatus (result);
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		public void EndConnectTransport (IAsyncResult asyncResult)
		{
			var r = (AggregateAsyncResult) asyncResult;
			r.CheckError (token);
		}

		/// <summary>
		/// Connects to the ADB server and establishes a transport for the specified device.
		/// </summary>
		public void ConnectTransport (string deviceID)
		{
			string transportCommand = GetTransportCommand (deviceID);
			Connect ();
			WriteCommandWithStatus (transportCommand);
		}

		#endregion

		#region WriteCommand

		public IAsyncResult BeginWriteCommand (string command, AsyncCallback callback, object state)
		{
			CheckConnected ();
			var buf = GetCommandBuffer (command);
			return stream.BeginWrite (buf, 0, buf.Length, callback, state);
		}

		public void EndWriteCommand (IAsyncResult asyncResult)
		{
			try {
				stream.EndWrite (asyncResult);
			} catch (Exception) {
				if (disposed && token.IsCancellationRequested)
					throw new OperationCanceledException (token);
				else throw;
			}
		}

		/// <summary>
		/// Writes a command.
		/// </summary>
		public void WriteCommand (string command)
		{
			CheckConnected ();
			var buf = GetCommandBuffer (command);
			stream.Write (buf, 0, buf.Length);
		}

		static byte[] GetCommandBuffer (string command)
		{
			if (string.IsNullOrEmpty (command))
				throw new ArgumentException ("Command too short");

			if (command.Length > MAX_COMMAND)
				throw new ArgumentException ("Command too long");

			var bytes = AdbClient.TextEncoding.GetBytes (command);
			var len = AdbClient.TextEncoding.GetBytes (string.Format ("{0:x4}", bytes.Length));
			var all = new byte[bytes.Length + 4];
			len.CopyTo (all, 0);
			bytes.CopyTo (all, 4);
			return all;
		}

		#endregion

		#region WriteCommandWithStatus

		public IAsyncResult BeginWriteCommandWithStatus (string command, AsyncCallback callback, object state)
		{
			var ar = new CommandAsyncResult (true, false, callback, state);
			BeginWriteCommand (command, WriteCommandWithStatus_OnWroteCommand, ar);
			return ar;
		}

		void WriteCommandWithStatus_OnWroteCommand (IAsyncResult result)
		{
			var r = (CommandAsyncResult) result.AsyncState;
			try {
				CheckDisposed ();
				stream.EndWrite (result);
				if (r.ReadStatus) {
					BeginReadStatus (WriteCommandWithStatus_OnGotStatus, r);
				} else {
					r.Complete ();
				}
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		void WriteCommandWithStatus_OnGotStatus (IAsyncResult result)
		{
			var r = (CommandAsyncResult) result.AsyncState;
			try {
				r.Status = EndReadStatus (result);
				if (r.Status && !r.ReadMessage) {
					r.Complete ();
				} else {
					BeginReadStringWithLength (WriteCommandWithStatus_OnGotMessage, r);
				}
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		void WriteCommandWithStatus_OnGotMessage (IAsyncResult result)
		{
			var r = (CommandAsyncResult) result.AsyncState;
			try {
				r.Message = EndReadStringWithLength (result);
				if (r.Status) {
					r.Complete ();
				} else {
					r.CompleteWithError (MessageToException (r.Message));
				}
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		public void EndWriteCommandWithStatus (IAsyncResult asyncResult)
		{
			var r = (CommandAsyncResult) asyncResult;
			r.CheckError (token);
		}

		/// <summary>
		/// Writes a command and checks the status response.
		/// </summary>
		public void WriteCommandWithStatus (string command)
		{
			WriteCommand (command);
			if (!ReadStatus ()) {
				var msg = ReadStringWithLength ();
				throw MessageToException (msg);
			}
		}

		Exception MessageToException (string msg)
		{
			if (msg == "device not found") {
				return new DeviceNotFoundException (deviceID);
			}
			return new AdbException (msg);
		}

		private class CommandAsyncResult : AggregateAsyncResult
		{
			public CommandAsyncResult (bool readStatus, bool readMessage, AsyncCallback callback, object state)
				: base (callback, state)
			{
				this.ReadStatus = readStatus;
				this.ReadMessage = readMessage;
				if (readMessage && !readStatus)
					throw new ArgumentException ("Cannot read message without reading status");
			}

			public bool ReadStatus;
			public bool ReadMessage;
			public bool Status;
			public string Message;
		}

		#endregion

		#region WriteCommandWithMessage

		public IAsyncResult BeginWriteCommandWithMessage (string command, AsyncCallback callback, object state)
		{
			var ar = new CommandAsyncResult (true, true, callback, state);
			BeginWriteCommand (command, WriteCommandWithStatus_OnWroteCommand, ar);
			return ar;
		}

		public string EndWriteCommandWithMessage (IAsyncResult asyncResult)
		{
			var r = (CommandAsyncResult) asyncResult;
			r.CheckError (token);
			return r.Message;
		}

		/// <summary>
		/// Writes a command, checks the status response, and reads the response message.
		/// </summary>
		public string WriteCommandWithMessage (string command)
		{
			WriteCommandWithStatus (command);
			return ReadStringWithLength ();
		}

		#endregion

		#region GetStatus

		public IAsyncResult BeginReadStatus (AsyncCallback callback, object state)
		{
			CheckConnected ();
			var buf = new byte[4];
			var wrapper = new WrapperAsyncResult (callback, state, buf);
			wrapper.InnerResult = stream.BeginReadFull (buf, 0, 4, wrapper.WrapperCallback, wrapper);
			return wrapper;
		}

		public bool EndReadStatus (IAsyncResult ar)
		{
			var war = (WrapperAsyncResult) ar;
			try {
				stream.EndReadFull (war.InnerResult);
			} catch (Exception ex) {
				throw AdbClient.ConvertException (ex);
			}
			return InterpretStatus ((byte[])war.WrapperState);
		}

		bool InterpretStatus (byte[] buf)
		{
			int status = FourCCToInt (buf, 0);
			if (status == STATUS_OKAY)
				return true;
			if (status == STATUS_FAIL)
				return false;
			throw new AdbException ("Unknown ADB status: " + AdbClient.TextEncoding.GetString (buf));
		}

		/// <summary>
		/// Reads a status message and returns true for OKAY, false for FAIL. Throws on unknown value.
		/// </summary>
		public bool ReadStatus ()
		{
			CheckConnected ();
			var buf = new byte[4];
			stream.ReadFull (buf, 0, 4);
			return InterpretStatus (buf);
		}

		#endregion

		#region BeginReadStringWithLength

		public IAsyncResult BeginReadStringWithLength (AsyncCallback callback, object state)
		{
			CheckConnected ();
			var ar = new ReadFullAsyncResult (stream, new byte[4], callback, state);
			ar.BeginRead (0, 4, ReadStringWithLength_OnReadLength);
			return ar;
		}

		static void ReadStringWithLength_OnReadLength (IAsyncResult ar)
		{
			var r = (ReadFullAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Remaining > 0) {
					r.ContinueRead (ReadStringWithLength_OnReadLength);
					return;
				}
				int stringLength = int.Parse (TextEncoding.GetString (r.Buffer), NumberStyles.HexNumber);
				if (stringLength == 0) {
					r.Buffer = null;
					r.Complete ();
					return;
				}
				r.Buffer = new byte [stringLength];
				r.BeginRead (0, stringLength, ReadStringWithLength_OnRead);
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		static void ReadStringWithLength_OnRead (IAsyncResult ar)
		{
			var r = (ReadFullAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Remaining > 0) {
					r.ContinueRead (ReadStringWithLength_OnRead);
				} else {
					r.Complete ();
				}
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		public string EndReadStringWithLength (IAsyncResult asyncResult)
		{
			var r = (ReadFullAsyncResult) asyncResult;
			r.CheckError (token);
			if (r.Buffer == null)
				return string.Empty;
			return TextEncoding.GetString (r.Buffer);
		}

		/// <summary>
		/// Reads UTF8 text with the length specified by the first 4 bytes.
		/// </summary>
		public string ReadStringWithLength ()
		{
			CheckConnected ();
			var buf = new byte[4];
			stream.ReadFull (buf, 0, 4);
			var len = int.Parse (TextEncoding.GetString (buf), NumberStyles.HexNumber);

			// If we received a zero length, the read will still
			// block on it, so return nothing instead
			if (len == 0)
				return string.Empty;

			buf = new byte[len];
			stream.ReadFull (buf, 0, len);
			return TextEncoding.GetString (buf);
		}

		#endregion

		#region ReadText

		public IAsyncResult BeginReadText (Action<string> output, AsyncCallback callback, object state)
		{
			CheckConnected ();
			var ar = new TextWriterAsyncResult (output, new byte[1024], callback, state);
			stream.BeginRead (ar.Buffer, 0, ar.Buffer.Length, ReadText_OnReadText, ar);
			return ar;
		}

		void ReadText_OnReadText (IAsyncResult ar)
		{
			if (this.token.IsCancellationRequested) {
				// if we have a wrapper (which we will), then we need to complete that so that
				// the task that was created from it will complete and return - we end up in a state
				// where the task never completes
				var wrapper = ar.AsyncState as TextWriterAsyncResult;
				if (wrapper != null) {
					wrapper.CompleteWithError (new OperationCanceledException ());
				}

				return;
			}

			if (disposed)
				return;
			var r = (TextWriterAsyncResult) ar.AsyncState;
			try {
				var len = stream.EndRead (ar);
				if (len == 0) {
					r.Complete ();
					return;
				}
				r.Output (TextEncoding.GetString (r.Buffer, 0, len));
				stream.BeginRead (r.Buffer, 0, r.Buffer.Length, ReadText_OnReadText, r);
			} catch (Exception ex) {
				r.CompleteWithError (ConvertException (ex));
			}
		}

		public void EndReadText (IAsyncResult ar)
		{
			var r = (TextWriterAsyncResult) ar;
			r.CheckError (token);
		}

		/// <summary>
		/// Reads UTF8 text of unknown length.
		/// </summary>
		public void ReadText (Action<string> output)
		{
			var buf = new byte[1024];
			int len;
			while ((len = stream.Read (buf, 0, buf.Length)) > 0) {
				output (TextEncoding.GetString (buf, 0, len));
			}
		}

		public void ReadLineWhile (Action<string> output, Func<bool> predicate)
		{
			var reader = default(StreamReader);

			try {
				reader = new StreamReader (stream, TextEncoding, detectEncodingFromByteOrderMarks:true, bufferSize: 1024, leaveOpen: true);
				string line;
				while ((line = reader.ReadLine ()) != null) {
					if (!predicate ())
						return;

					output (line);
				}
			} catch (Exception e) {
				throw ConvertException (e);
			} finally {
				reader?.Dispose ();	
			}
		}

		class TextWriterAsyncResult : AggregateAsyncResult
		{
			public TextWriterAsyncResult (Action<string> output, byte[] buffer, AsyncCallback callback, object state)
				: base (callback, state)
			{
				this.Output = output;
				this.Buffer = buffer;
			}

			public Action<string> Output;
			public byte[] Buffer;
		}

		#endregion

		#region Utility

		/// <summary>
		/// Converts a four-character code (FourCC) from bytes in a buffer to an int.
		/// </summary>
		internal static int FourCCToInt (byte[] fourCC, int offset)
		{
			//not sure which is more efficient in C#. how much does pinning cost?
			//fixed (byte* b = fourCC) { return *((int*)(b + offset)); }
			return fourCC[0+offset] << 24 | fourCC[1+offset] << 16 | fourCC[2+offset] << 8 | fourCC[3+offset];
		}

		/// <summary>
		/// Converts a four-character code (FourCC) from an int to bytes in a buffer.
		/// </summary>
		internal static void IntToFourCC (int val, byte[] buf, int pos)
		{
			//not sure which is more efficient in C#. how much does pinning cost?
			//fixed (byte* b = buf) { *((int*)(buf + pos)) = val; }
			buf[pos]   = (byte) (val >> 24);
			buf[pos+1] = (byte) ((val >> 16) & 0x000000FF);
			buf[pos+2] = (byte) ((val >> 08) & 0x000000FF);
			buf[pos+3] = (byte) (val & 0x000000FF);
		}

		/// <summary>
		/// Converts a four-character code (FourCC) to a string for displaying in error messages.
		/// </summary>
		internal static string IntToFourCCString (int val)
		{
			byte[] buf = new byte [4];
			IntToFourCC (val, buf, 0);
			return TextEncoding.GetString (buf);
		}

		#endregion

		CancellationToken token;

		public void Dispose ()
		{
			Dispose (CancellationToken.None);
		}

		public void Dispose (CancellationToken token)
		{
			if (disposed)
				return;
			disposed = true;
			this.token = token;

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}

			if (client != null) {
				((IDisposable)client).Dispose ();
				client = null;
			}
		}

		public static string GetHostPrefix (string deviceID)
		{
			if (deviceID == "usb") {
				return "host-usb:";
			} else if (deviceID == "local") {
				return "host-local:";
			} else if (deviceID == "any") {
				return "host:";
			} else {
				return "host-serial:" + deviceID + ":";
			}
		}

		public static string GetWaitForDeviceCommand (string deviceID)
		{
			if (deviceID == "usb") {
				return "host-usb:wait-for-usb";
			} else if (deviceID == "local") {
				return "host-local:wait-for-local";
			} else if (deviceID == "any") {
				return "host:wait-for-any";
			} else {
				return "host-serial:" + deviceID + ":wait-for-device";
			}
		}

		internal static Exception ConvertException (Exception ex)
		{
			if (ex is ObjectDisposedException)
				return ex;

			var adbEx = ex as AdbException;
			if (adbEx != null)
				return adbEx;

			var s = ex as SocketException;
			if (s == null && ex is IOException) {
				s = ex.InnerException as SocketException;
			}
			if (s != null && s.SocketErrorCode == SocketError.Shutdown) {
				return new DeviceDisconnectedException (ex);
			}

			return new AdbException (ex);
		}
	}
}
