using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools.Adb;

namespace Mono.AndroidTools.Internal
{
	// Reads newline-separated output into one fixed buffer, so neither the whole output
	// nor a single unterminated line can grow past the byte limit.
	sealed class AdbBoundedLineReader
	{
		const int ReadChunkSize = 4096;

		readonly byte [] buffer;
		readonly int maxLines;
		readonly Action<string> outputLine;
		int lineStart;

		public AdbBoundedLineReader (int maxBytes, int maxLines, Action<string> outputLine)
		{
			buffer = new byte [maxBytes];
			this.maxLines = maxLines;
			this.outputLine = outputLine;
		}

		public int Bytes { get; private set; }

		public int Lines { get; private set; }

		public async Task<BoundedShellCommandStatus> ReadAsync (Stream stream, CancellationToken token)
		{
			while (true) {
				if (Bytes == buffer.Length)
					return BoundedShellCommandStatus.ByteLimit;

				int read = await stream.ReadAsync (buffer, Bytes, Math.Min (ReadChunkSize, buffer.Length - Bytes), token).ConfigureAwait (false);
				if (read == 0) {
					if (lineStart < Bytes && !TryEmitLine (Bytes))
						return BoundedShellCommandStatus.LineLimit;
					return BoundedShellCommandStatus.Completed;
				}

				int end = Bytes + read;
				for (int i = Bytes; i < end; i++) {
					if (buffer [i] != (byte) '\n')
						continue;
					if (!TryEmitLine (i)) {
						Bytes = end;
						return BoundedShellCommandStatus.LineLimit;
					}
					lineStart = i + 1;
				}
				Bytes = end;
			}
		}

		bool TryEmitLine (int end)
		{
			if (Lines == maxLines)
				return false;
			if (end > lineStart && buffer [end - 1] == (byte) '\r')
				end--;
			outputLine (AdbClient.TextEncoding.GetString (buffer, lineStart, end - lineStart));
			Lines++;
			return true;
		}
	}
}
