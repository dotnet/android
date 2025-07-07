using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

public class AssemblyStore : IAspect
{
	const int MinimumStoreSize = 8;
	const uint MagicNumber = 0x41424158; // 'XABA', little-endian

	public static string AspectName { get; } = "Assembly Store";

	public IDictionary<string, ApplicationAssembly> Assemblies { get; private set; } = new Dictionary<string, ApplicationAssembly> (StringComparer.Ordinal);
	public AndroidTargetArch Architecture { get; private set; } = AndroidTargetArch.None;
	public ulong NumberOfAssemblies => (ulong)(Assemblies?.Count ?? 0);

	public static IAspect LoadAspect (Stream stream, string? description)
	{
		throw new NotImplementedException ();
	}

	public static bool ProbeAspect (Stream stream, string? description)
	{
		// TODO: check if it's an ELF .so and extract the payload, if necessary

		// All assembly store files are at least 8 bytes long - space taken up by
		// the magic number + store version.
		if (stream.Length < MinimumStoreSize) {
			Log.Debug ($"AssemblyStore: stream ('{description}') isn't long enough. Need at least {MinimumStoreSize} bytes");
			return false;
		}

		stream.Seek (0, SeekOrigin.Begin);
		using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
		uint magic = reader.ReadUInt32 ();
		if (magic != MagicNumber) {
			Log.Debug ($"AssemblyStore: stream ('{description}') doesn't have the correct signature.");
			return false;
		}

		uint version = reader.ReadUInt32 ();

		// We currently support version 3. Main store version is kept in the lower 16 bits of the version word
		uint mainVersion = version & 0xFFFF;
		switch (mainVersion) {
			case 3:
				return ValidateFormatVersion3 (stream, description);

			default:
				Log.Debug ($"AssemblyStore: unsupported store version: {mainVersion}");
				return false;
		}
	}

	static bool ValidateFormatVersion3 (Stream stream, string? description)
	{
		return true;
	}
}
