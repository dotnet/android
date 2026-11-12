using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	sealed class ProcessOutputReader
	{
		static readonly (Encoding Encoding, byte [] Preamble) [] byteOrderMarks = [
			(Encoding.UTF32, [0xff, 0xfe, 0x00, 0x00]),
			(new UTF32Encoding (bigEndian: true, byteOrderMark: true), [0x00, 0x00, 0xfe, 0xff]),
			(Encoding.UTF8, [0xef, 0xbb, 0xbf]),
			(Encoding.Unicode, [0xff, 0xfe]),
			(Encoding.BigEndianUnicode, [0xfe, 0xff]),
		];

		readonly Stream stream;
		readonly Encoding encoding;
		readonly byte [] preamble;
		readonly byte [] bytes;
		Decoder decoder;
		int prefixLength;
		bool endOfStream;

		public char [] Buffer { get; }

		public ProcessOutputReader (StreamReader reader)
		{
			stream = reader.BaseStream;
			encoding = reader.CurrentEncoding;
			preamble = encoding.GetPreamble ();
			bytes = new byte [Math.Max (4096, preamble.Length)];
			Buffer = new char [Math.Max (encoding.GetMaxCharCount (bytes.Length), Encoding.UTF8.GetMaxCharCount (bytes.Length))];
		}

		public async Task<int> ReadAsync (CancellationToken cancellationToken)
		{
			if (endOfStream)
				return 0;
			while (true) {
				int count = await stream.ReadAsync (bytes.AsMemory (prefixLength), cancellationToken).ConfigureAwait (false);
				endOfStream = count == 0;
				count += prefixLength;
				int skip = 0;
				if (decoder == null) {
					var prefix = bytes.AsSpan (0, count);
					if (!endOfStream && NeedsMorePrefix (prefix)) {
						prefixLength = count;
						continue;
					}
					decoder = GetEncoding (prefix, out skip).GetDecoder ();
				}
				prefixLength = 0;
				// Return each decoded pipe read before another cancellable read can discard it.
				int characters = decoder.GetChars (bytes.AsSpan (skip, count - skip), Buffer, flush: endOfStream);
				if (characters > 0 || endOfStream)
					return characters;
			}
		}

		bool NeedsMorePrefix (ReadOnlySpan<byte> prefix)
		{
			if (preamble.Length > 0 && prefix.StartsWith (preamble))
				return false;
			if (prefix.Length < preamble.Length && preamble.AsSpan ().StartsWith (prefix))
				return true;
			foreach (var mark in byteOrderMarks) {
				if (prefix.Length < mark.Preamble.Length && mark.Preamble.AsSpan ().StartsWith (prefix))
					return true;
			}
			return false;
		}

		Encoding GetEncoding (ReadOnlySpan<byte> prefix, out int skip)
		{
			if (preamble.Length > 0 && prefix.StartsWith (preamble)) {
				skip = preamble.Length;
				return encoding;
			}
			foreach (var mark in byteOrderMarks) {
				if (prefix.StartsWith (mark.Preamble)) {
					skip = mark.Preamble.Length;
					return mark.Encoding;
				}
			}
			skip = 0;
			return encoding;
		}
	}
}
