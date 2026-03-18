using System;
using System.IO;
using System.Text;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;

namespace ApplicationUtility;

/// <summary>
/// Represents an ELF shared library (.so file). Extracts metadata such as architecture,
/// alignment, debug info, build ID, Android ident, and SONAME.
/// </summary>
public class SharedLibrary : BaseAspect
{
	const uint ELF_MAGIC = 0x464c457f;
	const string DebugLinkSectionName = ".gnu_debuglink";

	readonly string? androidIdent;
	readonly string? buildId;
	readonly string? debugLink;
	readonly IELF elf;
	readonly bool hasDebugInfo;
	readonly bool is64Bit;
	readonly ulong libraryAlignment;
	readonly string libraryName;
	readonly NativeArchitecture nativeArch = NativeArchitecture.Unknown;
	readonly string? soname;

	public override string AspectName { get; } = "Native shared library";

	public ulong Alignment => libraryAlignment;
	public bool AlignmentCompatibleWith16k => libraryAlignment >= 0x4000 && (libraryAlignment % 0x4000 == 0);
	public string? AndroidIdent => androidIdent;
	public string? BuildID => buildId;
	public string? DebugLink => debugLink;
	public bool HasAndroidIdent => !String.IsNullOrEmpty (androidIdent);
	public bool HasBuildID => !String.IsNullOrEmpty (buildId);
	public bool HasDebugInfo => hasDebugInfo;
	public bool HasDebugLink => !String.IsNullOrEmpty (debugLink);
	public bool HasSoname => !String.IsNullOrEmpty (soname);
	public bool Is64Bit => is64Bit;
	public string Name => libraryName;
	public string? Soname => soname;
	public ulong Size => AspectStream.Length >= 0 ? (ulong)AspectStream.Length : 0;

	public NativeArchitecture TargetArchitecture => nativeArch;

	protected IELF ELF => elf;

	protected SharedLibrary (Stream stream, string libraryName, IAspectState state)
		: base (stream)
	{
		var libState = EnsureValidAspectState<SharedLibraryAspectState> (state);

		this.libraryName = libraryName;
		elf = libState.ElfImage!;
		(is64Bit, nativeArch) = ValidateELF (libState.ElfImage!, libraryName);
		libraryAlignment = DetectAlignment (elf, is64Bit);
		(hasDebugInfo, debugLink) = DetectDebugInfo (elf, libraryName);
		buildId = GetBuildID (elf, is64Bit);
		androidIdent = GetAndroidIdent (elf, is64Bit);
		soname = GetSoname (elf, is64Bit);
	}

	protected static T EnsureValidAspectState<T> (IAspectState? state) where T: IAspectState
	{
		if (!(state is SharedLibraryAspectState libState)) {
			throw new InvalidOperationException ("Internal error: invalid aspect state, call ProbeAspect to get one.");
		}

		if (!libState.Success || libState.ElfImage == null) {
			throw new InvalidOperationException ("Internal error: ProbeAspect failed to detect a valid shared library.");
		}

		try {
			return (T)((object)libState);
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Internal error: aspect should be of type '{typeof(T)}', found '{libState.GetType ()}' instead", ex);
		}
	}

	public static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		LogLoadAspectStart (typeof(SharedLibrary));
		try {
			if (String.IsNullOrEmpty (description)) {
				throw new ArgumentException ("Must be a shared library name", nameof (description));
			}

			var libState = EnsureValidAspectState<SharedLibraryAspectState> (state);

			return new SharedLibrary (stream, description, libState);
		} finally {
			LogLoadAspectEnd ();
		}
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		LogProbeAspectStart (typeof(SharedLibrary));
		try {
			return IsSupportedELFSharedLibrary (stream, description);
		} finally {
			LogProbeAspectEnd ();
		}
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

	protected static IAspectState IsSupportedELFSharedLibrary (Stream stream, string? description)
	{
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return new SharedLibraryAspectState (false, null);
		}

		return new SharedLibraryAspectState (true, elf);
	}

	(bool is64bit, NativeArchitecture nativeArch) ValidateELF (IELF elf, string? libraryName)
	{
		(bool is64, NativeArchitecture arch) = elf.Machine switch {
			Machine.ARM      => (false, NativeArchitecture.Arm),
			Machine.Intel386 => (false, NativeArchitecture.X86),
			Machine.AArch64  => (true, NativeArchitecture.Arm64),
			Machine.AMD64    => (true, NativeArchitecture.X64),
			_                => throw new NotSupportedException ($"Unsupported ELF architecture '{elf.Machine}' for '{libraryName}'")
		};

		return (is64, arch);
	}

	static (bool hasDebugInfo, string? debugLink) DetectDebugInfo (IELF elf, string libraryName)
	{
		bool hasDebugInfo = HasSection (elf, libraryName, ".debug_info", SectionType.ProgBits);

		if (!HasSection (elf, libraryName, DebugLinkSectionName, SectionType.ProgBits)) {
			return (hasDebugInfo, null);
		}

		return (hasDebugInfo, ReadDebugLinkSection (elf, libraryName));
	}

	static string? ReadDebugLinkSection (IELF elf, string libraryName)
	{
		Log.Debug ($"Trying to load debug link section from {libraryName}");
		if (!elf.TryGetSection (DebugLinkSectionName, out ISection? section)) {
			Log.Debug ($"Debug link section '{DebugLinkSectionName}' could not be read");
			return null;
		}

		// From https://sourceware.org/gdb/current/onlinedocs/gdb.html/Separate-Debug-Files.html
		//
		//  * A filename, with any leading directory components removed, followed by a zero byte,
		//  * zero to three bytes of padding, as needed to reach the next four-byte boundary within the section, and
		//  * a four-byte CRC checksum, stored in the same endianness used for the executable file itself. The checksum is computed on the debugging information file’s full
		//    contents by the function given below, passing zero as the crc argument.

		// TODO: At this point we ignore the CRC, perhaps it would be worth reading it?
		byte[] contents = section.GetContents ();
		if (contents.Length < 4) {
			// there must at least be the 4-byte CRC, otherwise section content is invalid
			return null;
		}

		int zeroIndex = 0;
		for (int i = 0; i < contents.Length; i++) {
			if (contents[i] != 0) {
				continue;
			}

			zeroIndex = i;
			break;
		}

		if (zeroIndex == 0) {
			return null;
		}

		return Encoding.UTF8.GetString (contents, 0, zeroIndex);
	}

	static ulong DetectAlignment (IELF elf, bool is64Bit)
	{
		if (!elf.HasSegmentHeader) {
			return 0;
		}

		foreach (ISegment segment in elf.Segments) {
			if (segment.Type != SegmentType.Load) {
				continue;
			}

			if (is64Bit) {
				return ((Segment<ulong>)segment).Alignment;
			}

			return ((Segment<uint>)segment).Alignment;
		}

		return 0;
	}

	static string? GetSoname (IELF elf, bool is64Bit)
	{
		IDynamicEntry? sonameEntry = null;

		foreach (IDynamicSection section in elf.GetSections<IDynamicSection> ()) {
			foreach (IDynamicEntry dyne in section.Entries) {
				if (dyne.Tag != DynamicTag.SoName) {
					continue;
				}
				sonameEntry = dyne;
				break;
			}

			if (sonameEntry != null) {
				break;
			}
		}

		if (sonameEntry == null) {
			return null;
		}

		ulong stringIndex = is64Bit switch {
			true => ((DynamicEntry<ulong>)sonameEntry).Value,
			false => ((DynamicEntry<uint>)sonameEntry).Value
		};

		// Offset is into the .dynstr section
		if (!elf.TryGetSection (".dynstr", out ISection? strtabSection) || strtabSection == null || strtabSection.Type != SectionType.StringTable) {
			return null;
		}

		var strtab = (IStringTable)strtabSection;
		try {
			return strtab[(long)stringIndex];
		} catch (Exception ex) {
			Log.Debug ($"Failed to obtain soname from the string table (asked for index {stringIndex})", ex);
			return null;
		}
	}

	static string? GetBuildID (IELF elf, bool is64Bit)
	{
		// From elf_common.h
		//
		//  #define NT_GNU_BUILD_ID     3
		//
		const ulong NT_GNU_BUILD_ID = 0;
		byte[]? contents = GetNoteSectionContents (elf, is64Bit, ".note.gnu.build-id", NT_GNU_BUILD_ID);
		if (contents == null) {
			return null;
		}

		var sb = new StringBuilder ();
		foreach (byte b in contents) {
			sb.Append ($"{b:x02}");
		}

		return sb.ToString ();
	}

	static string? GetAndroidIdent (IELF elf, bool is64Bit)
	{
		INoteSection? note = GetNoteSection (elf, is64Bit, ".note.android.ident");
		if (note == null || note.NoteName != "Android") {
			return null;
		}

		byte[]? bytes = note.Description;

		// Descriptor has fixed size:
		//
		//   platform SDK version: int32
		//   NDK version: 64 chars
		//   NDK build: 64 chars
		const int NdkVersionLength = 64;
		const int NdkBuildNumberLength = 64;
		const int MinDescLength = 4 + NdkVersionLength + NdkBuildNumberLength;
		if (bytes == null || bytes.Length < MinDescLength) {
			return null;
		}

		using var contents = new MemoryStream (bytes);
		using var reader = new BinaryReader (contents);

		// .net.android.indent format: https://android.googlesource.com/platform/ndk/+/ndk-release-r16/sources/crt/crtbrand.S#39
		int platformSdkVersion = reader.ReadInt32 ();
		string abiNdkVersion = GetASCIIZ (NdkVersionLength);
		string abiNdkBuildNumber = GetASCIIZ (NdkBuildNumberLength);

		return $"Platform SDK: {platformSdkVersion}; NDK version: {abiNdkVersion}, build {abiNdkBuildNumber}";

		string GetASCIIZ (int len)
		{
			byte[] data = reader.ReadBytes (len);
			int zeroIdx = -1;

			for (int i = 0; i < data.Length; i++) {
				if (data[i] == 0) {
					zeroIdx = i;
					break;
				}
			}

			if (zeroIdx <= 0) {
				return "<none>";
			}

			return Encoding.ASCII.GetString (data, 0, zeroIdx);
		}
	}

	static string? GetNoteSectionContentsAsString (IELF elf, bool is64Bit, string sectionName, ulong noteType = 0)
	{
		byte[]? contents = GetNoteSectionContents (elf, is64Bit, sectionName);
		if (contents == null) {
			return null;
		}

		return Encoding.UTF8.GetString (contents);
	}

	static INoteSection? GetNoteSection (IELF elf, bool is64Bit, string sectionName, ulong noteType = 0)
	{
		if (!elf.TryGetSection (sectionName, out ISection? section) || section == null || section.Type != SectionType.Note) {
			return null;
		}

		var note = (INoteSection)section;
		if (noteType == 0) {
			return note;
		}

		ulong type = is64Bit switch {
			true => ((NoteSection<ulong>)note).NoteType,
			false => ((NoteSection<uint>)note).NoteType
		};

		if (type != noteType) {
			return null;
		}

		return note;
	}

	static byte[]? GetNoteSectionContents (IELF elf, bool is64Bit, string sectionName, ulong noteType = 0)
	{
		return GetNoteSection (elf, is64Bit, sectionName, noteType)?.Description;
	}

	public bool HasSection (string name, SectionType type = SectionType.Null)
	{
		return HasSection (elf, libraryName, name, type);
	}

	protected static bool HasSection (IELF elf, string? libraryName, string sectionName, SectionType type = SectionType.Null)
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

	protected override void Dispose (bool disposing)
	{
		if (Disposed || !disposing) {
			return;
		}

		elf?.Dispose ();
	}
}
