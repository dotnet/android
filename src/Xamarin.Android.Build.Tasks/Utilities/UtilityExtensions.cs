using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
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

	public static T GetAttributeOrDefault<T> (this XElement xml, string name, T defaultValue)
	{
		var value = xml.Attribute (name)?.Value;

		if (string.IsNullOrWhiteSpace (value))
			return defaultValue;

		return (T) Convert.ChangeType (value, typeof (T));
	}

	[return: NotNullIfNotNull (nameof (defaultValue))]
	public static uint? GetUIntAttributeOrDefault (this XElement xml, string name, uint? defaultValue)
	{
		var value = xml.Attribute (name)?.Value;

		if (string.IsNullOrWhiteSpace (value))
			return defaultValue;

		if (uint.TryParse (value, out var result))
			return result;

		return defaultValue;
	}

	public static string GetRequiredAttribute (this XElement xml, string name)
	{
		var value = xml.Attribute (name)?.Value;

		if (string.IsNullOrWhiteSpace (value))
			throw new InvalidOperationException ($"Missing required attribute '{name}'");

		return value!;  // NRT - Guarded by IsNullOrWhiteSpace check above
	}

	public static void WriteAttributeStringIfNotDefault (this XmlWriter xml, string name, string? value)
	{
		if (value.HasValue ())
			xml.WriteAttributeString (name, value);
	}

	public static void WriteAttributeStringIfNotDefault (this XmlWriter xml, string name, bool value)
	{
		// If value is false, don't write the attribute, we'll default to false on import
		if (value)
			xml.WriteAttributeString (name, value.ToString ());
	}
}
