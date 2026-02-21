using System;
using System.IO;

namespace ApplicationUtility;

public class ApplicationAssembly : IAspect
{
	const string LogTag = "ApplicationAssembly";
	const uint COMPRESSED_MAGIC = 0x5A4C4158; // 'XALZ', little-endian
	const ushort MSDOS_EXE_MAGIC = 0x5A4D; // 'MZ'
	const uint PE_EXE_MAGIC = 0x00004550; // 'PE\0\0'

	public static string AspectName { get; } = "Application assembly";

	public bool IsCompressed    { get; }
	public string Name          { get; }
	public ulong CompressedSize { get; }
	public ulong Size           { get; }
	public bool IgnoreOnLoad    { get; }
	public ulong NameHash       { get; internal set; }

	readonly Stream? assemblyStream;

	ApplicationAssembly (Stream stream, uint uncompressedSize, string? description, bool isCompressed)
	{
		assemblyStream = stream;
		Size = uncompressedSize;
		CompressedSize = isCompressed ? (ulong)stream.Length : 0;
		IsCompressed = isCompressed;
		Name = NameMe (description);
	}

	ApplicationAssembly (string? description, bool isIgnored)
	{
		IgnoreOnLoad = isIgnored;
		Name = NameMe (description);
	}

	static string NameMe (string? description) => String.IsNullOrEmpty (description) ? "Unnamed" : description;

	// This is a special case, as much as I hate to have one. Ignored assemblies exist only in the assembly store's
	// index. They have an associated descriptor, but no data whatsoever. For that reason, we can't go the `ProbeAspect`
	// + `LoadAspect` route, so `AssemblyStore` will call this method for them.
	public static IAspect CreateIgnoredAssembly (string? description, ulong nameHash)
	{
		Log.Debug ($"{LogTag}: stream ('{description}') is an ignored assembly.");
		return new ApplicationAssembly (description, isIgnored: true) {
			NameHash = nameHash,
		};
	}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		using var reader = Utilities.GetReaderAndRewindStream (stream);
		if (ReadCompressedHeader (reader, out uint uncompressedLength)) {
			return new ApplicationAssembly (stream, uncompressedLength, description, isCompressed: true);
		}

		return new ApplicationAssembly (stream, (uint)stream.Length, description, isCompressed: false);
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		Log.Debug ($"{LogTag}: probing stream ('{description}')");
		if (stream.Length == 0) {
			// It can happen if the assembly store index or name table are corrupted and we cannot
			// determine if an assembly is ignored or not. If it is ignored, it will have no data
			// available and so the stream will have length of 0
			return new BasicAspectState (false);
		}

		// If we detect compressed assembly signature, we won't proceed with checking whether
		// the rest of data is actually a valid managed assembly. This is to avoid doing a
		// costly operation of decompressing when e.g. loading data from an assemblystore, when
		// we potentially create a lot of `ApplicationAssembly` instances. Presence of the compression
		// header is enough for the probing stage.

		using var reader = Utilities.GetReaderAndRewindStream (stream);
		if (ReadCompressedHeader (reader, out _)) {
			Log.Debug ($"{LogTag}: stream ('{description}') is a compressed assembly.");
			return new BasicAspectState (true);
		}

		// We could use PEReader (https://learn.microsoft.com/en-us/dotnet/api/system.reflection.portableexecutable.pereader)
		// but it would be too heavy for our purpose here.
		reader.BaseStream.Seek (0, SeekOrigin.Begin);
		ushort mzExeMagic = reader.ReadUInt16 ();
		if (mzExeMagic != MSDOS_EXE_MAGIC) {
			return Utilities.GetFailureAspectState ($"{LogTag}: stream doesn't have MS-DOS executable signature.");
		}

		const long PE_HEADER_OFFSET = 0x3c;
		if (reader.BaseStream.Length <= PE_HEADER_OFFSET) {
			return Utilities.GetFailureAspectState ($"{LogTag}: stream contains a corrupted MS-DOS executable image (too short, offset {PE_HEADER_OFFSET} is bigger than stream size).");
		}

		// Offset at 0x3C is where we can read the 32-bit offset to the PE header
		reader.BaseStream.Seek (PE_HEADER_OFFSET, SeekOrigin.Begin);
		uint uintVal = reader.ReadUInt32 ();
		if (reader.BaseStream.Length <= (long)uintVal) {
			return Utilities.GetFailureAspectState ($"{LogTag}: stream contains a corrupted PE executable image (too short, offset {uintVal} is bigger than stream size).");
		}

		reader.BaseStream.Seek ((long)uintVal, SeekOrigin.Begin);
		uintVal = reader.ReadUInt32 ();
		if (uintVal != PE_EXE_MAGIC) {
			return Utilities.GetFailureAspectState ($"{LogTag}: stream doesn't have PE executable signature.");
		}
		// This is good enough for us

		Log.Debug ($"{LogTag}: stream ('{description}') appears to be a PE image.");
		return new BasicAspectState (true);
	}

	/// <summary>
	/// Writes assembly data to the indicated file, uncompressing it if necessary. If the destination
	/// file exists, it will be overwritten.
	/// </summary>
	public void SaveToFile (string filePath)
	{
		throw new NotImplementedException ();
	}

	// We don't care about the descriptor index here, it's only needed during the run time
	static bool ReadCompressedHeader (BinaryReader reader, out uint uncompressedLength)
	{
		uncompressedLength = 0;

		uint uintVal = reader.ReadUInt32 ();
		if (uintVal != COMPRESSED_MAGIC) {
			return false;
		}

		uintVal = reader.ReadUInt32 (); // descriptor index
		uncompressedLength = reader.ReadUInt32 ();
		return true;
	}
}
