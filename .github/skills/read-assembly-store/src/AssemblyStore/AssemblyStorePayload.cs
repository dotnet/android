using System;
using System.IO;

namespace Xamarin.Android.AssemblyStore;

public static class AssemblyStorePayload
{
	public static bool TryExtractELFPayload (Stream input, Stream output)
	{
		ArgumentNullException.ThrowIfNull (input);
		ArgumentNullException.ThrowIfNull (output);

		if (!input.CanRead || !input.CanSeek) {
			throw new ArgumentException ("Input stream must be readable and seekable", nameof (input));
		}
		if (!output.CanWrite) {
			throw new ArgumentException ("Output stream must be writable", nameof (output));
		}

		long originalPosition = input.Position;
		(ulong offset, ulong size, ELFPayloadError error) = Utils.FindELFPayloadOffsetAndSize (input);
		if (error == ELFPayloadError.NotELF) {
			input.Seek (originalPosition, SeekOrigin.Begin);
			return false;
		}
		if (error != ELFPayloadError.None) {
			throw new InvalidDataException (error switch {
				ELFPayloadError.LoadFailed           => "ELF image could not be loaded",
				ELFPayloadError.NotSharedLibrary     => "ELF image is not a shared library",
				ELFPayloadError.NotLittleEndian      => "ELF image is not little-endian",
				ELFPayloadError.InvalidPayloadSymbol => "ELF image has an invalid '_assembly_store' symbol",
				ELFPayloadError.NoPayloadSection     => "ELF image does not contain a payload",
				_ => $"Unable to locate ELF payload: {error}",
			});
		}
		if (offset > Int64.MaxValue || size > Int64.MaxValue) {
			throw new InvalidDataException ("ELF payload offset or size exceeds the supported stream range");
		}

		input.Seek ((long)offset, SeekOrigin.Begin);
		byte[] buffer = Utils.BytePool.Rent (65535);
		try {
			long remaining = (long)size;
			while (remaining > 0) {
				int read = input.Read (buffer, 0, (int)Math.Min (buffer.Length, remaining));
				if (read == 0) {
					throw new InvalidDataException ("Unexpected end of ELF payload");
				}
				output.Write (buffer, 0, read);
				remaining -= read;
			}
			output.Flush ();
			if (output.CanSeek) {
				output.Seek (0, SeekOrigin.Begin);
			}
			return true;
		} finally {
			Utils.BytePool.Return (buffer);
		}
	}
}
