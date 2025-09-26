using System;
using System.IO;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace ApplicationUtility;

class DotNetAndroidWrapperSharedLibrary : SharedLibrary
{
	const string PayloadSectionName = "payload";
	const string DotNetPayloadMarkerSymbol = "dotnet_for_android_data_payload";

	readonly ulong payloadOffset;
	readonly ulong payloadSize;

	public bool HasAndroidPayload => payloadSize > 0;

	protected DotNetAndroidWrapperSharedLibrary (Stream stream, string libraryName, IAspectState state)
		: base (stream, libraryName, state)
	{
		var libState = EnsureValidAspectState<DotNetAndroidWrapperSharedLibraryAspectState> (state);
		(payloadOffset, payloadSize) = FindAndroidPayload (ELF, libState.Is64Bit, libraryName);
	}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libState = EnsureValidAspectState<DotNetAndroidWrapperSharedLibraryAspectState> (state);
		return new DotNetAndroidWrapperSharedLibrary (stream, description, libState);
	}

	public static new IAspectState ProbeAspect (Stream stream, string? description) => IsDotNetAndroidWrapperSharedLibrary (stream, description);

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

		return new SubStream (LibraryStream, (long)payloadOffset, (long)payloadSize);
	}

	static DotNetAndroidWrapperSharedLibraryAspectState IsDotNetAndroidWrapperSharedLibrary (Stream stream, string? description)
	{
		Log.Debug ($"Checking if '{description}' is a Mono AOT shared library");
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return GetErrorState ();
		}

		if (!AnELF.TryLoad (stream, description ?? String.Empty, out AnELF? anElf) || anElf == null) {
			Log.Debug ($"Library '{description}' failed to load");
			return GetErrorState ();
		}

		if (!anElf.HasSymbol (DotNetPayloadMarkerSymbol)) {
			Log.Debug ($"Symbol '{DotNetPayloadMarkerSymbol}' missing, not a .NET for Android wrapper shared library");
			return GetErrorState ();
		}

		return new DotNetAndroidWrapperSharedLibraryAspectState (true, anElf);

		DotNetAndroidWrapperSharedLibraryAspectState GetErrorState () => new DotNetAndroidWrapperSharedLibraryAspectState (false, null);
	}

	(ulong offset, ulong size) FindAndroidPayload (IELF elf, bool is64Bit, string libraryName)
	{
		if (!elf.TryGetSection (PayloadSectionName, out ISection? payloadSection)) {
			Log.Debug ($"Shared library '{libraryName}' doesn't have the '{PayloadSectionName}' section.");
			return (0, 0);
		}

		ulong offset;
		ulong size;

		if (is64Bit) {
			(offset, size) = GetOffsetAndSize64 ((Section<ulong>)payloadSection);
		} else {
			(offset, size) = GetOffsetAndSize32 ((Section<uint>)payloadSection);
		}

		Log.Debug ($"Found payload section at offset {offset}, size of {size} bytes.");
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
}
