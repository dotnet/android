using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Android.Tasks;

internal static class NativeAotBootstrapBlob
{
	public const string Symbol = "dotnet_android_nativeaot_bootstrap";
	public const uint Magic = 0x4746434e; // NCFG
	public const int HeaderSize = 32;
	const int MaximumSize = 256 * 1024 * 1024;

	public static byte [] Create (IDictionary<string, string> environment, IDictionary<string, string> properties)
	{
		ArgumentNullException.ThrowIfNull (environment);
		ArgumentNullException.ThrowIfNull (properties);
		var utf8 = new UTF8Encoding (false, true);
		uint propertiesOffset = checked ((uint)HeaderSize + (uint)environment.Count * 8);
		uint stringsOffset = checked (propertiesOffset + (uint)properties.Count * 8);
		if (stringsOffset >= MaximumSize) {
			throw new InvalidDataException ("NativeAOT bootstrap exceeds 256 MiB.");
		}
		using var strings = new MemoryStream ();
		strings.WriteByte (0);
		var offsets = new Dictionary<string, uint> (StringComparer.Ordinal);
		uint Add (string value, bool name)
		{
			if (value == null || value.IndexOf ('\0') >= 0 || name && value.Length == 0) {
				throw new InvalidDataException ("Invalid NativeAOT bootstrap string.");
			}
			if (value.Length == 0) {
				return stringsOffset;
			}
			if (!offsets.TryGetValue (value, out uint offset)) {
				int length = utf8.GetByteCount (value);
				if ((long)stringsOffset + strings.Length + length + 1 > MaximumSize) {
					throw new InvalidDataException ("NativeAOT bootstrap exceeds 256 MiB.");
				}
				offset = checked (stringsOffset + (uint)strings.Position);
				offsets.Add (value, offset);
				strings.Write (utf8.GetBytes (value));
				strings.WriteByte (0);
			}
			return offset;
		}
		using var output = new MemoryStream ();
		using var writer = new BinaryWriter (output, utf8, leaveOpen: true);
		writer.Write (new byte [HeaderSize]);
		foreach (var pair in environment) {
			writer.Write (Add (pair.Key, name: true));
			writer.Write (Add (pair.Value, name: false));
		}
		foreach (var pair in properties) {
			writer.Write (Add (pair.Key, name: true));
			writer.Write (Add (pair.Value, name: false));
		}
		writer.Write (strings.ToArray ());
		output.Position = 0;
		writer.Write (Magic);
		writer.Write ((ushort)1);
		writer.Write ((ushort)0);
		writer.Write (checked ((uint)output.Length));
		writer.Write (checked ((uint)environment.Count));
		writer.Write (checked ((uint)properties.Count));
		writer.Write (propertiesOffset);
		writer.Write (stringsOffset);
		writer.Write (checked ((uint)strings.Length));
		return output.ToArray ();
	}
}
