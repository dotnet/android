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
	public AndroidTargetArch Architecture { get; }
	public ulong NumberOfAssemblies => (ulong)(Assemblies?.Count ?? 0);

	AssemblyStoreAspectState storeState;
	string? description;

	AssemblyStore (AssemblyStoreAspectState state, string? description)
	{
		storeState = state;
		this.description = description;

		AssemblyStoreHeader? header = state.Format.Header;
		if (header == null) {
			throw new InvalidOperationException ("Internal error: state doesn't contain a valid store header.");
		}

		Architecture = header.Version.ABI switch {
			AssemblyStoreABI.Arm   => AndroidTargetArch.Arm,
			AssemblyStoreABI.Arm64 => AndroidTargetArch.Arm64,
			AssemblyStoreABI.X86   => AndroidTargetArch.X86,
			AssemblyStoreABI.X64   => AndroidTargetArch.X86_64,
			_                      => throw new InvalidOperationException ($"Internal error: unsupported assembly store ABI '{header.Version.ABI}'")
		};
	}

	bool Read ()
	{
		if (!storeState.Format.Read ()) {
			return false;
		}

		foreach (ApplicationAssembly asm in storeState.Format.Assemblies) {
			Assemblies.Add (asm.Name, asm);
		}

		return true;
	}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		var storeState = state as AssemblyStoreAspectState;
		if (storeState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		var store = new AssemblyStore (storeState, description);
		if (store.Read ()) {
			return store;
		}

		throw new InvalidOperationException ($"Failed to load assembly store '{description}'");
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		Stream? storeStream = null;

		try {
			IAspectState state = DotNetAndroidWrapperSharedLibrary.ProbeAspect (stream, description);
			if (!state.Success) {
				return DoProbeAspect (stream, description);
			}

			var library = (DotNetAndroidWrapperSharedLibrary)DotNetAndroidWrapperSharedLibrary.LoadAspect (stream, state, description);
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
