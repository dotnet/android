using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ApplicationUtility;

/// <summary>
/// Represents a Portable PDB (Program Database) file associated with a .NET assembly.
/// Detects PDB files by verifying the ECMA-335 metadata signature and the <c>#Pdb</c> stream.
/// </summary>
public class AssemblyPdb : BaseAspect
{
	const string LogTag = "PPDB";
	const uint Magic =  0x424A5342;

	public override string AspectName => "Assembly Portable PDB data";

	public NativeArchitecture Architecture { get; internal set; }
	public string Name { get; }
	public ulong Size  { get; }

	protected AssemblyPdb (Stream? aspectStream, string pdbName)
		: base (aspectStream)
	{
		Name = pdbName;
		Size = (ulong)(aspectStream?.Length ?? 0);
	}

	/// <summary>
	/// Loads a PDB aspect from the given stream.
	/// </summary>
	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		string desc = EnsureValidName (description);
		Log.Debug ($"Loading PDB data from stream '{desc}'");
		return new AssemblyPdb (stream, desc);
	}

	/// <summary>
	/// Probes the stream to determine whether it contains a valid Portable PDB file.
	/// </summary>
	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		string desc = EnsureValidName (description);
		Log.Debug ($"{LogTag}: probing stream ('{desc}')");

		// This is file header size + stream header size, taking into account only statically
		// sized fields. This is the absolute minimum with empty names in those headers.
		const long MinimumHeadersLength = 20 + 8;

		if (stream.Length < MinimumHeadersLength) {
			Log.Debug ($"{LogTag}: stream isn't long enough. Needed at least {MinimumHeadersLength} bytes, found {stream.Length}");
			return GetErrorState ();
		}

		// Portable PDB file format follows the definition in ECMA-335, partition II chapter 24.
		// File and stream headers are described in chapters 24.2.1 and 24.2.2, respectively.
		//
		//   https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf
		//
		// We will read the file header and enough stream headers to either find the `#Pdb` stream
		// or run out of stream headers. We don't care about the actual PDB data in the file, just
		// the ECMA headers, for detection purpose.
		using var reader = Utilities.GetReaderAndRewindStream (stream);

		// Signature: 4 bytes
		uint magic = reader.ReadUInt32 ();
		if (magic != Magic) {
			Log.Debug ($"{LogTag}: invalid magic signature for a PDB file.");
			return GetErrorState ();
		}

		// MajorVersion and MinorVersion, each 2 bytes
		ushort majorVersion = reader.ReadUInt16 ();
		ushort minorVersion = reader.ReadUInt16 ();
		Log.Debug ($"{LogTag}: file version {majorVersion}.{minorVersion}");

		// Reserved: 4 bytes
		reader.ReadUInt32 ();

		// Version string length: 4 bytes
		uint vsLength = reader.ReadUInt32 ();

		// Version string. `vsLength` bytes, UTF-8, length includes padding to the 4-byte boundary
		byte[] versionBytes = reader.ReadBytes ((int)vsLength);
		int nullIdx = Array.IndexOf (versionBytes, (byte)0);
		if (nullIdx < 0) {
			nullIdx = versionBytes.Length - 1;
		}
		string version = Encoding.UTF8.GetString (versionBytes, 0, nullIdx);
		Log.Debug ($"{LogTag}: format version '{version}'");

		// Flags: 2 bytes
		reader.ReadUInt16 ();

		// Number of streams: 2 bytes
		ushort nStreams = reader.ReadUInt16 ();
		if (nStreams == 0) {
			Log.Debug ($"{LogTag}: no streams headers reported.");
			return GetErrorState ();
		}

		bool pdbStreamFound = false;
		var streams = new List<AssemblyPdbAspectState.StreamInfo> ();
		for (ushort i = 0; i < nStreams; i++) {
			AssemblyPdbAspectState.StreamInfo info = ReadNextStreamName (i);
			if (!pdbStreamFound && info.Name == "#Pdb") {
				pdbStreamFound = true;
			}
			streams.Add (info);
		}

		if (pdbStreamFound) {
			Log.Debug ($"{LogTag}: stream detected as a valid Portable PDB file.");
			return new AssemblyPdbAspectState (version, majorVersion, minorVersion, streams);
		}

		Log.Debug ($"{LogTag}: stream is not a valid Portable PDB file.");
		return GetErrorState ();

		IAspectState GetErrorState () => new AssemblyPdbAspectState (false);

		AssemblyPdbAspectState.StreamInfo ReadNextStreamName (ushort idx)
		{
			// Stream headers are variable-length structures. Two statically sized fields are
			// followed by the ASCIIZ stream name, padded to the next 4-byte boundary with NUL
			// characters.

			// Offset: 4 bytes
			reader.ReadUInt32 ();

			// Size: 4 bytes
			uint size = reader.ReadUInt32 ();

			// Stream name is maximum 32-bytes
			var sb = new StringBuilder ();
			int nread = 0;
			for (int i = 0; i < 32; i++) {
				byte b = reader.ReadByte ();
				nread++;
				if (b == 0) {
					break;
				}
				sb.Append ((char)b);
			}

			if (nread % 4 != 0) {
				reader.BaseStream.Seek (4 - (nread % 4), SeekOrigin.Current);
			}
			string ret = sb.ToString ();
			Log.Debug ($"{LogTag}: stream {idx} name == '{ret}'; size {size} bytes");

			return new (ret, size);
		}
	}

	static string EnsureValidName (string? description)
	{
		if (description == null) {
			throw new ArgumentNullException (nameof (description));
		}

		if (description.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
			description = Path.ChangeExtension (description, ".pdb");
		}

		return description;
	}

	/// <summary>
	/// Writes the PDB data to the given stream.
	/// </summary>
	public bool WriteToStream (Stream stream)
	{
		AspectStream.Seek (0, SeekOrigin.Begin);
		AspectStream.CopyTo (stream);
		stream.Flush ();
		return true;
	}
}
