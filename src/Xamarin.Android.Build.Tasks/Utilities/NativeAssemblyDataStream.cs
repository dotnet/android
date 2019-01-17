using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	class NativeAssemblyDataStream : Stream
	{
		const uint MapVersionFound     = 0x0001;
		const uint MapEntryCountFound  = 0x0002;
		const uint MapEntryLengthFound = 0x0004;
		const uint MapValueOffsetFound = 0x0008;
		const uint MapEverythingFound  = MapVersionFound | MapEntryCountFound | MapEntryLengthFound | MapValueOffsetFound;

		const string MapFieldVersion     = "version";
		const string MapFieldEntryCount  = "entry-count";
		const string MapFieldEntryLength = "entry-len";
		const string MapFieldValueOffset = "value-offset";

		const uint MaxFieldsInRow = 32;

		MemoryStream outputStream;
		StreamWriter outputWriter;
		bool firstWrite = true;
		uint rowFieldCounter;
		SHA1CryptoServiceProvider sha1;
		bool hashingFinished;
		uint byteCount = 0;

		public int MapVersion     { get; set; } = -1;
		public int MapEntryCount  { get; set; } = -1;
		public int MapEntryLength { get; set; } = -1;
		public int MapValueOffset { get; set; } = -1;
		public uint MapByteCount => byteCount;

		public override bool CanRead => outputStream.CanRead;
		public override bool CanSeek => outputStream.CanSeek;
		public override bool CanWrite => outputStream.CanWrite;
		public override long Length => outputStream.Length;

		public override long Position {
			get => outputStream.Position;
			set => outputStream.Position = value;
		}

		public NativeAssemblyDataStream ()
		{
			outputStream = new MemoryStream ();
			outputWriter = new StreamWriter (outputStream, new UTF8Encoding (false));
			sha1 = new SHA1CryptoServiceProvider ();
			sha1.Initialize ();
		}

		public byte[] GetStreamHash ()
		{
			if (!hashingFinished) {
				sha1.TransformFinalBlock (new byte[0], 0, 0);
				hashingFinished = true;
			}

			return sha1.Hash;
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			if (!disposing)
				return;

			outputWriter.Dispose ();
			outputWriter = null;
			outputStream = null;
		}

		public override void Flush ()
		{
			outputWriter.Flush ();
			outputStream.Flush ();
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			return outputStream.Read (buffer, offset, count);
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			return outputStream.Seek (offset, origin);
		}

		public override void SetLength (long value)
		{
			outputStream.SetLength (value);
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));
			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));
			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));
			if (offset + count > buffer.Length)
				throw new ArgumentException ($"The sum of the '{nameof (offset)}' and '{nameof (count)}' arguments is greater than the buffer length");
			if (outputWriter == null)
				throw new ObjectDisposedException (this.GetType ().Name);

			if (!hashingFinished)
				sha1.TransformBlock (buffer, offset, count, null, 0);

			if (firstWrite) {
				// Kind of a backward thing to do, but I wanted to avoid having to modfy (and bump the
				// submodule of) Java.Interop. This should be Safe Enough™ (until it's not)
				string line = Encoding.UTF8.GetString (buffer, offset, count);

				// Format used by Java.Interop.Tools.JavaCallableWrappers.TypeNameMapGenerator:
				//
				//  "version=1\u0000entry-count={0}\u0000entry-len={1}\u0000value-offset={2}\u0000"
				//
				string[] parts = line.Split ('\u0000');
				if (parts.Length != 5)
					HeaderFormatError ("invalid number of fields");

				// Let's be just slightly flexible :P
				uint foundMask = 0;
				foreach (string p in parts) {
					string field = p.Trim ();
					if (String.IsNullOrEmpty (field))
						continue;

					string[] fieldParts = field.Split(new char[]{'='}, 2);
					if (fieldParts.Length != 2)
						HeaderFormatError ($"invalid field format '{field}'");

					switch (fieldParts [0]) {
						case MapFieldVersion:
							foundMask |= MapVersionFound;
							MapVersion = GetHeaderInteger (fieldParts[0], fieldParts [1]);
							break;

						case MapFieldEntryCount:
							foundMask |= MapEntryCountFound;
							MapEntryCount = GetHeaderInteger (fieldParts[0], fieldParts [1]);
							break;

						case MapFieldEntryLength:
							foundMask |= MapEntryLengthFound;
							MapEntryLength = GetHeaderInteger (fieldParts[0], fieldParts [1]);
							break;

						case MapFieldValueOffset:
							foundMask |= MapValueOffsetFound;
							MapValueOffset = GetHeaderInteger (fieldParts[0], fieldParts [1]);
							break;

						default:
							HeaderFormatError ($"unknown field '{fieldParts [0]}'");
							break;
					}
				}

				if (foundMask != MapEverythingFound) {
					var missingFields = new List <string> ();
					if ((foundMask & MapVersionFound) == 0)
						missingFields.Add (MapFieldVersion);
					if ((foundMask & MapEntryCountFound) == 0)
						missingFields.Add (MapFieldEntryCount);
					if ((foundMask & MapEntryLengthFound) == 0)
						missingFields.Add (MapFieldEntryLength);
					if ((foundMask & MapValueOffsetFound) == 0)
						missingFields.Add (MapFieldValueOffset);

					var sb = new StringBuilder ("missing header field");
					if (missingFields.Count > 1)
						sb.Append ('s');
					sb.Append (": ");
					sb.Append (String.Join (", ", missingFields));
					HeaderFormatError (sb.ToString ());
				}

				firstWrite = false;
				rowFieldCounter = 0;
				return;
			}

			for (int i = 0; i < count; i++) {
				byteCount++;
				if (rowFieldCounter == 0)
					outputWriter.Write ("\t.byte ");

				byte b = buffer [offset + i];
				if (rowFieldCounter > 0)
					outputWriter.Write (", ");
				outputWriter.Write ($"0x{b:x02}");

				rowFieldCounter++;
				if (rowFieldCounter > MaxFieldsInRow) {
					rowFieldCounter = 0;
					outputWriter.WriteLine ();
				}
			}

			void HeaderFormatError (string whatsWrong)
			{
				throw new InvalidOperationException ($"Java.Interop.Tools.JavaCallableWrappers.TypeNameMapGenerator header format changed: {whatsWrong}");
			}

			int GetHeaderInteger (string name, string value)
			{
				int ret;
				if (!Int32.TryParse (value, out ret))
					HeaderFormatError ($"failed to parse integer value from '{value}' for field '{name}'");
				return ret;
			}
		}
	};
}
