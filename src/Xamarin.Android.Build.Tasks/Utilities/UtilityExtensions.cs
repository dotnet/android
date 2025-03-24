using System;
using System.IO;
using System.IO.Compression;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks;

static class UtilityExtensions
{
	public static System.IO.Compression.CompressionLevel ToCompressionLevel (this CompressionMethod method)
	{
		switch (method) {
			case CompressionMethod.Store:
				return System.IO.Compression.CompressionLevel.NoCompression;
			case CompressionMethod.Default:
			case CompressionMethod.Deflate:
				return System.IO.Compression.CompressionLevel.Optimal;
			default:
				throw new ArgumentOutOfRangeException (nameof (method), method, null);
		}
	}

	public static CompressionMethod ToCompressionMethod (this System.IO.Compression.CompressionLevel level)
	{
		switch (level) {
			case System.IO.Compression.CompressionLevel.NoCompression:
				return CompressionMethod.Store;
			case System.IO.Compression.CompressionLevel.Optimal:
				return CompressionMethod.Deflate;
			default:
				throw new ArgumentOutOfRangeException (nameof (level), level, null);
		}
	}

	public static FileMode ToFileMode (this ZipArchiveMode mode)
	{
		switch (mode) {
			case ZipArchiveMode.Create:
				return FileMode.Create;
			case ZipArchiveMode.Update:
				return FileMode.Open;
			default:
				throw new ArgumentOutOfRangeException (nameof (mode), mode, null);
		}
	}
}
