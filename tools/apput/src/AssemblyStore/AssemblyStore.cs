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

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		var storeState = state as AssemblyStoreAspectState;
		throw new NotImplementedException ();
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		Stream? storeStream = null;

		try {
			IAspectState state = SharedLibrary.ProbeAspect (stream, description);
			if (!state.Success) {
				return DoProbeAspect (stream, description);
			}

			var library = (SharedLibrary)SharedLibrary.LoadAspect (stream, state, description);
			if (!library.HasAndroidPayload) {
				Log.Debug ($"AssemblyStore: stream ('{description}') is an ELF shared library, without payload");
				return new BasicAspectState (false);
			}
			Log.Debug ($"AssemblyStore: stream ('{description}') is an ELF shared library with .NET for Android payload section");
			storeStream = library.OpenAndroidPayload ();
			return DoProbeAspect (storeStream, description);
		} finally {
			storeStream?.Dispose ();
		}
	}

	// We return `BasicAspectState` instance for all failures, since there's no extra information we can
	// pass on.
	static IAspectState DoProbeAspect (Stream storeStream, string? description)
	{
		// All assembly store files are at least 8 bytes long - space taken up by
		// the magic number + store version.
		if (storeStream.Length < MinimumStoreSize) {
			Log.Debug ($"AssemblyStore: stream ('{description}') isn't long enough. Need at least {MinimumStoreSize} bytes");
			return new BasicAspectState (false);
		}

		storeStream.Seek (0, SeekOrigin.Begin);
		using var reader = new BinaryReader (storeStream, Encoding.UTF8, leaveOpen: true);
		uint magic = reader.ReadUInt32 ();
		if (magic != MagicNumber) {
			Log.Debug ($"AssemblyStore: stream ('{description}') doesn't have the correct signature.");
			return new BasicAspectState (false);
		}

		uint version = reader.ReadUInt32 ();
		var storeVersion = new AssemblyStoreVersion (version);
		FormatBase? validator = null;
		Log.Debug ($"AssemblyStore: store format version {storeVersion.MainVersion}");

		switch (storeVersion.MainVersion) {
			case 2:
				validator = new Format_V2 (storeStream, description);
				break;

			case 3:
				validator = new Format_V3 (storeStream, description);
				break;

			default:
				Log.Debug ($"AssemblyStore: unsupported store version: {storeVersion.MainVersion}");
				return new BasicAspectState (false);
		}

		if (validator == null) {
			throw new InvalidOperationException ("Internal error: validator should never be null here");
		}

		return validator.Validate ();
	}
}
