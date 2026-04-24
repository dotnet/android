using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using ILCompiler.Reflection.ReadyToRun;
using K4os.Compression.LZ4;

namespace ApplicationUtility;

using ReadyToRunOperatingSystem = ILCompiler.Reflection.ReadyToRun.OperatingSystem;

/// <summary>
/// Represents a .NET managed assembly contained within an Android application package or assembly store.
/// Supports both compressed (LZ4) and uncompressed PE assemblies.
/// </summary>
public class ApplicationAssembly : BaseAspect
{
	const string LogTag = "ApplicationAssembly";
	const uint COMPRESSED_MAGIC  = 0x5A4C4158; // 'XALZ', little-endian
	const ushort MSDOS_EXE_MAGIC = 0x5A4D;     // 'MZ'
	const uint PE_EXE_MAGIC      = 0x00004550; // 'PE\0\0'
	const uint CompressedHeaderSize = 3 * 4; // 3 32-bit words

	public override string AspectName { get; } = "Application assembly";

	public bool IsCompressed                      { get; }
	public string Name                            { get; }
	public string FullName                        { get; }
	public ulong CompressedSize                   { get; }
	public ulong Size                             { get; }
	public bool IgnoreOnLoad                      { get; }
	public ulong NameHash                         { get; internal set; }
	public NativeArchitecture Architecture        { get; internal set; }
	public bool IsSatellite                       { get; }
	public string? Culture                        { get; }
	public ApplicationAssemblyContainer Container { get; internal set; } = ApplicationAssemblyContainer.Standalone;
	public bool IsRTR                             { get; }
	public ReadyToRunOperatingSystem RTROS        { get; } = ReadyToRunOperatingSystem.Unknown;
	public Machine RTRMachine                     { get; } = Machine.Unknown;

	static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;

	ApplicationAssembly (Stream stream, uint uncompressedSize, string? description, bool isCompressed)
		: base (stream)
	{
		Size = uncompressedSize;
		CompressedSize = isCompressed ? (ulong)stream.Length : 0;
		IsCompressed = isCompressed;
		string name = NameMe (description);
		if (!name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
			name = $"{name}.dll";
		}

		FullName = name;

		MemoryStream? decompressedStream = null;
		try {
			(IsSatellite, Culture, Name) = DetectSatellite (stream, CompressedSize, Size, isCompressed, name, ref decompressedStream);
			(IsRTR, RTRMachine, RTROS) = DetectRTR (stream, CompressedSize, Size, isCompressed, ref decompressedStream);
		} finally {
			try {
				decompressedStream?.Dispose ();
			} catch (Exception ex) {
				Log.Debug ("Failed to dispose decompressed memory stream.", ex);
				// Ignore
			}
		}
	}

	ApplicationAssembly (string? description, bool isIgnored)
		: base (null)
	{
		IgnoreOnLoad = isIgnored;
		string name = NameMe (description);
		FullName = name;

		MemoryStream? decompressedStream = null;
		try {
			(IsSatellite, Culture, Name) = DetectSatellite (null, 0, 0, false, name, ref decompressedStream);
		} finally {
			try {
				decompressedStream?.Dispose ();
			} catch (Exception ex) {
				Log.Debug ("Failed to dispose decompressed memory stream.", ex);
				// Ignore
			}
		}
	}

	static string NameMe (string? description) => String.IsNullOrEmpty (description) ? "Unnamed" : description;

	static (bool isSatellite, string? culture, string name) DetectSatellite (
		Stream? aspectStream,
		ulong compressedSize,
		ulong size,
		bool isCompressed,
		string name,
		ref MemoryStream? decompressedStream
	)
	{
		Log.Debug ("Detecting if it is a satellite assembly.");

		// If we were passed full path to an actual file, detection of the culture based on its name is
		// hard (if possible), so we will just look at the metadata then.
		if (Path.IsPathRooted (name) && File.Exists (name)) {
			Log.Debug ("Deciding based on assembly metadata");
			try {
				return DetectSateliteFromMetadata (aspectStream, compressedSize, size, isCompressed, name, ref decompressedStream);
			} catch (Exception ex) {
				Log.Warning ($"Failed to detect culture of assembly '{name}'", ex);
				return (false, null, Path.GetFileName (name));
			}
		}

		Log.Debug ("Deciding based on assembly name");
		// Otherwise we can take advantage of the name, since the container (assembly store or application package
		// will give us either just the file name or one with the `culture/` prefix
		int idx = name.IndexOf ('/');
		bool isSatellite = idx > 0;
		if (!isSatellite || idx == name.Length - 1) {
			Log.Debug ("Not a satellite assembly");
			return (false, null, name);
		}

		string newName = name.Substring (idx + 1);
		string culture = name.Substring (0, idx);

		Log.Debug ($"Satellite assembly detected. Culture name: '{culture}'");
		return (true, culture, newName);
	}

	static PEReader OpenPEReaderForCompressedStream (Stream aspectStream, ulong compressedSize, ulong size, out MemoryStream decompressedStream)
	{
		decompressedStream = new ();
		DecompressTo (aspectStream, decompressedStream, compressedSize, size);
		decompressedStream.Seek (0, SeekOrigin.Begin);
		return new PEReader (decompressedStream, PEStreamOptions.LeaveOpen);
	}

	static (bool isSatellite, string? culture, string name) DetectSateliteFromMetadata (
		Stream? aspectStream,
		ulong compressedSize,
		ulong size,
		bool isCompressed,
		string path,
		ref MemoryStream? decompressedStream
	)
	{
		if (aspectStream == null) {
			throw new ArgumentNullException (nameof (aspectStream));
		}

		decompressedStream = null;
		using PEReader peReader = isCompressed switch {
			true  => OpenPEReaderForCompressedStream (aspectStream, compressedSize, size, out decompressedStream),
			false => new PEReader (File.OpenRead (path))
		};

		var mdataReader = peReader.GetMetadataReader ();

		AssemblyDefinition asmdef = mdataReader.GetAssemblyDefinition ();
		AssemblyNameInfo nameInfo = asmdef.GetAssemblyNameInfo ();
		string fileName = Path.GetFileName (path);
		if (String.IsNullOrEmpty (nameInfo.CultureName)) {
			Log.Debug ("Not a satellite assembly");
			return (false, null, fileName);
		}

		Log.Debug ($"Satellite assembly detected. Culture name: '{nameInfo.CultureName}'");
		return (true, nameInfo.CultureName, fileName);
	}

	static (bool isR2R, Machine machine, ReadyToRunOperatingSystem os) DetectRTR (
		Stream? aspectStream,
		ulong compressedSize,
		ulong size,
		bool isCompressed,
		ref MemoryStream? decompressedStream
	)
	{
		if (aspectStream == null) {
			throw new ArgumentNullException (nameof (aspectStream));
		}

		Log.Debug ("Detecting if it is a ReadyToRun assembly");
		PEReader? peReader = null;
		try {
			if (isCompressed) {
				if (decompressedStream != null) {
					decompressedStream.Seek (0, SeekOrigin.Begin);
					peReader = new PEReader (aspectStream, PEStreamOptions.LeaveOpen);
				} else {
					peReader = OpenPEReaderForCompressedStream (aspectStream, compressedSize, size, out decompressedStream);
				}
			} else {
				aspectStream.Seek (0, SeekOrigin.Begin);
				peReader = new PEReader (aspectStream, PEStreamOptions.LeaveOpen);
			};

			CorHeader? corHeader = peReader?.PEHeaders?.CorHeader;
			if (peReader == null || corHeader == null) {
				return ThisIsNotTheRtrYouAreLookingFor ();
			}

			if ((corHeader.Flags & CorFlags.ILLibrary) == CorFlags.ILLibrary) {
				return DetectCompositeRTR (peReader);
			}

			return DetectLegacyRTR (peReader);
		} catch (Exception ex) {
			Log.Warning ("Failed to detect whether assembly is a ReadyToRun image.", ex);
			return ThisIsNotTheRtrYouAreLookingFor ();
		} finally {
			try {
				peReader?.Dispose ();
			} catch (Exception ex) {
				Log.Debug ("Failed to dispose of a PE reader stream.", ex);
				// Ignore
			}
		}
	}

	static (bool isR2R, Machine machine, ReadyToRunOperatingSystem os) DetectCompositeRTR (PEReader peReader)
	{
		var compositeReader = new PEImageReader (peReader);
		IAssemblyMetadata? metadata = compositeReader.GetStandaloneAssemblyMetadata ();
		bool ret = compositeReader.TryGetReadyToRunHeader (out _, out _);
		if (!ret) {
			return ThisIsNotTheRtrYouAreLookingFor ();
		}

		Log.Debug ("ReadyToRun assembly detected.");
		return (true, compositeReader.Machine, compositeReader.OperatingSystem);
	}

	static (bool isR2R, Machine machine, ReadyToRunOperatingSystem os) DetectLegacyRTR (PEReader peReader)
	{
		// TODO: implement
		return ThisIsNotTheRtrYouAreLookingFor (log: false);
	}

	static (bool isR2R, Machine machine, ReadyToRunOperatingSystem os) ThisIsNotTheRtrYouAreLookingFor (bool log = true)
	{
		if (log) {
			Log.Debug ("Not a ReadyToRun assembly.");
		}
		return (false, Machine.Unknown, ReadyToRunOperatingSystem.Unknown);
	}

	/// <summary>
	/// Creates a special ignored-on-load assembly instance for entries that exist in the store index but have no data.
	/// </summary>
	/// <param name="description">Assembly name or description.</param>
	/// <param name="nameHash">The xxHash of the assembly name.</param>
	/// <param name="arch">The target native architecture.</param>
	/// <returns>A new <see cref="ApplicationAssembly"/> marked as ignored.</returns>
	public static IAspect CreateIgnoredAssembly (string? description, ulong nameHash, NativeArchitecture arch, ApplicationAssemblyContainer container)
	{
		// This is a special case, as much as I hate to have one. Ignored assemblies exist only in the assembly store's
		// index. They have an associated descriptor, but no data whatsoever. For that reason, we can't go the `ProbeAspect`
		// + `LoadAspect` route, so `AssemblyStore` will call this method for them.

		Log.Debug ($"{LogTag}: stream ('{description}') is an ignored assembly.");
		return new ApplicationAssembly (description, isIgnored: true) {
			Architecture = arch,
			NameHash = nameHash,
			Container = container,
		};
	}

	/// <summary>
	/// Loads an assembly aspect from the given stream and probe state.
	/// </summary>
	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		Log.Debug ($"Loading assembly from stream '{description}'");
		using var reader = Utilities.GetReaderAndRewindStream (stream);
		if (ReadCompressedHeader (reader, out uint uncompressedLength)) {
			return new ApplicationAssembly (stream, uncompressedLength, description, isCompressed: true);
		}

		return new ApplicationAssembly (stream, (uint)stream.Length, description, isCompressed: false);
	}

	/// <summary>
	/// Probes the stream to determine whether it contains a valid .NET assembly (compressed or PE).
	/// </summary>
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
	/// Writes the assembly data to the given stream, optionally decompressing LZ4-compressed data.
	/// </summary>
	/// <param name="stream">The destination stream.</param>
	/// <param name="decompress">If <c>true</c>, decompress the assembly data before writing.</param>
	/// <returns><c>true</c> if the write succeeded.</returns>
	public bool WriteToStream (Stream stream, bool decompress)
	{
		Log.Debug ($"Writing assembly '{Name}' to stream");
		if (decompress && IsCompressed) {
			return DecompressTo (AspectStream, stream, CompressedSize, Size);
		}

		Log.Debug ($"Assembly is not compressed, copying {Utilities.SizeToString (Size)} bytes of data to stream verbatim.");
		AspectStream.Seek (0, SeekOrigin.Begin);
		AspectStream.CopyTo (stream);
		stream.Flush ();
		return true;
	}

	static bool DecompressTo (Stream aspectStream, Stream destStream, ulong compressedSize, ulong size)
	{
		Log.Debug ($"Assembly is compressed. Decompressing {compressedSize - CompressedHeaderSize} bytes to {size} bytes (as per compression header info).");
		using var reader = Utilities.GetReaderAndRewindStream (aspectStream);
		if (!ReadCompressedHeader (reader, out uint uncompressedLength)) {
			Log.Error ($"Stream doesn't have the required compressed assembly header, or the header is invalid.");
			return false;
		}

		int inputLength = (int)aspectStream.Length - (int)CompressedHeaderSize;
		Log.Debug ($"Input data length: {inputLength}");
		byte[] inputData = bytePool.Rent (inputLength);
		byte[] assemblyData = bytePool.Rent ((int)size); // Let it throw if there's an integer overflow...

		Log.Debug ("Starting decompression...");
		var watch = new Stopwatch ();
		try {

			watch.Start ();

			reader.Read (inputData, 0, inputLength);
			int decoded = LZ4Codec.Decode (inputData, 0, inputLength, assemblyData, 0, (int)size);
			if (decoded != (int)size) {
				Log.Error ($"Failed to decompress input stream data. Decoded {decoded} bytes, expected {size}");
				return false;
			}
			destStream.Write (assemblyData, 0, decoded);
			destStream.Flush ();
		} finally {
			bytePool.Return (inputData);
			bytePool.Return (assemblyData);

			watch.Stop ();
			Log.Debug ($"Decompression done in {watch.Elapsed}");
		}

		return true;
	}

	// We don't care about the descriptor index here, it's only needed during the run time
	static bool ReadCompressedHeader (BinaryReader reader, out uint uncompressedLength)
	{
		uncompressedLength = 0;

		if (reader.BaseStream.Length < (int)CompressedHeaderSize) {
			Log.Debug ($"Not enough data in input stream to read the compressed header. Need at least {CompressedHeaderSize} bytes, found {reader.BaseStream.Length}");
			return false;
		}

		uint uintVal = reader.ReadUInt32 ();
		if (uintVal != COMPRESSED_MAGIC) {
			Log.Debug ("Input stream doesn't have the compression header.");
			return false;
		}

		uintVal = reader.ReadUInt32 (); // descriptor index
		uncompressedLength = reader.ReadUInt32 ();
		return true;
	}
}
