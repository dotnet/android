//
// StreamExtensions.cs
//
// Author:
//       Michael Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2011-2014 Xamarin Inc.
//

using System;
using System.IO;
using Mono.AndroidTools.Adb;

namespace Mono.AndroidTools.Util
{
	static class StreamExtensions
	{
		/// <summary>
		/// Guaranteed to read the requested number of bytes from the stream.
		/// </summary>
		public static void ReadFull (this Stream stream, byte[] buffer, int offset, int count)
		{
			while (true) {
				int read = stream.Read (buffer, offset, count);
				if (read == count)
					return;
				if (read == 0)
					throw new EndOfStreamException ();
				offset += read;
				count -= read;
			}
		}
		
		public static IAsyncResult BeginReadFull (this Stream stream, byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			var r = new ReadFullAsyncResult (stream, buffer, callback, state);
			r.BeginRead (offset, count, BeginReadFull_GotData);
			return r;
		}
		
		static void BeginReadFull_GotData (IAsyncResult ar)
		{
			var r = (ReadFullAsyncResult) ar.AsyncState;
			try {
				r.EndRead (ar);
				if (r.Read == r.Count) {
					r.Complete ();
				} else {
					r.ContinueRead (BeginReadFull_GotData);
				}
			} catch (Exception ex) {
				r.CompleteWithError (ex);
			}
		}
		
		public static void EndReadFull (this Stream stream, IAsyncResult asyncResult)
		{
			var r = (ReadFullAsyncResult) asyncResult;
			r.CheckError ();
		}
	}

	class ReadFullAsyncResult : AggregateAsyncResult
	{
		public ReadFullAsyncResult (Stream stream, byte[] buffer, AsyncCallback callback, object state)
			: base (callback, state)
		{
			this.Stream = stream;
			this.Buffer = buffer;
		}

		public Stream Stream;
		public byte[] Buffer;
		public int Offset, Count, Read;

		public void BeginRead (int offset, int count, AsyncCallback callback)
		{
			Offset = offset;
			Count = count;
			Read = 0;
			Stream.BeginRead (Buffer, Offset, Count, callback, this);
		}

		public void ContinueRead (AsyncCallback callback)
		{
			Stream.BeginRead (Buffer, Offset + Read, Count - Read, callback, this);
		}

		public int EndRead (IAsyncResult ar)
		{
			int read = Stream.EndRead (ar);
			if (read == 0)
				throw new EndOfStreamException ();
			Read += read;
			return read;
		}

		public int Remaining {
			get { return Count - Read; }
		}
	}
}