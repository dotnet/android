#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Android.Tasks;

// The BLBB envelope is written by the final ELF assembly task. Offsets here are
// relative to this raw body, so the same data is shared by every target ABI.
internal static class CoreClrBootstrapBlob
{
	public const string Symbol = "coreclr_bootstrap";
	const uint Magic = 0x47464358; // XCFG
	const ushort Version = 1;
	const int HeaderSize = 84;
	const int MaximumSize = 256 * 1024 * 1024;
	static readonly UTF8Encoding Utf8 = new (false, true);

	public static byte [] Create (
		bool ignoreSplitConfigs, bool haveAssemblyStore, uint packageNamingPolicy,
		uint assemblyCount, uint bundledNameWidth, uint sharedLibraryCount, string packageName,
		IDictionary<string, string> environment, IDictionary<string, string> systemProperties,
		IDictionary<string, string> runtimeProperties,
		IReadOnlyList<(uint Hash, bool Ignore, bool IsJniLibrary, string Name)> libraries,
		IReadOnlyList<uint> preloads, uint preloadStride)
	{
		if (string.IsNullOrEmpty (packageName) || libraries == null || preloads == null ||
			environment == null || systemProperties == null || runtimeProperties == null ||
			preloadStride == 0 || preloads.Count % preloadStride != 0) {
			throw new InvalidDataException ("Invalid CoreCLR bootstrap configuration.");
		}

		static List<KeyValuePair<string, string>> Normalized (IDictionary<string, string> values)
		{
			var result = new List<KeyValuePair<string, string>> (values.Count);
			foreach (var pair in values) {
				string name = pair.Key.Trim ();
				if (name.Length > 0) {
					result.Add (new (name, pair.Value));
				}
			}
			return result;
		}
		var environmentPairs = Normalized (environment);
		var systemPairs = Normalized (systemProperties);
		var runtimeNames = new List<KeyValuePair<string, string>> {
			new ("HOST_RUNTIME_CONTRACT", ""),
			new ("RUNTIME_IDENTIFIER", ""),
			new ("APP_CONTEXT_BASE_DIRECTORY", ""),
		};
		foreach (var pair in runtimeProperties) {
			if (pair.Key != runtimeNames [0].Key && pair.Key != runtimeNames [1].Key && pair.Key != runtimeNames [2].Key) {
				runtimeNames.Add (pair);
			}
		}
		var strings = new MemoryStream ();
		strings.WriteByte (0);
		var stringOffsets = new Dictionary<string, uint> (StringComparer.Ordinal);
		uint envOffset = HeaderSize;
		uint sysOffset = checked (envOffset + (uint)environmentPairs.Count * 8);
		uint runtimeOffset = checked (sysOffset + (uint)systemPairs.Count * 8);
		uint dsoOffset = checked (runtimeOffset + (uint)runtimeNames.Count * 8);
		uint preloadsOffset = checked (dsoOffset + (uint)libraries.Count * 12);
		uint stringsOffset = checked (preloadsOffset + (uint)preloads.Count * 4);
		if (stringsOffset >= MaximumSize) {
			throw new InvalidDataException ("CoreCLR bootstrap configuration exceeds 256 MiB.");
		}
		uint Add (string value, bool required = false)
		{
			if (value == null || value.IndexOf ('\0') >= 0 || required && value.Length == 0) {
				throw new InvalidDataException ("Invalid CoreCLR bootstrap string.");
			}
			if (value.Length == 0) {
				return 0;
			}
			if (!stringOffsets.TryGetValue (value, out uint offset)) {
				int byteCount = Utf8.GetByteCount (value);
				if ((long)stringsOffset + strings.Length + byteCount + 1 > MaximumSize) {
					throw new InvalidDataException ("CoreCLR bootstrap configuration exceeds 256 MiB.");
				}
				offset = checked (stringsOffset + (uint)strings.Position);
				stringOffsets.Add (value, offset);
				byte [] bytes = Utf8.GetBytes (value);
				strings.Write (bytes, 0, bytes.Length);
				strings.WriteByte (0);
			}
			return offset;
		}

		using var output = new MemoryStream ();
		using var writer = new BinaryWriter (output, Utf8, leaveOpen: true);
		writer.Write (new byte [HeaderSize]);
		void WritePairs (IEnumerable<KeyValuePair<string, string>> pairs, bool runtime = false)
		{
			int index = 0;
			foreach (var pair in pairs) {
				writer.Write (Add (pair.Key, required: true));
				writer.Write (runtime && index < 3 ? 0u : Add (pair.Value));
				index++;
			}
		}
		WritePairs (environmentPairs);
		WritePairs (systemPairs);
		WritePairs (runtimeNames, runtime: true);
		foreach (var library in libraries) {
			writer.Write (library.Hash);
			writer.Write ((byte)(library.Ignore ? 1 : 0));
			writer.Write ((byte)(library.IsJniLibrary ? 1 : 0));
			writer.Write ((ushort)0);
			writer.Write (Add (library.Name, required: true));
		}
		foreach (uint preload in preloads) {
			if (preload >= libraries.Count) {
				throw new InvalidDataException ("CoreCLR JNI preload index exceeds the DSO cache.");
			}
			writer.Write (preload);
		}
		if (output.Position != stringsOffset) {
			throw new InvalidDataException ("CoreCLR bootstrap table layout mismatch.");
		}
		uint packageOffset = Add (packageName, required: true);
		writer.Write (strings.ToArray ());
		if (output.Length > MaximumSize) {
			throw new InvalidDataException ("CoreCLR bootstrap configuration exceeds 256 MiB.");
		}
		output.Position = 0;
		writer.Write (Magic);
		writer.Write (Version);
		writer.Write ((ushort)((ignoreSplitConfigs ? 1 : 0) | (haveAssemblyStore ? 2 : 0)));
		writer.Write (checked ((uint)output.Length));
		writer.Write (checked ((uint)environmentPairs.Count));
		writer.Write (checked ((uint)systemPairs.Count));
		writer.Write (checked ((uint)runtimeNames.Count));
		writer.Write (checked ((uint)libraries.Count));
		writer.Write (sharedLibraryCount);
		writer.Write (checked ((uint)preloads.Count));
		writer.Write (preloadStride);
		writer.Write (envOffset);
		writer.Write (sysOffset);
		writer.Write (runtimeOffset);
		writer.Write (dsoOffset);
		writer.Write (preloadsOffset);
		writer.Write (stringsOffset);
		writer.Write (checked ((uint)strings.Length));
		writer.Write (packageNamingPolicy);
		writer.Write (assemblyCount);
		writer.Write (bundledNameWidth);
		writer.Write (packageOffset);
		byte [] raw = output.ToArray ();
		Validate (raw);
		return raw;
	}

	public static void Validate (byte [] raw)
	{
		if (raw == null || raw.Length < HeaderSize || raw.Length > MaximumSize) {
			throw new InvalidDataException ("Invalid CoreCLR bootstrap body length.");
		}
		using var reader = new BinaryReader (new MemoryStream (raw), Utf8);
		if (reader.ReadUInt32 () != Magic || reader.ReadUInt16 () != Version || reader.ReadUInt16 () > 3 ||
			reader.ReadUInt32 () != raw.Length) {
			throw new InvalidDataException ("Invalid CoreCLR bootstrap header.");
		}
		uint [] counts = new uint [5];
		for (int i = 0; i < 4; i++) {
			counts [i] = reader.ReadUInt32 ();
		}
		reader.ReadUInt32 (); // Number of packaged native libraries.
		counts [4] = reader.ReadUInt32 ();
		uint stride = reader.ReadUInt32 ();
		uint [] offsets = new uint [6];
		for (int i = 0; i < offsets.Length; i++) {
			offsets [i] = reader.ReadUInt32 ();
		}
		uint stringLength = reader.ReadUInt32 ();
		reader.ReadUInt32 (); // Package naming policy.
		reader.ReadUInt32 (); // Assembly count.
		reader.ReadUInt32 (); // Bundled assembly name width.
		uint packageOffset = reader.ReadUInt32 ();
		ulong next = HeaderSize;
		uint [] entrySizes = [8, 8, 8, 12, 4];
		for (int i = 0; i < entrySizes.Length; i++) {
			if (offsets [i] != next) {
				throw new InvalidDataException ("Invalid CoreCLR bootstrap table offset.");
			}
			next += (ulong)counts [i] * entrySizes [i];
		}
		if (stride == 0 || counts [4] % stride != 0 || counts [2] < 3 ||
			offsets [5] != next || next + stringLength != (ulong)raw.Length ||
			stringLength == 0 || raw [(int)next] != 0) {
			throw new InvalidDataException ("Invalid CoreCLR bootstrap string pool or preload stride.");
		}
		string ReadString (uint offset, bool required)
		{
			if (offset == 0 && !required) return "";
			if (offset <= offsets [5] || offset >= raw.Length || raw [offset - 1] != 0) {
				throw new InvalidDataException ("Invalid CoreCLR bootstrap string offset.");
			}
			int end = Array.IndexOf (raw, (byte)0, (int)offset);
			if (end < 0 || required && end == offset) {
				throw new InvalidDataException ("Unterminated or empty CoreCLR bootstrap string.");
			}
			try {
				return Utf8.GetString (raw, (int)offset, end - (int)offset);
			} catch (DecoderFallbackException ex) {
				throw new InvalidDataException ("Invalid CoreCLR bootstrap UTF-8.", ex);
			}
		}
		ReadString (packageOffset, required: true);
		string [] reserved = ["HOST_RUNTIME_CONTRACT", "RUNTIME_IDENTIFIER", "APP_CONTEXT_BASE_DIRECTORY"];
		for (int i = 0; i < 3; i++) {
			for (uint j = 0; j < counts [i]; j++) {
				reader.BaseStream.Position = offsets [i] + j * entrySizes [i];
				string name = ReadString (reader.ReadUInt32 (), required: true);
				uint valueOffset = reader.ReadUInt32 ();
				if (i == 2 && j < 3) {
					if (name != reserved [j] || valueOffset != 0) {
						throw new InvalidDataException ("Invalid CoreCLR host property slots.");
					}
				} else {
					ReadString (valueOffset, required: false);
				}
			}
		}
		uint previousHash = 0;
		for (uint j = 0; j < counts [3]; j++) {
			reader.BaseStream.Position = offsets [3] + j * entrySizes [3];
			uint hash = reader.ReadUInt32 ();
			if (j != 0 && hash < previousHash) {
				throw new InvalidDataException ("CoreCLR DSO cache entries are not sorted.");
			}
			previousHash = hash;
			if (reader.ReadByte () > 1 || reader.ReadByte () > 1 || reader.ReadUInt16 () != 0) {
				throw new InvalidDataException ("Invalid CoreCLR DSO cache flags.");
			}
			ReadString (reader.ReadUInt32 (), required: true);
		}
		for (uint j = 0; j < counts [4]; j++) {
			reader.BaseStream.Position = offsets [4] + j * entrySizes [4];
			if (reader.ReadUInt32 () >= counts [3]) {
				throw new InvalidDataException ("Invalid CoreCLR JNI preload index.");
			}
		}
	}
}
