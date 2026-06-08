// 
// AdbSyncClient.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

using Mono.AndroidTools.Internal;
using Mono.AndroidTools.Util;

// Useful references:
// http://android.git.kernel.org/?p=platform/sdk.git;a=blob;f=ddms/libs/ddmlib/src/com/android/ddmlib/SyncService.java
// http://android.git.kernel.org/?p=platform/system/core.git;a=blob;f=adb/file_sync_client.c
using System.Net;


namespace Mono.AndroidTools.Adb
{
	/// <summary>
	/// Client for the ADB sync service.
	/// </summary>
	/// <remarks>Can be accessed by multiple threads, but can only perform one operation at once.</remarks>
	public sealed class AdbSyncClient : IDisposable
	{
		#region Constants
		
		//packet IDs
		const int ID_STAT = ((int)'S') << 24 | ((int)'T') << 16 | ((int)'A') << 8 |((int)'T');
		const int ID_LIST = ((int)'L') << 24 | ((int)'I') << 16 | ((int)'S') << 8 |((int)'T');
		const int ID_ULNK = ((int)'U') << 24 | ((int)'L') << 16 | ((int)'N') << 8 |((int)'K');
		const int ID_SEND = ((int)'S') << 24 | ((int)'E') << 16 | ((int)'N') << 8 |((int)'D');
		const int ID_RECV = ((int)'R') << 24 | ((int)'E') << 16 | ((int)'C') << 8 |((int)'V');
		const int ID_DENT = ((int)'D') << 24 | ((int)'E') << 16 | ((int)'N') << 8 |((int)'T');
		const int ID_DONE = ((int)'D') << 24 | ((int)'O') << 16 | ((int)'N') << 8 |((int)'E');
		const int ID_DATA = ((int)'D') << 24 | ((int)'A') << 16 | ((int)'T') << 8 |((int)'A');
		const int ID_OKAY = ((int)'O') << 24 | ((int)'K') << 16 | ((int)'A') << 8 |((int)'Y');
		const int ID_FAIL = ((int)'F') << 24 | ((int)'A') << 16 | ((int)'I') << 8 |((int)'L');
		const int ID_QUIT = ((int)'Q') << 24 | ((int)'U') << 16 | ((int)'I') << 8 |((int)'T');
		
		//packet structures
		
		const int REQ_OFFSET_ID = 0;
		const int REQ_OFFSET_NAMELEN = 4;
		const int REQ_SIZE = 8;
		
		const int STAT_OFFSET_ID = 0;
		const int STAT_OFFSET_MODE = 4;
		const int STAT_OFFSET_SIZE = 8;
		const int STAT_OFFSET_TIME = 12;
		const int STAT_SIZE = 16;
		
		const int DENT_OFFSET_ID = 0;
		const int DENT_OFFSET_MODE = 4;
		const int DENT_OFFSET_SIZE = 8;
		const int DENT_OFFSET_TIME = 12;
		const int DENT_OFFSET_NAMELEN = 16;
		const int DENT_SIZE = 20;
		
		const int DATA_OFFSET_ID = 0;
		const int DATA_OFFSET_SIZE = 4;
		const int DATA_SIZE = 8;
		
		const int STATUS_OFFSET_ID = 0;
		const int STATUS_OFFSET_MSGLEN = 4;
		const int STATUS_SIZE = 8;
		
		#endregion
		
		const int SYNC_DATA_MAX = 64 * 1024;
		const int MAX_PATH = 1024;
		
		//re-usable buffer
		byte[] buf = new byte [SYNC_DATA_MAX + 4 + 4]; // biggest blob of data + id + length
		
		AdbClient client;
		AdbClient shellClient;
		string deviceID;
		IPAddress address;
		int port;
		bool disposed;
		
		/// <summary>
		/// Creates a sync client that connects to the local ADB server on the default port.
		/// </summary>
		public AdbSyncClient () : this (IPAddress.Loopback, AdbClient.ADB_PORT)
		{
		}

		/// <summary>
		/// Creates a sync client that connects to the specified IP endpoint.
		/// </summary>
		public AdbSyncClient (IPEndPoint endpoint) : this (endpoint.Address, endpoint.Port)
		{
		}
		
		/// <summary>
		/// Creates a sync client that connects to the specified address and port.
		/// </summary>
		public AdbSyncClient (IPAddress address, int port)
		{
			this.address = address;
			this.port = port;
			
			client = CreateClient ();
		}
		
		AdbClient CreateClient ()
		{
			return new AdbClient (address, port);
		}

		CancellationToken token;

		public void Dispose (CancellationToken token)
		{
			if (disposed)
				return;
			disposed = true;
			this.token = token;
			
			client.Dispose (token);
			
			//disposing the AdbSyncClient can interrupt inner shell commands
			var sc = shellClient;
			if (sc != null) {
				shellClient.Dispose (token);
			}
		}
		
		public void Dispose ()
		{
			Dispose (CancellationToken.None);
		}
		
		#region ConnectSyncSession
		
		public IAsyncResult BeginConnectSyncSession (string deviceID, AsyncCallback callback, object state)
		{
			this.deviceID = deviceID;
			var r = new AggregateAsyncResult (callback, state);
			client.BeginConnectTransport (deviceID, ConnectSyncSession_OnGotTransport, r);
			return r;
		}
		
		void ConnectSyncSession_OnGotTransport (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result.AsyncState;
			try {
				client.EndConnectTransport (result);
				client.BeginWriteCommandWithStatus ("sync:", ConnectSyncSession_OnWroteCommand, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void ConnectSyncSession_OnWroteCommand (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result.AsyncState;
			try {
				client.EndWriteCommandWithStatus (result);
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public void EndConnectSyncSession (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result;
			r.CheckError (token);
		}
		
		/// <summary>
		/// Connects to the ADB server and starts a sync session for the specified device.
		/// </summary>
		public void ConnectSyncSession (string deviceID)
		{
			client.ConnectTransport (deviceID);
			client.WriteCommandWithStatus ("sync:");
			this.deviceID = deviceID;
		}
		
		#endregion
		
		#region ListFiles
		
		public IAsyncResult BeginListFiles (string remoteDirectoryPath, Action<AdbFileInfo> listCallback, AsyncCallback completionCallback, object state)
		{
			var r = new LsAsyncResult (client.Stream, buf, listCallback, completionCallback, state);
			r.RootLocation = EnsureTrailingSlash (remoteDirectoryPath);
			BeginWriteReq (ID_LIST, remoteDirectoryPath, ListFiles_OnWroteReq, r);
			return r;
		}
		
		static void ListFiles_OnWroteReq (IAsyncResult result)
		{
			var r = (LsAsyncResult) result.AsyncState;
			try {
				EndWriteReq (result);
				r.BeginRead (0, DENT_SIZE, ListFiles_OnReadValue);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ListFiles_OnReadValue (IAsyncResult ar)
		{
			var r = (LsAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				
				//got the packet id, check it
				if (r.Read >= 4) {
					int id = AdbClient.FourCCToInt (r.Buffer, DENT_OFFSET_ID);
					if (id == ID_DONE) {
						r.Complete ();
						return;
					}
					if (id == ID_FAIL) {
						BeginReadFailMessage (r);
						return;
					}
					CheckPacketId (id, ID_DENT);
				}
				
				if (r.Read != r.Count) {
					r.ContinueRead (ListFiles_OnReadValue);
					return;
				}
				
				r.Mode = DataConverter.LittleEndian.GetUInt32 (r.Buffer, DENT_OFFSET_MODE);
				r.Size = DataConverter.LittleEndian.GetUInt32 (r.Buffer, DENT_OFFSET_SIZE);
				r.Time = DataConverter.LittleEndian.GetUInt32 (r.Buffer, DENT_OFFSET_TIME);
				r.NameLength = (int)DataConverter.LittleEndian.GetUInt32 (r.Buffer, DENT_OFFSET_NAMELEN);
				r.BeginRead (0, r.NameLength, ListFiles_OnReadName);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ListFiles_OnReadName (IAsyncResult ar)
		{
			var r = (LsAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Read != r.Count) {
					r.ContinueRead (ListFiles_OnReadName);
					return;
				}
				
				string name = AdbClient.TextEncoding.GetString (r.Buffer, 0, r.Count);
				//don't know why adb includes '.' and '..' in listings but it's unexpected. clean it up.
				if (name != "..") {
					string fullPath;
					if (name == ".") {
						fullPath = r.RootLocation.TrimEnd ('/');
					} else {
						fullPath = r.RootLocation + name;
					}
					r.ListCallback (new AdbFileInfo (fullPath, r.Mode, r.Size, r.Time));
				}
				
				r.BeginRead (0, DENT_SIZE, ListFiles_OnReadValue);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public void EndListFiles (IAsyncResult result)
		{
			var r = (LsAsyncResult) result;
			r.CheckError (token);
		}
		
		/// <summary>
		/// Lists the files in the specified directory on the connected device.
		/// </summary>
		/// <remarks>Includes the entry for the specified directory too.</remarks>
		public void ListFiles (string remoteDirectoryPath, Action<AdbFileInfo> listCallback)
		{
			WriteReq (ID_LIST, remoteDirectoryPath);
			remoteDirectoryPath = EnsureTrailingSlash (remoteDirectoryPath);
			while (true) {
				int read = client.Stream.Read (buf, 0, DENT_SIZE);
				while (read < 4) {
					read += client.Stream.Read (buf, read, DENT_SIZE - read);
				}
				int id = AdbClient.FourCCToInt (buf, DENT_OFFSET_ID);
				if (id == ID_DONE)
					return;
				if (id == ID_FAIL)
					ReadFailMessage (buf, read);
				CheckPacketId (id, ID_DENT);
				while (read < DENT_SIZE) {
					read += client.Stream.Read (buf, 0, DENT_SIZE - read);
				}
				
				uint mode = DataConverter.LittleEndian.GetUInt32 (buf, DENT_OFFSET_MODE);
				uint size = DataConverter.LittleEndian.GetUInt32 (buf, DENT_OFFSET_SIZE);
				uint time = DataConverter.LittleEndian.GetUInt32 (buf, DENT_OFFSET_TIME);
				int nameLength = (int)DataConverter.LittleEndian.GetUInt32 (buf, DENT_OFFSET_NAMELEN);
				
				client.Stream.ReadFull (buf, 0, nameLength);
				string name = AdbClient.TextEncoding.GetString (buf, 0, nameLength);
				//NOTE: adb includes '.' and '..' in listings but we discard it since it's not something callers expect
				//FIXME: figure out how to keep this info as it might help avoid stats in some circumstances
				if (name != "..") {
					string fullPath;
					if (name == ".") {
						fullPath = remoteDirectoryPath.TrimEnd ('/');
					} else {
						fullPath = remoteDirectoryPath + name;
					}
					listCallback (new AdbFileInfo (fullPath, mode, size, time));
				}
			}
		}
		
		class LsAsyncResult : ReadFullAsyncResult
		{
			public LsAsyncResult (NetworkStream stream, byte[] buffer, Action<AdbFileInfo> listCallback,
				AsyncCallback completionCallback, object state)
				: base (stream, buffer, completionCallback, state)
			{
				this.ListCallback = listCallback;
			}
			
			public Action<AdbFileInfo> ListCallback;
			public uint Mode, Size, Time;
			public int NameLength;
			public string RootLocation;
		}
		
		#endregion
		
		#region ListFilesRecursive
		
		public IAsyncResult BeginListFilesRecursive (string remoteDirectoryPath, Action<AdbFileInfo> listCallback, AsyncCallback callback, object state)
		{
			remoteDirectoryPath = remoteDirectoryPath.TrimEnd ('/');
			
			var queue = new Queue<string> ();
			
			//ListFiles includes the entry for the requested directory, filter it out to prevent duplicates and infinite loop
			//however, for the toplevel we want to emit that entry for consistency, but still prevent the infinite loop
			Action<AdbFileInfo> firstCallback = f => {
				if (f.Mode.HasFlag (AdbFileMode.S_IFDIR) && f.FullPath != remoteDirectoryPath)
					queue.Enqueue (f.FullPath);
				listCallback (f);
			};
			
			var ar = new AggregateAsyncResult<Queue<string>,Action<AdbFileInfo>> (queue, listCallback, callback, state);
			BeginListFiles (remoteDirectoryPath, firstCallback, ListFilesRecursive_OnListed, ar);
			return ar;
		}
		
		void ListFilesRecursive_OnListed (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<Queue<string>,Action<AdbFileInfo>>) result.AsyncState;
			try {
				EndListFiles (result);
				if (r.Arg1.Count == 0) {
					r.Complete ();
				} else {
					var nextDir = r.Arg1.Dequeue ();
					Action<AdbFileInfo> lc = (info) => {
						if (info.IsFileType (AdbFileMode.S_IFDIR)) {
							if (info.FullPath == nextDir)
								return;
							r.Arg1.Enqueue (info.FullPath);
						}
						r.Arg2 (info);
					};
					BeginListFiles (nextDir, lc, ListFilesRecursive_OnListed, r);
				}
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public void EndListFilesRecursive (IAsyncResult result)
		{
			var ar = (AggregateAsyncResult) result;
			ar.CheckError (token);
		}
		
		/// <summary>
		/// Recursively lists the files in the specified directory on the connected device.
		/// </summary>
		public void ListFilesRecursive (string remoteDirectoryPath, Action<AdbFileInfo> listCallback)
		{
			remoteDirectoryPath = remoteDirectoryPath.TrimEnd ('/');
			
			var toList = new Queue<string> ();
			
			ListFiles (remoteDirectoryPath, f => {
				if (f.Mode.HasFlag (AdbFileMode.S_IFDIR) && f.FullPath != remoteDirectoryPath)
					toList.Enqueue (f.FullPath);
				listCallback (f);
			});
			
			while (toList.Count > 0) {
				var p = toList.Dequeue ();
				ListFiles (p, f => {
					if (f.Mode.HasFlag (AdbFileMode.S_IFDIR)) {
						if (f.FullPath == p)
							return;
						toList.Enqueue (f.FullPath);
					}
					listCallback (f);
				});
			}
		}
		
		#endregion
		
		#region Stat
		
		public IAsyncResult BeginStat (string remoteFilePath, AsyncCallback callback, object state)
		{
			var r = new LsAsyncResult (client.Stream, buf, null, callback, state);
			r.RootLocation = remoteFilePath;
			BeginWriteReq (ID_STAT, remoteFilePath, Stat_OnWroteReq, r);
			return r;
		}
		
		static void Stat_OnWroteReq (IAsyncResult ar)
		{
			var r = (LsAsyncResult) ar.AsyncState;
			try {
				EndWriteReq (ar);
				r.BeginRead (0, STAT_SIZE, Stat_OnRead);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void Stat_OnRead (IAsyncResult ar)
		{
			var r = (LsAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				
				if (r.Read >= 4) {
					int id = AdbClient.FourCCToInt (r.Buffer, STAT_OFFSET_ID);
					if (id == ID_FAIL) {
						BeginReadFailMessage (r);
						return;
					}
					CheckPacketId (id, ID_STAT);
				}
				
				if (r.Read != r.Count) {
					r.ContinueRead (ListFiles_OnReadName);
					return;
				}
					
				r.Mode = DataConverter.LittleEndian.GetUInt32 (r.Buffer, STAT_OFFSET_MODE);
				r.Size = DataConverter.LittleEndian.GetUInt32 (r.Buffer, STAT_OFFSET_SIZE);
				r.Time = DataConverter.LittleEndian.GetUInt32 (r.Buffer, STAT_OFFSET_TIME);
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public AdbFileInfo EndStat (IAsyncResult result)
		{
			var r = (LsAsyncResult) result;
			r.CheckError (token);
			return new AdbFileInfo (r.RootLocation, r.Mode, r.Size, r.Time);
		}
		
		/// <summary>
		/// Gets information about the specified file on the connected device.
		/// </summary>
		public AdbFileInfo Stat (string remoteFilePath)
		{
			WriteReq (ID_STAT, remoteFilePath);
			int read = client.Stream.Read (buf, 0, STAT_SIZE);
			while (read < 4) {
				read += client.Stream.Read (buf, read, STAT_SIZE - read);
			}
			
			int id = AdbClient.FourCCToInt (buf, STAT_OFFSET_ID);
			if (id == ID_FAIL)
				ReadFailMessage (buf, read);
			CheckPacketId (id, ID_STAT);
			
			while (read < STAT_SIZE) {
				read += client.Stream.Read (buf, read, STAT_SIZE - read);
			}
			
			return new AdbFileInfo (
				remoteFilePath,
				DataConverter.LittleEndian.GetUInt32 (buf, STAT_OFFSET_MODE),
				DataConverter.LittleEndian.GetUInt32 (buf, STAT_OFFSET_SIZE),
				DataConverter.LittleEndian.GetUInt32 (buf, STAT_OFFSET_TIME));
		}
		
		#endregion
		
		public delegate int ReadCallback (byte[] buffer, int offset, int length);
		
		#region WriteFile
		
		public IAsyncResult BeginWriteFile (string remotePath, AdbFileMode mode, uint? filetime, ReadCallback getChunk,
			AsyncCallback callback, object state)
		{
			return BeginWriteFile (remotePath, mode, filetime, getChunk, callback, state);
		}
		
		
		public IAsyncResult BeginWriteFile (string remotePath, AdbFileMode mode, uint? filetime, ReadCallback getChunk, Action<long> progressTotalBytes,
			AsyncCallback callback, object state)
		{
			if (mode != 0 && (mode & AdbFileMode.S_IFMT) != AdbFileMode.S_IFREG)
				throw new AdbException ("Can only write regular files");
			else if (mode == 0) // Starting in N preview, having a file mode parameter is necessary
				mode = AdbFileMode.DEFFILEMODE;
			
			remotePath += "," + ((uint)mode).ToString ();

			var ar = new WriteFileAsyncResult (client.Stream, buf, remotePath, mode, filetime, getChunk,
				progressTotalBytes, callback, state);
			BeginWriteReq (ID_SEND, remotePath, FileWrite_OnWroteReq, ar);
			return ar;
		}
		
		static void FileWrite_OnWroteReq (IAsyncResult result)
		{
			var r = (WriteFileAsyncResult) result.AsyncState;
			try {
				EndWriteReq (result);
				AdbClient.IntToFourCC (ID_DATA, r.Buffer, DATA_OFFSET_ID);
				FileWrite_WriteChunk (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void FileWrite_WriteChunk (WriteFileAsyncResult r)
		{
			if (((NetworkStream)r.Stream).DataAvailable) {
				r.BeginRead (0, STATUS_SIZE, FileWrite_OnReadStatus);
				return;
			}
			r.ChunkSize = r.GetChunk (r.Buffer, DATA_SIZE, r.Buffer.Length - DATA_SIZE);
			if (r.ChunkSize > 0) {
				DataConverter.LittleEndian.PutBytes (r.Buffer, DATA_OFFSET_SIZE, r.ChunkSize);
				r.Stream.BeginWrite (r.Buffer, 0, DATA_SIZE + r.ChunkSize, FileWrite_OnWroteChunk, r);
				return;
			}
			AdbClient.IntToFourCC (ID_DONE, r.Buffer, DATA_OFFSET_ID);
			if (!r.Filetime.HasValue)
				r.Filetime = AdbFileInfo.UnixFileTimeFromDateTime (DateTime.Now);
			DataConverter.LittleEndian.PutBytes (r.Buffer, DATA_OFFSET_SIZE, r.Filetime.Value);
			r.Stream.BeginWrite (r.Buffer, 0, DATA_SIZE, FileWrite_OnWroteDone, r);
		}
		
		static void FileWrite_OnWroteChunk (IAsyncResult result)
		{
			var r = (WriteFileAsyncResult) result.AsyncState;
			try {
				r.Stream.EndWrite (result);
				r.TotalWritten += (long) r.ChunkSize;
				//begin writing the next chunk before dispatching the progress event
				FileWrite_WriteChunk (r);
				if (r.ProgressTotalBytes != null)
					r.ProgressTotalBytes (r.TotalWritten);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void FileWrite_OnWroteDone (IAsyncResult result)
		{
			var r = (WriteFileAsyncResult) result.AsyncState;
			try {
				r.Stream.EndWrite (result);
				r.BeginRead (0, STATUS_SIZE, FileWrite_OnReadStatus);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void FileWrite_OnReadStatus (IAsyncResult ar)
		{
			var r = (WriteFileAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Read != r.Count) {
					r.ContinueRead (FileWrite_OnReadStatus);
					return;
				}
				int id = AdbClient.FourCCToInt (r.Buffer, STATUS_OFFSET_ID);
				if (id == ID_OKAY) {
					r.Complete ();
				} else if (id == ID_FAIL) {
					BeginReadFailMessage (r);
				} else {
					throw new AdbException ("Unknown error");
				}
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public long EndWriteFile (IAsyncResult result)
		{
			var r = (WriteFileAsyncResult) result;
			r.CheckError (token);
			return r.TotalWritten;
		}
		
		class WriteFileAsyncResult : ReadFullAsyncResult
		{
			public WriteFileAsyncResult (NetworkStream stream, byte[] buffer, string remotePath, AdbFileMode mode,
				uint? filetime, ReadCallback getChunk, Action<long> progressTotalBytes,
				AsyncCallback callback, object state)
				: base (stream, buffer, callback, state)
			{
				this.RemotePath = remotePath;
				this.Mode = mode;
				this.Filetime = filetime;
				this.GetChunk = getChunk;
				this.ProgressTotalBytes = progressTotalBytes;
			}
			
			public string RemotePath;
			public AdbFileMode Mode;
			public uint? Filetime;
			public ReadCallback GetChunk;
			public int ChunkSize;
			public long TotalWritten;
			public Action<long> ProgressTotalBytes;
		}
		
		/// <summary>
		/// Writes a file to the remote device.
		/// </summary>
		/// <remarks>Cannot create anything other than regular files.</remarks>
		public long WriteFile (string remotePath, AdbFileMode mode, uint? filetime, ReadCallback getChunk, Action<long> progressTotalBytes=null)
		{
			if (mode != 0) {
				//sync service ignores file type bits and always creates regular files.
				if ((mode & AdbFileMode.S_IFMT) != AdbFileMode.S_IFREG)
					throw new AdbException ("Can only write regular files");
				remotePath += "," + ((uint)mode).ToString ();
			}
			WriteReq (ID_SEND, remotePath);
			
			long totalBytes = 0;
			bool error = client.Stream.DataAvailable;
			if (!error) {
				AdbClient.IntToFourCC (ID_DATA, buf, DATA_OFFSET_ID);
				
				int chunkSize;
				while ((chunkSize = getChunk (buf, DATA_SIZE, buf.Length - DATA_SIZE)) > 0) {
					if (client.Stream.DataAvailable) {
						error = true;
						break;
					}
					DataConverter.LittleEndian.PutBytes (buf, DATA_OFFSET_SIZE, chunkSize);
					client.Stream.Write (buf, 0, chunkSize + DATA_SIZE);
					totalBytes += chunkSize;
					if (progressTotalBytes != null)
						progressTotalBytes (totalBytes);
				}
				if (!error) {
					AdbClient.IntToFourCC (ID_DONE, buf, DATA_OFFSET_ID);
					if (!filetime.HasValue)
						filetime = AdbFileInfo.UnixFileTimeFromDateTime (DateTime.Now);
					DataConverter.LittleEndian.PutBytes (buf, DATA_OFFSET_SIZE, filetime.Value);
					client.Stream.Write (buf, 0, DATA_SIZE);
				}
			}
			
			client.Stream.ReadFull (buf, 0, STATUS_SIZE);
			int id = AdbClient.FourCCToInt (buf, STATUS_OFFSET_ID);
			if (error || id != ID_OKAY) {
				if (id == ID_FAIL)
					ReadFailMessage (buf, STAT_SIZE);
				throw new AdbException ("Unknown error");
			}
			return totalBytes;
		}
		
		#endregion
		
		#region Push
		
		public IAsyncResult BeginPush (string localFilePath, string remoteFilePath, AsyncCallback callback, object state)
		{
			return BeginPush (localFilePath, remoteFilePath, null, callback, state);
		}
		
		public IAsyncResult BeginPush (string localFilePath, string remoteFilePath, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			var localStream = File.OpenRead (localFilePath);
			
			Action<long> progressTotalBytes = null;
			if (notifyProgress != null) {
				long length = localStream.Length;
				progressTotalBytes = (b) => notifyProgress (b, length);
			}
			
			var r = new AggregateAsyncResult<IDisposable,long> (localStream, 0, callback, state);
			BeginWriteFile (remoteFilePath, 0, null, localStream.Read, progressTotalBytes, Push_OnWroteFile, r);
			return r;
		}

		public IAsyncResult BeginPush (Stream contents, string remoteFilePath, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			Action<long> progressTotalBytes = null;
			if (notifyProgress != null) {
				long length = contents.Length;
				progressTotalBytes = (b) => notifyProgress (b, length);
			}

			var r = new AggregateAsyncResult<IDisposable,long> (contents, 0, callback, state);
			BeginWriteFile (remoteFilePath, 0, null, contents.Read, progressTotalBytes, Push_OnWroteFile, r);
			return r;
		}

		public IAsyncResult BeginPushLeaveOpen (Stream contents, string remoteFilePath, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			Action<long> progressTotalBytes = null;
			if (notifyProgress != null) {
				long length = contents.Length;
				progressTotalBytes = (b) => notifyProgress (b, length);
			}

			var r = new AggregateAsyncResult<long> (0, callback, state);
			BeginWriteFile (remoteFilePath, 0, null, contents.Read, progressTotalBytes, Push_OnWroteFileLeaveOpen, r);
			return r;
		}
		
		void Push_OnWroteFile (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<IDisposable,long>) result.AsyncState;
			try {
				r.Arg2 = EndWriteFile (result);
				r.Arg1.Dispose ();
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}

		void Push_OnWroteFileLeaveOpen (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<long>) result.AsyncState;
			try {
				r.Arg = EndWriteFile (result);
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public long EndPush (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<IDisposable,long>) result;
			r.CheckError (token);
			return r.Arg2;
		}

		public long EndPushLeaveOpen (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<long>) result;
			r.CheckError (token);
			return r.Arg;
		}
		
		/// <summary>
		/// Copies the specified local file to the specified remote path on the connected device.
		/// </summary>
		public long Push (string localFilePath, string remoteFilePath, AdbProgressReporter notifyProgress=null)
		{
			using (var localStream = File.OpenRead (localFilePath)) {
				Action<long> progressTotalBytes = null;
				if (notifyProgress != null) {
					long length = localStream.Length;
					progressTotalBytes = (b) => notifyProgress (b, length);
				}
				return WriteFile (remoteFilePath, 0, null, localStream.Read, progressTotalBytes);
			}
		}
		
		#endregion
		
		#region PushDirectory

		[Obsolete ("Use another overload with PushOptions argument.")]
		public IAsyncResult BeginPushDirectory (string localDirectoryPath, string remoteDirectoryPath,
			bool checkTimestamps, bool removeUnknown, bool dryRun,
			Action<AdbSyncNotification> notifySync, Action<string> notifyPhase, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			return BeginPushDirectory (new PushOptions () {
				LocalDirectoryPath = localDirectoryPath,
				RemoteDirectoryPath = remoteDirectoryPath,
				CheckTimestamps = checkTimestamps,
				RemoveUnknown = removeUnknown,
				DryRun = dryRun,
				RemoveBeforeCopy = false,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress
				}, callback, state);
		}
		
		public class PushOptions
		{
			public string LocalDirectoryPath { get; set; }
			public string RemoteDirectoryPath { get; set; }
			public bool CheckTimestamps { get; set; }
			public bool RemoveUnknown { get; set; }
			public bool DryRun { get; set; }
			public bool RemoveBeforeCopy { get; set; }
			public Action<AdbSyncNotification> NotifySync { get; set; }
			public Action<string> NotifyPhase { get; set; }
			public AdbProgressReporter NotifyProgress { get; set; }
		}
		
		public IAsyncResult BeginPushDirectory (PushOptions options,
			AsyncCallback callback, object state)
		{
			AdbSyncDirectory dir;
			string remoteParent;
			PushDirectory_GetArgs (options, out dir, out remoteParent);
			return BeginPushSyncItems (dir, remoteParent, options, callback, state);
		}
		
		public long EndPushDirectory (IAsyncResult result)
		{
			return EndPushSyncItems (result);
		}
		
		/// <summary>
		/// Recursively copies a local directory to a directory on the remote device
		/// </summary>
		/// <param name='localDirectoryPath'>The local directory path.</param>
		/// <param name='remoteDirectoryPath'>The temote directory path.</param>
		/// <param name='checkTimestamps'>Skip files if the remote files exists and is newer.</param>
		/// <param name='removeUnknown'>Delete unknown files in the remote target directory.</param>
		/// <param name='dryRun'>Do not perform any modifications, but report those that would be performed.</param>
		/// <param name='notifySync'>Reports each copy/create/delete operation.</param>
		/// <param name='notifyPhase'>Reports the phase of the directory push.</param>
		/// <param name='notifyProgress'>Reports overall progress.</param>
		[Obsolete ("Use another overload with 'removeBeforeCopy' parameter.")]
		public long PushDirectory (string localDirectoryPath, string remoteDirectoryPath, bool checkTimestamps=true,
			bool removeUnknown=false,
			bool dryRun=false,
			Action<AdbSyncNotification> notifySync=null,
			Action<string> notifyPhase=null,
			AdbProgressReporter notifyProgress=null)
		{
			return PushDirectory (new PushOptions () {
				LocalDirectoryPath = localDirectoryPath,
				RemoteDirectoryPath = remoteDirectoryPath,
				CheckTimestamps = checkTimestamps,
				RemoveUnknown = removeUnknown,
				DryRun = dryRun,
				RemoveBeforeCopy = false,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress});
		}
		
		/// <summary>
		/// Recursively copies a local directory to a directory on the remote device
		/// </summary>
		/// <param name='localDirectoryPath'>The local directory path.</param>
		/// <param name='remoteDirectoryPath'>The temote directory path.</param>
		/// <param name='checkTimestamps'>Skip files if the remote files exists and is newer.</param>
		/// <param name='removeUnknown'>Delete unknown files in the remote target directory.</param>
		/// <param name='dryRun'>Do not perform any modifications, but report those that would be performed.</param>
		/// <param name='removeBeforeCopy'>When overwriting files, remove them first.</param>
		/// <param name='notifySync'>Reports each copy/create/delete operation.</param>
		/// <param name='notifyPhase'>Reports the phase of the directory push.</param>
		/// <param name='notifyProgress'>Reports overall progress.</param>
		public long PushDirectory (PushOptions options)
		{
			AdbSyncDirectory dir;
			string remoteParent;
			PushDirectory_GetArgs (options,
				out dir, out remoteParent);
			return PushSyncItems (dir, remoteParent, options);
		}
		
		void PushDirectory_GetArgs (PushOptions options,
			out AdbSyncDirectory dir, out string remoteParent)
		{
			if (options.NotifyPhase != null)
				options.NotifyPhase ("Enumerating local files");
			
			var action = options.CheckTimestamps? AdbSyncAction.CopyIfNewer : AdbSyncAction.Copy;
			int lastSlash = options.RemoteDirectoryPath.LastIndexOf ('/');
			var remoteName = options.RemoteDirectoryPath.Substring (lastSlash);
			remoteParent = options.RemoteDirectoryPath.Substring (0, lastSlash);
			dir = new AdbSyncDirectory (remoteName, action, options.RemoveUnknown,
				AdbSyncItem.FromLocalDirectoryContents (options.LocalDirectoryPath, action, options.RemoveUnknown));
		}
		
		#endregion
		
		#region PushSyncItems
		
		[Obsolete ("Use another overload with 'removeBeforeCopy' parameter.")]
		public IAsyncResult BeginPushSyncItems (AdbSyncDirectory targetDir, string remoteParentDir, bool dryRun,
			Action<AdbSyncNotification> notifySync, Action<string> notifyPhase, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			return BeginPushSyncItems (targetDir, remoteParentDir, new PushOptions () {
				DryRun = dryRun,
				RemoveBeforeCopy = false,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress
				}, callback, state);
		}
		
		public IAsyncResult BeginPushSyncItems (AdbSyncDirectory targetDir, string remoteParentDir, PushOptions options,
			AsyncCallback callback, object state)
		{
			var ar = new PushSyncItemsAsyncResult (new AdbSyncTargetContext (targetDir, remoteParentDir), options.DryRun, options.RemoveBeforeCopy,
				options.NotifySync, options.NotifyPhase, options.NotifyProgress, callback, state);
			
			if (ar.NotifyPhase != null)
				ar.NotifyPhase ("Enumerating remote files");
			
			ar.RemoteItems = new List<AdbFileInfo> ();
			var remoteDir = ar.Context.RemoteParentDir + targetDir.Name;
			BeginStat (remoteDir, PushSyncItems_OnStat, ar);
			return ar;
		}
		
		void PushSyncItems_OnStat (IAsyncResult result)
		{
			var r = (PushSyncItemsAsyncResult) result.AsyncState;
			try {
				var rootStat = EndStat (result);
				if (rootStat != null && rootStat.IsFileType (AdbFileMode.S_IFDIR)) {
					r.RemoteItems.Add (rootStat);
					BeginListFilesRecursive (rootStat.FullPath, r.RemoteItems.Add, PushSyncItems_OnGotList, r);
				} else {
					PushSyncItems_BeginOperations (r);
				}
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void PushSyncItems_OnGotList (IAsyncResult result)
		{
			var r = (PushSyncItemsAsyncResult) result.AsyncState;
			try {
				EndListFilesRecursive (result);
				PushSyncItems_BeginOperations (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void PushSyncItems_BeginOperations (PushSyncItemsAsyncResult r)
		{
			if (r.NotifyPhase != null)
				r.NotifyPhase ("Determining required operations");
			r.Context.ComputeRequiredOperations (r.RemoteItems, r.RemoveBeforeCopy);
			
			if (r.Context.Operations.Any (op => op.IsRemove)) {
				if (r.NotifyPhase != null)
					r.NotifyPhase ("Removing unnecessary files and directories");
				BeginRemoveItems (r.Context.Operations.Where (op => op.IsRemove), r.DryRun, r.NotifySync, PushSyncItems_OnRemovedFiles, r);
			} else {
				PushSyncItems_CreateDirectories (r);
			}
		}
		
		void PushSyncItems_OnRemovedFiles (IAsyncResult result)
		{
			var r = (PushSyncItemsAsyncResult) result.AsyncState;
			try {
				EndRemoveItems (result);
				PushSyncItems_CreateDirectories (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void PushSyncItems_CreateDirectories (PushSyncItemsAsyncResult r)
		{
			if (r.NotifyPhase != null)
				r.NotifyPhase ("Creating directories");
			BeginCreateDirectories (r.Context.Operations.Where (op => op.IsCreateDirectory),
				r.DryRun, r.NotifySync, PushSyncItems_OnCreatedDirectories, r);
		}
		
		void PushSyncItems_OnCreatedDirectories (IAsyncResult result)
		{
			var r = (PushSyncItemsAsyncResult) result.AsyncState;
			try {
				EndCreateDirectories (result);
				PushSyncItems_StartCopyFiles (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void PushSyncItems_StartCopyFiles (PushSyncItemsAsyncResult r)
		{
			foreach (var item in r.Context.Operations.Where (op => op.IsCopyFile)) {
				r.TotalSize += item.Size;
			}
			
			//actually copy the files
			if (r.NotifyPhase != null)
				r.NotifyPhase ("Uploading files");
			
			r.ItemIndex = 0;
			PushSyncItems_PushNextFile (r);
		}
		
		void PushSyncItems_PushNextFile (PushSyncItemsAsyncResult r)
		{
			const AdbSyncKind copyOrPreserve = AdbSyncKind.CopyFile | AdbSyncKind.SkipCopyFile
				| AdbSyncKind.PreserveFile | AdbSyncKind.PreserveDirectory;
			
			AdbSyncNotification item;
			while (r.ItemIndex < r.Context.Operations.Count) {
				item = r.Context.Operations[r.ItemIndex];
				if ((item.Kind & copyOrPreserve) != 0) {
					if (!r.DryRun && item.Kind == AdbSyncKind.CopyFile) {
						AdbProgressReporter notifyFileProgress = null;
						if (r.NotifyProgress != null) {
							notifyFileProgress = (fileCopied, fileTotal) => 
								r.NotifyProgress (r.FilesCompletedSize + fileCopied, r.TotalSize);
						}
						BeginPush (item.LocalPath, item.RemotePath, notifyFileProgress, PushSyncItems_OnPushedFile, r);
						return;
					}
					if (r.NotifySync != null)
						r.NotifySync (item);
				}
				r.ItemIndex++;
			}
			
			if (r.NotifyPhase != null)
				r.NotifyPhase ("Upload completed");
			r.Complete ();
		}
		
		void PushSyncItems_OnPushedFile (IAsyncResult result)
		{
			var r = (PushSyncItemsAsyncResult) result.AsyncState;
			try {
				r.FilesCompletedSize += EndPush (result);
				if (r.NotifySync != null)
					r.NotifySync (r.Context.Operations[r.ItemIndex]);
				r.ItemIndex++;
				PushSyncItems_PushNextFile (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public long EndPushSyncItems (IAsyncResult result)
		{
			var r = (PushSyncItemsAsyncResult) result;
			r.CheckError (token);
			return r.FilesCompletedSize;
		}
		
		class PushSyncItemsAsyncResult : AggregateAsyncResult
		{
			public PushSyncItemsAsyncResult (AdbSyncTargetContext ctx, bool dryRun, bool removeBeforeCopy,
				Action<AdbSyncNotification> notifySync,
				Action<string> notifyPhase, AdbProgressReporter notifyProgress,
				AsyncCallback callback, object state)
				: base (callback, state)
			{
				this.Context = ctx;
				this.DryRun = dryRun;
				this.RemoveBeforeCopy = removeBeforeCopy;
				this.NotifySync = notifySync;
				this.NotifyPhase = notifyPhase;
				this.NotifyProgress = notifyProgress;
			}
			
			public bool DryRun;
			public Action<AdbSyncNotification> NotifySync;
			public Action<string> NotifyPhase;
			public AdbProgressReporter NotifyProgress;
			public AdbSyncTargetContext Context;
			public List<AdbFileInfo> RemoteItems;
			public int ItemIndex;
			public long FilesCompletedSize;
			public long TotalSize;
			public bool RemoveBeforeCopy;
		}
		
		[Obsolete ("Use another overload with PushOptions argument.")]
		public long PushSyncItems (AdbSyncDirectory targetDir, string remoteParentDir,
			bool dryRun=false,
			Action<AdbSyncNotification> notifySync=null,
			Action<string> notifyPhase=null,
			AdbProgressReporter notifyProgress=null)
		{
			return PushSyncItems (targetDir, remoteParentDir, new PushOptions () {
				DryRun = dryRun,
				RemoveBeforeCopy = false,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress});
		}
		
		public long PushSyncItems (AdbSyncDirectory targetDir, string remoteParentDir, PushOptions options)
		{
			var ctx = new AdbSyncTargetContext (targetDir, remoteParentDir);
			var remoteDirectoryPath = ctx.RemoteParentDir + ctx.TargetDir.Name;
			
			if (options.NotifyPhase != null)
				options.NotifyPhase ("Enumerating remote files");
			
			var remoteItems = new List<AdbFileInfo> ();
			var rootStat = Stat (remoteDirectoryPath);
			if (rootStat != null && rootStat.IsFileType (AdbFileMode.S_IFDIR)) {
				ListFilesRecursive (remoteDirectoryPath, remoteItems.Add);
			}
			
			if (options.NotifyPhase != null)
				options.NotifyPhase ("Determining required operations");
			ctx.ComputeRequiredOperations (remoteItems, options.RemoveBeforeCopy);
			
			if (ctx.Operations.Any (op => op.IsRemove)) {
				if (options.NotifyPhase != null)
					options.NotifyPhase ("Removing files and directories");
				RemoveItems (ctx.Operations.Where (op => op.IsRemove), options.DryRun, options.NotifySync);
			}
			
			if (options.NotifyPhase != null)
				options.NotifyPhase ("Creating directories");
			CreateDirectories (ctx.Operations.Where (op => op.IsCreateDirectory), options.DryRun, options.NotifySync);
			
			long totalBytes = 0, completedFilesBytes = 0;
			int filesToCopyCount = 0;
			foreach (var item in ctx.Operations.Where (op => op.Kind == AdbSyncKind.CopyFile)) {
				totalBytes += item.Size;
				filesToCopyCount++;
			}
			
			//actually copy the files
			if (options.NotifyPhase != null && filesToCopyCount > 0)
				options.NotifyPhase ("Uploading files");
			
			foreach (var item in ctx.Operations) {
				if (item.IsCreateDirectory || item.IsRemove)
					continue;
				if (!options.DryRun && item.Kind == AdbSyncKind.CopyFile) {
					AdbProgressReporter notifyFileProgress = null;
					if (options.NotifyProgress != null) {
						notifyFileProgress = (fileCopied, fileTotal) => 
							options.NotifyProgress (completedFilesBytes + fileCopied, totalBytes);
					}
					Push (item.LocalPath, item.RemotePath, notifyFileProgress);
					if (options.NotifyProgress != null) {
						completedFilesBytes += item.Size;
					}
				}
				if (options.NotifySync != null)
					options.NotifySync (item);
			}
			
			if (options.NotifyPhase != null)
				options.NotifyPhase ("Directory push completed");
			
			return totalBytes;
		}
		
		#endregion
		
		#region ReadFile
		
		public delegate void WriteCallback (byte[] buffer, int offset, int length);
		
		public IAsyncResult BeginReadFile (string remotePath, WriteCallback gotChunk, AsyncCallback callback, object state)
		{
			return BeginReadFile (remotePath, gotChunk, null, callback, state);
		}
		
		public IAsyncResult BeginReadFile (string remotePath, WriteCallback gotChunk, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			if (remotePath == null)
				throw new ArgumentNullException ("remotePath");
			if (gotChunk == null)
				throw new ArgumentNullException ("gotChunk");
			
			var ar = new ReadFileAsyncResult (this.client.Stream, buf, remotePath,
				gotChunk, notifyProgress, callback, state);
			BeginStat (remotePath, ReadFile_GotStat, ar);
			return ar;
		}
		
		void ReadFile_GotStat (IAsyncResult result)
		{
			var r = (ReadFileAsyncResult) result.AsyncState;
			try {
				AdbFileInfo info = EndStat (result);
				if (!info.IsFileType (AdbFileMode.S_IFREG)) {
					throw new AdbException ("Can only read regular files");
				}
				r.ExpectedSize = info.Size;
				BeginWriteReq (ID_RECV, r.RemotePath, ReadFile_WroteReq, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ReadFile_WroteReq (IAsyncResult result)
		{
			var r = (ReadFileAsyncResult) result.AsyncState;
			try {
				EndWriteReq (result);
				r.BeginRead (0, DATA_SIZE, ReadFile_ReadPacketHeader);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ReadFile_ReadPacketHeader (IAsyncResult ar)
		{
			var r = (ReadFileAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Read >= 4) {
					var id = AdbClient.FourCCToInt (r.Buffer, DATA_OFFSET_ID);
					if (id == ID_DONE) {
						r.Complete ();
						return;
					}
					CheckPacketId (id, ID_DATA);
				}
				if (r.Read != r.Count) {
					r.ContinueRead (ReadFile_ReadPacketHeader);
					return;
				}
				
				r.PacketSize = (int)DataConverter.LittleEndian.GetUInt32 (r.Buffer, DATA_OFFSET_SIZE);
				if (r.PacketSize > SYNC_DATA_MAX)
					throw new AdbException ("Data packet is bigger than maximum");
				r.PacketRead = 0;
				r.Stream.BeginRead (r.Buffer, 0, r.PacketSize, ReadFile_GotPacketChunk, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ReadFile_GotPacketChunk (IAsyncResult result)
		{
			var r = (ReadFileAsyncResult) result.AsyncState;
			try {
				int chunkSize = r.Stream.EndRead (result);
				r.PacketRead += chunkSize;
				if (r.PacketSize > r.PacketRead) {
					r.Stream.BeginRead (r.Buffer, r.PacketRead, r.PacketSize - r.PacketRead, ReadFile_GotPacketChunk, r);
				} else {
					r.GotChunk (r.Buffer, 0, r.PacketSize);
					r.ReadSize += r.PacketSize;
					if (r.NotifyProgress != null)
						r.NotifyProgress (r.ReadSize, r.ExpectedSize);
					r.BeginRead (0, DATA_SIZE, ReadFile_ReadPacketHeader);
				}
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public long EndReadFile (IAsyncResult result)
		{
			var ar = (ReadFileAsyncResult) result;
			ar.CheckError (token);
			return ar.ReadSize;
		}
		
		class ReadFileAsyncResult : ReadFullAsyncResult
		{
			public ReadFileAsyncResult (NetworkStream stream, byte[] buf, string remotePath,
				WriteCallback gotChunk, AdbProgressReporter notifyProgress,
				AsyncCallback callback, object state)
				: base (stream, buf, callback, state)
			{
				this.RemotePath = remotePath;
				this.GotChunk = gotChunk;
				this.NotifyProgress = notifyProgress;
			}
			
			public string RemotePath;
			public WriteCallback GotChunk;
			public AdbProgressReporter NotifyProgress;
			public long ExpectedSize, ReadSize;
			public int PacketSize, PacketRead;
		}
		
		/// <summary>
		/// Reads a file from the remote device.
		/// </summary>
		/// <remarks>Con only read regular files.</remarks>
		public long ReadFile (string remotePath, WriteCallback gotChunk, AdbProgressReporter notifyProgress)
		{
			if (remotePath == null)
				throw new ArgumentNullException ("remotePath");
			if (gotChunk == null)
				throw new ArgumentNullException ("gotChunk");
			
			var info = Stat (remotePath);
			if (!info.IsFileType (AdbFileMode.S_IFREG)) {
				throw new AdbException ("Can only read regular files");
			}
			long size = info.Size, totalRead = 0;
			
			WriteReq (ID_RECV, remotePath);
			
			do {
				client.Stream.ReadFull (buf, 0, DATA_SIZE);
				var id = AdbClient.FourCCToInt (buf, DATA_OFFSET_ID);
				if (id == ID_DONE)
					break;
				CheckPacketId (id, ID_DATA);
				var dataSize = (int)DataConverter.LittleEndian.GetUInt32 (buf, DATA_OFFSET_SIZE);
				if (dataSize > SYNC_DATA_MAX)
					throw new AdbException ("Data packet is bigger than maximum");
				
				//the packet may read in smaller chunks than expected
				int dataRead = 0;
				do {
					dataRead += client.Stream.Read (buf, dataRead, dataSize - dataRead);
				} while (dataRead < dataSize);
				
				gotChunk (buf, 0, dataSize);
				
				totalRead += dataRead;
				if (notifyProgress != null)
					notifyProgress (totalRead, size);
			} while (true);
			
			return totalRead;
		}
		
		#endregion
		
		#region ReadFileToMemory
		
		int GetCappedMemorySize (int requestedCap)
		{
			const int MAX_MEMORY_BUFFER = 1024 * 1024 * 128; //128 MB
			if (requestedCap <= 0 || requestedCap > MAX_MEMORY_BUFFER)
				return MAX_MEMORY_BUFFER;
			return requestedCap;
		}
		
		static int CheckRemoteSizeFitsCap (long remoteSize, int cap)
		{
			if (remoteSize > cap)
				throw new AdbException ("Remote file is too large to be read into memory");
			return (int) cap;
		}
		
		public IAsyncResult BeginReadFileToMemory (string remotePath, AsyncCallback callback, object state)
		{
			return BeginReadFileToMemory (remotePath, 0, callback, state);
		}
		
		public IAsyncResult BeginReadFileToMemory (string remotePath, int memoryStreamLengthCap,
			AsyncCallback callback, object state)
		{
			return BeginReadFileToMemory (remotePath, memoryStreamLengthCap, null, callback, state);
		}
		
		public IAsyncResult BeginReadFileToMemory (string remotePath, int memoryStreamLengthCap, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			if (remotePath == null)
				throw new ArgumentNullException ("remotePath");
			
			memoryStreamLengthCap = GetCappedMemorySize (memoryStreamLengthCap);
			var ar = new ReadFileToMemoryAsyncResult (client.Stream, buf, remotePath,
				memoryStreamLengthCap, notifyProgress, callback, state);
			BeginStat (remotePath, ReadFileToMemory_GotStat, ar);
			return ar;
		}
		
		void ReadFileToMemory_GotStat (IAsyncResult result)
		{
			var r = (ReadFileToMemoryAsyncResult) result.AsyncState;
			try {
				AdbFileInfo info = EndStat (result);
				if (!info.IsFileType (AdbFileMode.S_IFREG)) {
					throw new AdbException ("Can only read regular files");
				}
				r.ExpectedSize = info.Size;
				
				int msSize = CheckRemoteSizeFitsCap (r.ExpectedSize, r.MemoryStreamLengthCap);
				r.MemoryStream = new MemoryStream (msSize);
				BeginWriteReq (ID_RECV, r.RemotePath, ReadFileToMemory_WroteReq, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ReadFileToMemory_WroteReq (IAsyncResult result)
		{
			var r = (ReadFileToMemoryAsyncResult) result.AsyncState;
			try {
				EndWriteReq (result);
				r.BeginRead (0, DATA_SIZE, ReadFileToMemory_ReadPacketHeader);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ReadFileToMemory_ReadPacketHeader (IAsyncResult ar)
		{
			var r = (ReadFileToMemoryAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Read >= 4) {
					var id = AdbClient.FourCCToInt (r.Buffer, DATA_OFFSET_ID);
					if (id == ID_DONE) {
						r.MemoryStream.Position = 0;
						r.Complete ();
						return;
					} else if (id == ID_FAIL) {
						BeginReadFailMessage (r);
						return;
					}
					CheckPacketId (id, ID_DATA);
				}
				if (r.Read != r.Count) {
					System.Diagnostics.Debugger.Break ();
					r.ContinueRead (ReadFile_ReadPacketHeader);
					return;
				}
				
				r.PacketSize = (int)DataConverter.LittleEndian.GetUInt32 (r.Buffer, DATA_OFFSET_SIZE);
				if (r.PacketSize > SYNC_DATA_MAX)
					throw new AdbException ("Data packet is bigger than maximum");
				if (r.MemoryStreamLengthCap != 0)
					CheckRemoteSizeFitsCap (r.PacketRead + r.ReadSize, r.MemoryStreamLengthCap);
				r.PacketRead = 0;
				r.Stream.BeginRead (r.Buffer, 0, r.PacketSize, ReadFileToMemory_GotPacketChunk, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void ReadFileToMemory_GotPacketChunk (IAsyncResult result)
		{
			var r = (ReadFileToMemoryAsyncResult) result.AsyncState;
			try {
				int chunkSize = r.Stream.EndRead (result);
				r.PacketRead += chunkSize;
				if (r.PacketSize > r.PacketRead) {
					r.Stream.BeginRead (r.Buffer, r.PacketRead, r.PacketSize - r.PacketRead, ReadFileToMemory_GotPacketChunk, r);
				} else {
					r.MemoryStream.Write (r.Buffer, 0, r.PacketSize);
					r.ReadSize += r.PacketSize;
					if (r.NotifyProgress != null)
						r.NotifyProgress (r.ReadSize, r.ExpectedSize);
					r.BeginRead (0, DATA_SIZE, ReadFileToMemory_ReadPacketHeader);
				}
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public MemoryStream EndReadFileToMemory (IAsyncResult result)
		{
			var ar = (ReadFileToMemoryAsyncResult) result;
			ar.CheckError (token);
			return ar.MemoryStream;
		}
	
		class ReadFileToMemoryAsyncResult : ReadFullAsyncResult
		{
			public ReadFileToMemoryAsyncResult (NetworkStream stream, byte[] buffer, string remotePath,
				int memoryStreamLengthCap, AdbProgressReporter notifyProgress,
				AsyncCallback callback, object state)
				: base (stream, buffer, callback, state)
			{
				this.RemotePath = remotePath;
				this.MemoryStreamLengthCap = memoryStreamLengthCap;
				this.NotifyProgress = notifyProgress;
			}
			
			public string RemotePath;
			public AdbProgressReporter NotifyProgress;
			public int MemoryStreamLengthCap;
			public MemoryStream MemoryStream;
			public long ExpectedSize, ReadSize;
			public int PacketSize, PacketRead;
		}
		
		/// <summary>
		/// Reads a file from the remote device into a memory stream.
		/// </summary>
		/// <remarks>Con only read regular files.</remarks>
		public MemoryStream ReadFileToMemory (string remotePath, int memoryStreamLengthCap = 0, AdbProgressReporter notifyProgress = null)
		{
			if (remotePath == null)
				throw new ArgumentNullException (remotePath);
			
			var info = Stat (remotePath);
			if (!info.IsFileType (AdbFileMode.S_IFREG)) {
				throw new AdbException ("Can only read regular files");
			}
			long size = info.Size, totalRead = 0;
			
			memoryStreamLengthCap = GetCappedMemorySize (memoryStreamLengthCap);
			int msSize = CheckRemoteSizeFitsCap (size, memoryStreamLengthCap);
			var ms = new MemoryStream (msSize);
			
			WriteReq (ID_RECV, remotePath);
			
			do {
				client.Stream.ReadFull (buf, 0, DATA_SIZE);
				var id = AdbClient.FourCCToInt (buf, DATA_OFFSET_ID);
				if (id == ID_DONE)
					break;
				CheckPacketId (id, ID_DATA);
				var dataSize = (int)DataConverter.LittleEndian.GetUInt32 (buf, DATA_OFFSET_SIZE);
				if (dataSize > SYNC_DATA_MAX)
					throw new AdbException ("Data packet is bigger than maximum");
				CheckRemoteSizeFitsCap (dataSize + totalRead, memoryStreamLengthCap);
				
				//the packet may read in smaller chunks than expected
				int dataRead = 0;
				do {
					dataRead += client.Stream.Read (buf, dataRead, dataSize - dataRead);
				} while (dataRead < dataSize);
				
				ms.Write (buf, 0, dataSize);
				
				totalRead += dataRead;
				if (notifyProgress != null)
					notifyProgress (totalRead, size);
			} while (true);
			
			ms.Position = 0;
			return ms;
		}
		
		#endregion
		
		#region Pull
		
		public IAsyncResult BeginPull (string remoteFilePath, string localFilePath, AsyncCallback callback, object state)
		{
			return BeginPull (localFilePath, remoteFilePath, null, callback, state);
		}
		
		public IAsyncResult BeginPull (string remoteFilePath, string localFilePath, AdbProgressReporter notifyProgress,
			AsyncCallback callback, object state)
		{
			var localStream = File.OpenWrite (localFilePath);
			var r = new AggregateAsyncResult<IDisposable,long> (localStream, 0, callback, state);
			BeginReadFile (remoteFilePath, localStream.Write, notifyProgress, Pull_OnReadFile, r);
			return r;
		}
		
		void Pull_OnReadFile (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<IDisposable,long>) result.AsyncState;
			try {
				r.Arg2 = EndWriteFile (result);
				r.Arg1.Dispose ();
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		public long EndPull (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<IDisposable,long>) result;
			r.CheckError (token);
			return r.Arg2;
		}
		
		public long Pull (string remoteFilePath, string localFilePath, AdbProgressReporter notifyProgress=null)
		{
			using (var file = File.OpenWrite (localFilePath)) {
				return ReadFile (remoteFilePath, file.Write, notifyProgress);
			}
		}
		
		#endregion
		
		#region WriteReq
		
		IAsyncResult BeginWriteReq (int id, string remotePath, AsyncCallback callback, object state)
		{
			var pathBytes = AdbClient.TextEncoding.GetBytes (remotePath);
			if (pathBytes.Length > MAX_PATH)
				throw new ArgumentException ("Path is too long");
			
			AdbClient.IntToFourCC (id, buf, REQ_OFFSET_ID);
			DataConverter.LittleEndian.PutBytes (buf, REQ_OFFSET_NAMELEN, (uint)pathBytes.Length);
			
			var ar = new AggregateAsyncResult<byte[]> (pathBytes, callback, state);
			client.Stream.BeginWrite (buf, 0, REQ_SIZE, WriteReq_OnWroteHeader, ar);
			return ar;
		}
		
		void WriteReq_OnWroteHeader (IAsyncResult result)
		{
			var r = (AggregateAsyncResult<byte[]>) result.AsyncState;
			try {
				client.Stream.EndWrite (result);
				client.Stream.BeginWrite (r.Arg, 0, r.Arg.Length, WriteReq_OnWrotePath, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void WriteReq_OnWrotePath (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result.AsyncState;
			try {
				client.Stream.EndWrite (result);
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		static void EndWriteReq (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result;
			r.CheckError ();
		}
		
		/// <summary>
		/// Writes a request packet.
		/// </summary>
		void WriteReq (int id, string remotePath)
		{
			var pathBytes = AdbClient.TextEncoding.GetBytes (remotePath);
			if (pathBytes.Length > MAX_PATH)
				throw new ArgumentException ("Path is too long");
			
			AdbClient.IntToFourCC (id, buf, REQ_OFFSET_ID);
			DataConverter.LittleEndian.PutBytes (buf, REQ_OFFSET_NAMELEN, (uint)pathBytes.Length);
			
			client.Stream.Write (buf, 0, REQ_SIZE);
			client.Stream.Write (pathBytes, 0, pathBytes.Length);
		}
		
		#endregion
		
		#region ReadFailMessage
		
		static void BeginReadFailMessage (ReadFullAsyncResult res)
		{
			res.Count = STAT_SIZE;
			int len = DataConverter.LittleEndian.GetInt32 (res.Buffer, STATUS_OFFSET_MSGLEN);
			if (len > 256)
				len = 256;
			res.BeginRead (0, len, ReadFailMessage_OnRead);
		}
		
		static void ReadFailMessage_OnRead (IAsyncResult ar)
		{
			var r = (ReadFullAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Read != r.Count) {
					r.ContinueRead (ReadFailMessage_OnRead);
				} else {
					string msg = AdbClient.TextEncoding.GetString (r.Buffer, 0, r.Count);
					r.CompleteWithError (new AdbException (msg));
				}
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void EndReadFailMessage (IAsyncResult result)
		{
			var r = (ReadFullAsyncResult) result;
			r.CheckError ();
		}
		
		/// <summary>
		/// Reads the message from an ID_FAIL packet and throws an exception containing the message.
		/// </summary>
		void ReadFailMessage (byte[] buffer, int packetRead)
		{
			while (packetRead < STATUS_SIZE) {
				packetRead += client.Stream.Read (buf, packetRead, STATUS_SIZE - packetRead);
			}
			int len = DataConverter.LittleEndian.GetInt32 (buffer, STATUS_OFFSET_MSGLEN);
			if (len > 256)
				len = 256;
			client.Stream.ReadFull (buffer, 0, len);
			string msg = AdbClient.TextEncoding.GetString (buffer, 0, len);
			throw new AdbException (msg);
		}
		
		#endregion
		
		#region CreateDirectories
		
		IAsyncResult BeginCreateDirectories (IEnumerable<AdbSyncNotification> items, bool dryRun, Action<AdbSyncNotification> notifySync,
			AsyncCallback callback, object state)
		{
			CreateDirectories_ComputeRedundant (items);
			var ar = new CreateDirectoriesAsyncResult (items.GetEnumerator (), dryRun, notifySync, callback, state);
			CreateDirectories_CreateNextItem (ar);
			return ar;
		}
		
		void CreateDirectories_CreateNextItem (CreateDirectoriesAsyncResult r)
		{
			while (r.Enumerator.MoveNext ()) {
				var item = r.Enumerator.Current;
				if (!item.IsCreateDirectory) {
					continue;
				}
				if (item.Kind != AdbSyncKind.CreateDirectory || item.Redundant || r.DryRun) {
					if (r.NotifySync != null)
						r.NotifySync (item);
					continue;
				}
				var pb = new ProcessArgumentBuilder ();
				pb.Add ("mkdir", "-p");
				pb.AddQuoted (item.RemotePath);
				BeginRunShellCommand (pb.ToString (), CreateDirectories_OnCreatedItem, r);
				return;
			}
			r.Complete ();
		}
		
		void CreateDirectories_OnCreatedItem (IAsyncResult result)
		{
			var r = (CreateDirectoriesAsyncResult) result.AsyncState;
			try {
				EndRunShellCommand (result);
				if (r.NotifySync != null)
					r.NotifySync (r.Enumerator.Current);
				CreateDirectories_CreateNextItem (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void EndCreateDirectories (IAsyncResult result)
		{
			var r = (CreateDirectoriesAsyncResult) result;
			r.CheckError ();
		}
		
		class CreateDirectoriesAsyncResult : AggregateAsyncResult
		{
			public CreateDirectoriesAsyncResult (IEnumerator<AdbSyncNotification> enumerator, bool dryRun,
				Action<AdbSyncNotification> notifySync, AsyncCallback callback, object state)
				: base (callback, state)
			{
				this.Enumerator = enumerator;
				this.DryRun = dryRun;
				this.NotifySync = notifySync;
			}
			
			public IEnumerator<AdbSyncNotification> Enumerator { get; private set; }
			public bool DryRun { get; private set; }
			public Action<AdbSyncNotification> NotifySync { get; private set; }
		}
		
		//adb sync service doesn't support creating directories (it ignores the mode bit)
		//so separate them out and use shell:mkdir -p
		void CreateDirectories (IEnumerable<AdbSyncNotification> items, bool dryRun, Action<AdbSyncNotification> notifySync)
		{
			//create directories one at a time, else we could overrun MAX_COMMAND
			CreateDirectories_ComputeRedundant (items);
			foreach (var item in items) {
				if (!item.IsCreateDirectory)
					continue;
				if (item.Kind == AdbSyncKind.CreateDirectory && !item.Redundant && !dryRun) {
					var pb = new ProcessArgumentBuilder ();
					pb.Add ("mkdir", "-p");
					pb.AddQuoted (item.RemotePath);
					RunShellCommand (pb.ToString ());
				}
				if (notifySync != null)
					notifySync (item);
			}
		}
		
		void CreateDirectories_ComputeRedundant (IEnumerable<AdbSyncNotification> items)
		{
			var filtered = new List<KeyValuePair<string,AdbSyncNotification>> ();
			//skip all the directories that have children also being created
			foreach (var item in items) {
				//ignore items that aren't directories or already exist, they don't affect this
				if (item.Kind != AdbSyncKind.CreateDirectory)
					continue;
				bool foundParentDir = false;
				var itemWithTrailingSlash = EnsureTrailingSlash (item.RemotePath);
				for (int i = 0; i < filtered.Count; i++) {
					var existingItem = filtered[i];
					//if an existing directory is a parent of this, replace it
					if (itemWithTrailingSlash.StartsWith (existingItem.Key)) {
						foundParentDir = true;
						existingItem.Value.Redundant = true;
						filtered[i] = new KeyValuePair<string,AdbSyncNotification> (itemWithTrailingSlash, item);
						break;
					}
					if (existingItem.Key.StartsWith (itemWithTrailingSlash)) {
						item.Redundant = true;
						break;
					}
				}
				if (!foundParentDir) {
					filtered.Add (new KeyValuePair<string,AdbSyncNotification> (itemWithTrailingSlash, item));
				}
			}
		}
		
		#endregion
		
		#region RemoveItems
		
		IAsyncResult BeginRemoveItems (IEnumerable<AdbSyncNotification> items, bool dryRun,
			Action<AdbSyncNotification> notifySync, AsyncCallback callback, object state)
		{
			RemoveItems_ComputeRedundant (items);
			var ar = new RemoveItemsAsyncResult (items.GetEnumerator (), dryRun, notifySync, callback, state);
			RemoveItems_RemoveNextItem (ar);
			return ar;
		}
		
		void RemoveItems_RemoveNextItem (RemoveItemsAsyncResult r)
		{
			while (r.Enumerator.MoveNext ()) {
				var item = r.Enumerator.Current;
				if (!item.IsRemove)
					continue;
				if (item.Redundant || r.DryRun) {
					if (r.NotifySync != null)
						r.NotifySync (item);
					continue;
				}
				var pb = new ProcessArgumentBuilder ();
				pb.Add ("rm", "-r");
				pb.AddQuoted (item.RemotePath);
				BeginRunShellCommand (pb.ToString (), RemoveItems_OnRemovedItem, r);
				return;
			}
			r.Complete ();
		}
		
		void RemoveItems_OnRemovedItem (IAsyncResult result)
		{
			var r = (RemoveItemsAsyncResult) result.AsyncState;
			try {
				EndRunShellCommand (result);
				if (r.NotifySync != null) {
					r.NotifySync (r.Enumerator.Current);
				}
				RemoveItems_RemoveNextItem (r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void EndRemoveItems (IAsyncResult result)
		{
			var r = (RemoveItemsAsyncResult) result;
			r.CheckError ();
		}
		
		class RemoveItemsAsyncResult : AggregateAsyncResult
		{
			public RemoveItemsAsyncResult (IEnumerator<AdbSyncNotification> enumerator, bool dryRun,
				Action<AdbSyncNotification> notifySync, AsyncCallback callback, object state)
				: base (callback, state)
			{
				this.Enumerator = enumerator;
				this.DryRun = dryRun;
				this.NotifySync = notifySync;
			}
			
			public IEnumerator<AdbSyncNotification> Enumerator { get; private set; }
			public bool DryRun { get; private set; }
			public Action<AdbSyncNotification> NotifySync { get; private set; }
		}
		
		//adb sync service doesn't support deleting items (ID_ULNK id defined but not implemented)
		//so separate them out and use shell:rm -r
		void RemoveItems (IEnumerable<AdbSyncNotification> items, bool dryRun, Action<AdbSyncNotification> notifySync)
		{
			if (!dryRun) {
				//remove items one at a time, if we do too many at once are likely to overrun MAX_COMMAND
				RemoveItems_ComputeRedundant (items);
				foreach (var item in items) {
					if (!item.IsRemove)
						continue;
					if (!item.Redundant) {
						var pb = new ProcessArgumentBuilder ();
						pb.Add ("rm", "-r");
						pb.AddQuoted (item.RemotePath);
						RunShellCommand (pb.ToString ());
					}
					if (notifySync != null)
						notifySync (item);
				}
			}
		}
		
		void RemoveItems_ComputeRedundant (IEnumerable<AdbSyncNotification> itemsToRemove)
		{
			var highestDirsToRemove = new List<string> ();
			
			//add the directories, skip any whose parent dir is being removed
			foreach (var item in itemsToRemove) {
				if ((item.Kind & (AdbSyncKind.RemoveDirectory | AdbSyncKind.RemoveUnknownDirectory)) == 0)
					continue;
				var itemWithTrailingSlash = EnsureTrailingSlash (item.RemotePath);
				bool foundParentDir = false;
				for (int i = highestDirsToRemove.Count - 1;  i >= 0; i--) {
					string dir = highestDirsToRemove[i];
					//remove any items that are children of this item
					if (dir.StartsWith (itemWithTrailingSlash)) {
						highestDirsToRemove.RemoveAt (i--);
					}
					//if we find the parent of this item, we don't need to add it or check for more children
					else if (itemWithTrailingSlash.StartsWith (dir)) {
						foundParentDir = true;
						break;
					}
				}
				if (!foundParentDir) {
					highestDirsToRemove.Add (itemWithTrailingSlash);
				}
			}
			
			//mark redundant removes (things being removed in dirs being removed)
			foreach (var item in itemsToRemove) {
				if (!item.IsRemove)
					continue;
				//check whether it's a child of any of the directory items we added already
				for (int i = 0;  i < highestDirsToRemove.Count; i++) {
					if (item.RemotePath.StartsWith (highestDirsToRemove[i])) {
						item.Redundant = true;
						break;
					}
				}
			}
		}
		
		#endregion
		
		#region RunShellCommand
		
		IAsyncResult BeginRunShellCommand (string command, AsyncCallback callback, object state)
		{
			var r = new ShellCommandAsyncResult (CreateClient (), command, new StringWriter (), callback, state);
			shellClient = r.Client;
			shellClient.BeginConnectTransport (deviceID, RunShellCommand_OnGotTransport, r);
			return r;
		}
		
		void RunShellCommand_OnGotTransport (IAsyncResult result)
		{
			var r = (ShellCommandAsyncResult) result.AsyncState;
			try {
				r.Client.EndConnectTransport (result);
				r.Client.BeginWriteCommandWithStatus ("shell:" + r.Command, RunShellCommand_OnWroteCommand, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void RunShellCommand_OnWroteCommand (IAsyncResult result)
		{
			var r = (ShellCommandAsyncResult) result.AsyncState;
			try {
				r.Client.EndWriteCommandWithStatus (result);
				r.Client.BeginReadText (r.Writer.Write, RunShellCommand_OnGotText, r);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		void RunShellCommand_OnGotText (IAsyncResult result)
		{
			var r = (ShellCommandAsyncResult) result.AsyncState;
			try {
				r.Client.EndReadText (result);
				r.Complete ();
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		string EndRunShellCommand (IAsyncResult result)
		{
			var r = (ShellCommandAsyncResult) result;
			r.CheckError ();
			return r.Writer.ToString ();
		}
		
		class ShellCommandAsyncResult : AggregateAsyncResult
		{
			public ShellCommandAsyncResult (AdbClient client, string command, StringWriter writer, AsyncCallback callback, object state)
				: base (callback, state)
			{
				this.Client = client;
				this.Command = command;
				this.Writer = writer;
			}
			
			public AdbClient Client { get; private set; }
			public string Command { get; private set; }
			public StringWriter Writer { get; private set; }
		}
		
		string RunShellCommand (string command)
		{
			var sc = CreateClient ();
			try {
				shellClient = sc;
				sc.ConnectTransport (deviceID);
				sc.WriteCommandWithStatus ("shell:" + command);
				var sw = new StringWriter ();
				sc.ReadText (sw.Write);
				return sw.ToString ();
			} finally {
				sc.Dispose ();
			}
		}
		
		#endregion
		
		#region Utility
		
		static string EnsureTrailingSlash (string directoryPath)
		{
			if (directoryPath[directoryPath.Length-1] != '/')
				return directoryPath + "/";
			return directoryPath;
		}
		
		/// <summary>
		/// Throws an exception if the packet ID does not match the expected value.
		/// </summary>
		static void CheckPacketId (int received, int expected)
		{
			if (received == expected)
				return;
			throw new AdbException (string.Format ("Packet was of wrong kind, expected '{0}', got '{1}'",
				AdbClient.IntToFourCCString (expected), AdbClient.IntToFourCCString (received).Replace ("\0", "\\0")));
		}
		
		/// <summary>
		/// Utility method that any AggregateAsyncResult can use as callback for BeginReadFailMessage.
		/// </summary>
		void AggregateResult_OnReadFail (IAsyncResult result)
		{
			var r = (AggregateAsyncResult) result.AsyncState;
			try {
				//always throws
				EndReadFailMessage (result);
			} catch (Exception ex) {
				r.CompleteWithError (AdbClient.ConvertException (ex));
			}
		}
		
		#endregion
	}
	
	public delegate void AdbProgressReporter (long copiedBytes, long totalBytes);
}