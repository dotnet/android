using System;
using System.IO;

using ApplicationUtility;

public class SharedLibrary : IAspect
{
	public static string AspectName { get; } = "Native shared library";

	public bool HasAndroidPayload => payloadSize > 0;
	public string Name => libraryName;

	readonly ulong payloadOffset;
	readonly ulong payloadSize;
	readonly string libraryName;

	SharedLibrary (Stream stream, string libraryName)
	{
		(payloadOffset, payloadSize) = FindAndroidPayload (stream);
		this.libraryName = libraryName;
	}

	public static IAspect LoadAspect (Stream stream, string description)
	{
		if (!IsELFSharedLibrary (stream)) {
			throw new InvalidOperationException ("Stream is not an ELF shared library");
		}

		return new SharedLibrary (stream, description);
	}

	public static bool ProbeAspect (Stream stream) => IsELFSharedLibrary (stream);

	/// <summary>
	/// If the library has .NET for Android payload section, this
	/// method will read the data and write it to the <paramref name="dest"/>
	/// stream. All the data in the output stream will be overwritten.
	/// </summary>
	public void CopyAndroidPayload (Stream dest)
	{
		Stream payload = OpenAndroidPayload ();
		throw new NotImplementedException ();
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

		throw new NotImplementedException ();
	}

	static bool IsELFSharedLibrary (Stream stream)
	{
		throw new NotImplementedException ();
	}

	(ulong offset, ulong size) FindAndroidPayload (Stream stream)
	{
		throw new NotImplementedException ();
	}
}
