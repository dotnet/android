using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

abstract class AssemblyStoreReader
{
	static readonly UTF8Encoding ReaderEncoding = new UTF8Encoding (false);

	protected Stream StoreStream                { get; }

	public abstract string Description          { get; }
	public abstract bool NeedsExtensionInName   { get; }
	public string StorePath                     { get; }

	public AndroidTargetArch TargetArch         { get; protected set; } = AndroidTargetArch.Arm;
	public uint AssemblyCount                   { get; protected set; }
	public uint IndexEntryCount                 { get; protected set; }
	public IList<AssemblyStoreItem>? Assemblies { get; protected set; }
	public bool Is64Bit                         { get; protected set; }

	protected AssemblyStoreReader (Stream store, string path)
	{
		StoreStream = store;
		StorePath = path;
	}

	public static AssemblyStoreReader? Create (Stream store, string path)
	{
		AssemblyStoreReader? reader = MakeReaderReady (new StoreReader_V2 (store, path));
		if (reader != null) {
			return reader;
		}

		return null;
	}

	static AssemblyStoreReader? MakeReaderReady (AssemblyStoreReader reader)
	{
		if (!reader.IsSupported ()) {
			return null;
		}

		reader.Prepare ();
		return reader;
	}

	protected BinaryReader CreateReader () => new BinaryReader (StoreStream, ReaderEncoding, leaveOpen: true);

	protected abstract bool IsSupported ();
	protected abstract void Prepare ();
	protected abstract ulong GetStoreStartDataOffset ();

	public virtual Stream ReadEntryImageData (AssemblyStoreItem entry, bool uncompressIfNeeded = false)
	{
		ulong startOffset = GetStoreStartDataOffset ();
		StoreStream.Seek (checked ((long)startOffset + entry.DataOffset), SeekOrigin.Begin);
		var stream = new MemoryStream ();

		const long BufferSize = 65535;
		byte[] buffer = Utils.BytePool.Rent ((int)BufferSize);
		try {
			long remainingToRead = entry.DataSize;
			while (remainingToRead > 0) {
				int nread = StoreStream.Read (buffer, 0, (int)Math.Min (BufferSize, remainingToRead));
				if (nread == 0) {
					throw new EndOfStreamException ($"Unexpected end of assembly store '{StorePath}' while reading '{entry.Name}'");
				}

				stream.Write (buffer, 0, nread);
				remainingToRead -= nread;
			}
		} finally {
			Utils.BytePool.Return (buffer);
		}

		stream.Flush ();
		stream.Seek (0, SeekOrigin.Begin);
		return UncompressIfNeeded (stream, uncompressIfNeeded);
	}

	protected static Stream UncompressIfNeeded (MemoryStream stream, bool uncompressIfNeeded)
	{
		if (!uncompressIfNeeded) {
			return stream;
		}

#if NET11_0_OR_GREATER
		var output = new MemoryStream ();
		if (AssemblyCompression.TryDecompress (stream, output, out _)) {
			stream.Dispose ();
			output.Seek (0, SeekOrigin.Begin);
			return output;
		}

		output.Dispose ();
		stream.Seek (0, SeekOrigin.Begin);
		return stream;
#else // !NET11_0_OR_GREATER
		if (!AssemblyCompression.IsCompressed (stream)) {
			return stream;
		}

		stream.Dispose ();
		throw new NotSupportedException ("Assembly decompression requires .NET 11 or later");
#endif // !NET11_0_OR_GREATER
	}
}
