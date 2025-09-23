using System;
using System.IO;
using System.Text;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace ApplicationUtility;

class SharedLibrary : IAspect, IDisposable
{
	const uint ELF_MAGIC = 0x464c457f;

	readonly ulong payloadOffset;
	readonly ulong payloadSize;
	readonly string libraryName;
	readonly bool is64Bit;
	readonly Stream libraryStream;

	IELF elf;
	bool disposed;
	NativeArchitecture nativeArch = NativeArchitecture.Unknown;

	public static string AspectName { get; } = "Native shared library";

	public bool HasAndroidPayload => payloadSize > 0;
	public string Name => libraryName;
	public NativeArchitecture TargetArchitecture => nativeArch;
	public bool Is64Bit => is64Bit;

	protected IELF ELF => elf;

	protected SharedLibrary (Stream stream, string libraryName)
	{
		this.libraryStream = stream;
		this.libraryName = libraryName;
		(elf, is64Bit, nativeArch) = LoadELF (stream, libraryName);
		(payloadOffset, payloadSize) = FindAndroidPayload (elf);
	}

	public static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		if (!IsSupportedELFSharedLibrary (stream, description)) {
			throw new InvalidOperationException ("Stream is not a supported ELF shared library");
		}

		return new SharedLibrary (stream, description);
	}

	public static IAspectState ProbeAspect (Stream stream, string? description) => new BasicAspectState (IsSupportedELFSharedLibrary (stream, description));

	/// <summary>
	/// If the library has .NET for Android payload section, this
	/// method will read the data and write it to the <paramref name="dest"/>
	/// stream. All the data in the output stream will be overwritten.
	/// </summary>
	public void CopyAndroidPayload (Stream dest)
	{
		using Stream payload = OpenAndroidPayload ();
		payload.CopyTo (dest);
	}

	/// <summary>
	/// Creates a stream referring to the Android payload data inside
	/// the shared library. No data is read, the open stream is returned
	/// to the user. Ownership of the stream is transferred to the caller.
	/// </summary>
	public Stream OpenAndroidPayload ()
	{
		if (!HasAndroidPayload) {
			throw new InvalidOperationException ("Payload section not found");
		}

		if (payloadOffset > Int64.MaxValue) {
			throw new InvalidOperationException ($"Payload offset of {payloadOffset} is too large to support.");
		}

		if (payloadSize > Int64.MaxValue) {
			throw new InvalidOperationException ($"Payload offset of {payloadSize} is too large to support.");
		}

		return new SubStream (libraryStream, (long)payloadOffset, (long)payloadSize);
	}

	protected static bool IsSupportedELFSharedLibrary (Stream stream, string? description, out IELF? elf)
	{
		elf = null;

		if (stream.Length < 4) { // Less than that and we know there isn't room for ELF magic
			Log.Debug ($"SharedLibrary: stream ('{description}') is too short to be an ELF image.");
			return false;
		}
		stream.Seek (0, SeekOrigin.Begin);

		using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
		uint magic = reader.ReadUInt32 ();
		if (magic != ELF_MAGIC) {
			Log.Debug ($"SharedLibrary: stream ('{description}') is not an ELF image.");
			return false;
		}
		stream.Seek (0, SeekOrigin.Begin);

		Class elfClass = ELFReader.CheckELFType (stream);
		if (elfClass == Class.NotELF) {
			Log.Debug ($"SharedLibrary: stream ('{description}') is not a supported ELF class.");
			return false;
		}

		if (!ELFReader.TryLoad (stream, shouldOwnStream: false, out elf) || elf == null) {
			Log.Debug ($"SharedLibrary: stream ('{description}') failed to load as an ELF image while checking support.");
			return false;
		}

		if (elf.Type != FileType.SharedObject) {
			Log.Debug ($"SharedLibrary: stream ('{description}') is not an ELF shared library image.");
			return false;
		}

		if (elf.Endianess != ELFSharp.Endianess.LittleEndian) {
			Log.Debug ($"SharedLibrary: stream ('{description}') is not a little-endian ELF image.");
			return false;
		}

		bool supported = elf.Machine switch {
			Machine.ARM      => true,
			Machine.Intel386 => true,
			Machine.AArch64  => true,
			Machine.AMD64    => true,
			_                => false
		};

		string not = supported ? String.Empty : " not";
		Log.Debug ($"SharedLibrary: stream ('{description}') is{not} a supported ELF architecture ('{elf.Machine}')");

		return supported;
	}

	protected static bool IsSupportedELFSharedLibrary (Stream stream, string? description)
	{
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return false;
		}

		elf.Dispose ();
		return true;
	}

	// We assume below that stream corresponds to a valid and supported by us ELF image. This should have been asserted by
	// the `LoadAspect` method.
	(IELF elf, bool is64bit, NativeArchitecture nativeArch) LoadELF (Stream stream, string? libraryName)
	{
		stream.Seek (0, SeekOrigin.Begin);
		if (!ELFReader.TryLoad (stream, shouldOwnStream: false, out IELF? elf) || elf == null) {
			Log.Debug ($"SharedLibrary: stream ('{libraryName}') failed to load as an ELF image.");
			throw new InvalidOperationException ($"Failed to load ELF library '{libraryName}'.");
		}

		(bool is64, NativeArchitecture arch) = elf.Machine switch {
			Machine.ARM      => (false, NativeArchitecture.Arm),
			Machine.Intel386 => (false, NativeArchitecture.X86),
			Machine.AArch64  => (true, NativeArchitecture.Arm64),
			Machine.AMD64    => (true, NativeArchitecture.X64),
			_                => throw new NotSupportedException ($"Unsupported ELF architecture '{elf.Machine}'")
		};

		return (elf, is64, arch);
	}

	(ulong offset, ulong size) FindAndroidPayload (IELF elf)
	{
		if (!elf.TryGetSection ("payload", out ISection? payloadSection)) {
			Log.Debug ($"SharedLibrary: shared library '{libraryName}' doesn't have the 'payload' section.");
			return (0, 0);
		}

		ulong offset;
		ulong size;

		if (is64Bit) {
			(offset, size) = GetOffsetAndSize64 ((Section<ulong>)payloadSection);
		} else {
			(offset, size) = GetOffsetAndSize32 ((Section<uint>)payloadSection);
		}

		Log.Debug ($"SharedLibrary: found payload section at offset {offset}, size of {size} bytes.");
		return (offset, size);

		(ulong offset, ulong size) GetOffsetAndSize64 (Section<ulong> payload)
		{
			return (payload.Offset, payload.Size);
		}

		(ulong offset, ulong size) GetOffsetAndSize32 (Section<uint> payload)
		{
			return ((ulong)payload.Offset, (ulong)payload.Size);
		}
	}

	public bool HasSection (string name, SectionType type = SectionType.Null)
	{
		return HasSection (elf, libraryName, name, type);
	}

	protected static bool HasSection (IELF elf, string libraryName, string sectionName, SectionType type = SectionType.Null)
	{
		Log.Debug ($"Checking for section '{sectionName}' with type {type} in library '{libraryName}'");
		if (!elf.TryGetSection (sectionName, out ISection? section)) {
			Log.Debug ("Section not found");
			return false;
		}

		if (type == SectionType.Null) {
			Log.Debug ("Section present, type check not requested");
			return true;
		}

		Log.Debug ($"Section present, type {section.Type}");
		return section.Type == type;
	}

	protected virtual void Dispose (bool disposing)
	{
		if (disposed) {
			return;
		}

		if (disposing) {
			elf?.Dispose ();
		}

		disposed = true;
	}

	public void Dispose ()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose (disposing: true);
		GC.SuppressFinalize (this);
	}
}
