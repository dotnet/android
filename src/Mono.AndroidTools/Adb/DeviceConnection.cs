//
// DeviceConnection.cs
//
// Author:
//       Michael Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2014 Xamarin Inc.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Mono.AndroidTools.Util;

namespace Mono.AndroidTools.Adb
{
	// Connects directly to the adb daemon on a device/emulator and
	// forwards streams.
	//
	// NOTE: The ADB daemon only responds to one connection, though it
	//   will accept multiple connections. Therefore if the adb server
	//   is already connected to it, this will not work.
	//
	// LIMITATIONS: This does not implement any services itself. In
	//   addition, it doesn't perform any buffering so throughput will
	//   be bad.
	//
	// USAGE:
	//   var c = new TcpClient ();
	//   c.Connect (IPAddress.Loopback, 5555);
	//
	//   var pc = new DeviceConnection ();
	//   pc.Connect (c.GetStream ());
	//
	//   var s = pc.OpenShell ();
	//   var tw = new StreamWriter (s);
	//   tw.Write ("ls \n\0");
	//   tw.Flush ();
	//
	//   etc.
	//
	public class DeviceConnection : IDisposable
	{
		const uint A_SYNC = 0x434e5953;
		const uint A_CNXN = 0x4e584e43;
		const uint A_AUTH = 0x48545541;
		const uint A_OPEN = 0x4e45504f;
		const uint A_OKAY = 0x59414b4f;
		const uint A_CLSE = 0x45534c43;
		const uint A_WRTE = 0x45545257;

		const uint A_VERSION = 0x01000000;

		const int MAX_PAYLOAD = 4096;
		const int HEADER_SIZE = 24;

		int targetMaxPayload;

		byte[] writeBuffer = new byte[MAX_PAYLOAD + HEADER_SIZE];

		Stream stream;
		bool disposed;

		string ident;
		uint nextLocalID = 1;
		Exception readError;

		Dictionary<uint,ServiceStream> streams = new Dictionary<uint, ServiceStream>();

		public void Connect (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");
			if (disposed)
				throw new ObjectDisposedException ("DeviceConnection");
			if (this.stream != null)
				throw new InvalidOperationException ("Already connected");
			this.stream = stream;

			string identity = "host::";
			int length = AdbClient.TextEncoding.GetBytes (identity, 0, identity.Length, writeBuffer, HEADER_SIZE);
			writeBuffer [length++] = 0;
			uint checksum = Checksum (writeBuffer, HEADER_SIZE, length);

			CreateMessage (writeBuffer, A_CNXN, A_VERSION, MAX_PAYLOAD, length, checksum);

			stream.Write (writeBuffer, 0, HEADER_SIZE + length);

			var thread = new Thread (ReadLoop) { IsBackground = true };
			thread.Start ();
		}

		public void Dispose ()
		{
			if (disposed)
				return;
			disposed = true;

			stream.Dispose ();
			stream = null;

			foreach (var s in streams)
				s.Value.Dispose ();

			streams = null;
		}

		void CheckError ()
		{
			if (readError != null)
				throw new Exception ("Error reading from device", readError);
			if (disposed)
				throw new ObjectDisposedException ("DeviceConnection");
		}

		public Stream OpenStream (string serviceName)
		{
			CheckError ();

			var s = new ServiceStream (this, nextLocalID++);
			streams [s.LocalID] = s;

			int length = AdbClient.TextEncoding.GetBytes (serviceName, 0, serviceName.Length, writeBuffer, HEADER_SIZE);
			writeBuffer [length++] = 0;
			uint checksum = Checksum (writeBuffer, HEADER_SIZE, length);

			Debug.WriteLine ("W OKAY local {0}", s.LocalID);

			CreateMessage (writeBuffer, A_OPEN, s.LocalID, 0, length, checksum);
			stream.Write (writeBuffer, 0, HEADER_SIZE + length);

			SetReady (s);

			return s;
		}

		public Stream OpenTcp (short port)
		{
			return OpenStream ("tcp:" + port);
		}

		public Stream OpenLocal (string name)
		{
			return OpenStream ("local:" + name);
		}

		public Stream OpenLocalReserved (string name)
		{
			return OpenStream ("localreserved:" + name);
		}

		public Stream OpenLocalAbstract (string name)
		{
			return OpenStream ("localabstract:" + name);
		}

		public Stream OpenLocalFileSystem (string name)
		{
			return OpenStream ("localfilesystem:" + name);
		}

		public Stream OpenDev (string name)
		{
			return OpenStream ("dev:" + name);
		}

		public Stream OpenFrameBuffer ()
		{
			return OpenStream ("framebuffer:");
		}

		public Stream OpenJdwp (int pid)
		{
			return OpenStream ("jdwp:" + pid);
		}

		public Stream OpenShell (string command = null)
		{
			return OpenStream ("shell:" + command);
		}

		public Stream OpenSync ()
		{
			return OpenStream ("sync:");
		}

		public Stream OpenRemount ()
		{
			return OpenStream ("remount:");
		}

		public Stream OpenReboot (string arg = null)
		{
			return OpenStream ("reboot:" + arg);
		}

		public Stream OpenRoot ()
		{
			return OpenStream ("root:");
		}

		public Stream OpenBackup (string arg = null)
		{
			return OpenStream ("backup:" + arg);
		}

		public Stream OpenRestore()
		{
			return OpenStream ("restore:");
		}

		public Stream OpenTcpip (short port = 0)
		{
			return OpenStream ("tcpip:" + port);
		}

		public Stream OpenUsb ()
		{
			return OpenStream ("usb:");
		}

		public Stream OpenReverse (string cookie)
		{
			return OpenStream ("reverse:");
		}

		void Close (ServiceStream stream)
		{
			if (streams.Remove (stream.LocalID)) {
				CreateMessage (writeBuffer, A_CLSE, stream.LocalID, stream.RemoteID, 0, 0);
				stream.Write (writeBuffer, 0, HEADER_SIZE);
			}
		}

		void ReadLoop ()
		{
			byte[] readBuffer = new byte[HEADER_SIZE];

			try {
				while (!disposed) {
					stream.ReadFull (readBuffer, 0, HEADER_SIZE);

					uint command, arg0, arg1, checksum;

					int length;
					ReadMessage (readBuffer, out command, out arg0, out arg1, out length, out checksum);

					byte[] payload = null;
					if (length > 0) {
						payload = new byte[length];
						stream.ReadFull (payload, 0, length);
						if (Checksum (payload, 0, length) != checksum)
							throw new Exception ("Checksum failed");
					}

					if (ident == null) {
						if (command != A_CNXN)
							throw new Exception ("Not yet identified");
						if (arg0 != A_VERSION)
							throw new Exception ("Version mismatch, got " + arg0);
						targetMaxPayload = (int)arg1;
						ident = AdbClient.TextEncoding.GetString (payload);
						Debug.WriteLine ("CNXN");
						continue;
					}

					switch (command) {
					case A_OKAY:
						HandleOkay (arg1, arg0);
						continue;
					case A_WRTE:
						HandleWrite (arg1, payload);
						continue;
					case A_CLSE:
						HandleClose (arg1, arg0);
						continue;
					case A_AUTH:
					case A_SYNC:
					case A_OPEN:
						throw new NotSupportedException ();
					case A_CNXN:
						throw new Exception ("Already identified");
					default:
						throw new Exception ("Unknown command " + command);
					}
				}
			} catch (Exception ex) {
				readError = ex;
			}
		}

		void HandleClose (uint localID, uint remoteID)
		{
			Debug.WriteLine ("R CLSE local {0} remote {1}", localID, remoteID);

			ServiceStream value;
			if (!streams.TryGetValue (localID, out value))
				return;
			value.Dispose ();
		}

		void HandleOkay (uint localID, uint remoteID)
		{
			Debug.WriteLine ("R OKAY local {0} remote {1}", localID, remoteID);

			ServiceStream value;
			if (!streams.TryGetValue (localID, out value))
				return;
			value.WriteEvent.Set ();
			value.RemoteID = remoteID;
		}

		void HandleWrite (uint localID, byte[] payload)
		{
			Debug.WriteLine ("R WRTE local {0} payload {1}", localID, payload.Length);

			ServiceStream value;
			if (!streams.TryGetValue (localID, out value))
				return;

			if (value.Incoming != null) {
				// not ready, invalid
				value.Dispose ();
				return;
			}

			value.Incoming = payload;
			value.ReadEvent.Set ();
		}

		void SetReady (ServiceStream s)
		{
			Debug.WriteLine ("W OKAY local {0} payload {1}", s.LocalID, s.RemoteID);

			var buf = new byte[HEADER_SIZE];
			CreateMessage (buf, A_OKAY, s.LocalID, s.RemoteID, 0, 0);
			stream.Write (buf, 0, HEADER_SIZE);
		}

		static void CreateMessage (byte[] buf, uint command, uint arg0, uint arg1, int length, uint checksum)
		{
			UIntToBytes (command,  buf, 0);
			UIntToBytes (arg0,     buf, 4);
			UIntToBytes (arg1,     buf, 8);
			UIntToBytes ((uint)length,   buf, 12);
			UIntToBytes (checksum, buf, 16);

			UIntToBytes (command ^ 0xffffffff,    buf, 20);
		}

		static void ReadMessage (byte[] buf, out uint command, out uint arg0, out uint arg1, out int length, out uint checksum)
		{
			command = BytesToUInt (buf, 0);
			arg0 = BytesToUInt (buf, 4);
			arg1 = BytesToUInt (buf, 8);
			length = (int)BytesToUInt (buf, 12);
			checksum = BytesToUInt (buf, 16);

			if (BytesToUInt (buf, 20) != (command ^ 0xffffffff))
				throw new Exception ("Corrupt magic number");
		}

		static uint Checksum (byte[] buf, int offset, int length)
		{
			//the docs lie, it's not a CRC32, it's a byte-by-byte sum
			uint checksum = 0;
			for (int i = offset; i < length + offset; i++) {
				unchecked {
					checksum += buf [i];
				}
			}
			return checksum;
		}

		static uint BytesToUInt (byte[] buf, int offset)
		{
			return
				(uint) buf[3+offset] << 24 |
				(uint) buf[2+offset] << 16 |
				(uint) buf[1+offset] << 8 |
				(uint) buf[0+offset];
		}

		static void UIntToBytes (uint val, byte[] buf, int pos)
		{
			buf[pos+3] = (byte) (val >> 24);
			buf[pos+2] = (byte) ((val >> 16) & 0x000000FF);
			buf[pos+1] = (byte) ((val >> 08) & 0x000000FF);
			buf[pos+0] = (byte) (val & 0x000000FF);
		}

		class ServiceStream : Stream
		{
			public ManualResetEvent WriteEvent = new ManualResetEvent (false);
			public ManualResetEvent ReadEvent = new ManualResetEvent (false);

			public byte[] Incoming;

			int incomingRead;

			DeviceConnection connection;

			public uint LocalID { get; private set; }
			public uint RemoteID { get; set; }

			public ServiceStream (DeviceConnection connection, uint localID)
			{
				this.connection = connection;
				this.LocalID = localID;
			}

			public override int Read (byte[] buffer, int offset, int count)
			{
				connection.CheckError ();

				ReadEvent.WaitOne ();

				int read = System.Math.Min (count, Incoming.Length - incomingRead);
				Array.Copy (Incoming, incomingRead, buffer, offset, read);

				incomingRead += read;

				if (incomingRead == Incoming.Length) {
					ReadEvent.Reset ();
					Incoming = null;
					incomingRead = 0;
					connection.SetReady (this);
				}

				return read;
			}

			public override void Write (byte[] buffer, int offset, int count)
			{
				connection.CheckError ();

				var msg = new byte[HEADER_SIZE + connection.targetMaxPayload];

				int remaining = count;
				while (remaining > 0) {
					var write = System.Math.Min (connection.targetMaxPayload, remaining);
					uint checksum = Checksum (buffer, offset, write);
					Array.Copy (buffer, offset, msg, HEADER_SIZE, write);
					CreateMessage (msg, A_WRTE, 0, RemoteID, write, checksum);

					offset += write;
					remaining -= write;

					WriteEvent.WaitOne ();
					connection.stream.Write (msg, 0, HEADER_SIZE + write);
				}
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing) {
					connection.Close (this);
				}
				base.Dispose (disposing);
			}

			public override void Flush ()
			{
			}

			public override long Seek (long offset, SeekOrigin origin)
			{
				throw new NotSupportedException ();
			}

			public override void SetLength (long value)
			{
				throw new NotSupportedException ();
			}

			public override bool CanRead {
				get { return true; }
			}

			public override bool CanSeek {
				get { return false; }
			}

			public override bool CanWrite {
				get { return true; }
			}

			public override long Length {
				get {
					throw new NotSupportedException ();
				}
			}

			public override long Position {
				get {
					throw new NotSupportedException ();
				}
				set {
					throw new NotSupportedException ();
				}
			}
		}
	}
}
