using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

/// <summary>
/// Represents a .NET for Android assembly store—a binary container holding multiple
/// managed assemblies, PDB files, and config data for a single ABI target.
/// </summary>
public class AssemblyStore : BaseAspect
{
	const int MinimumStoreSize = 8;
	const uint MagicNumber = 0x41424158; // 'XABA', little-endian

	public override string AspectName { get; } = "Assembly Store";

	public IDictionary<string, ApplicationAssembly> Assemblies { get; private set; } = new Dictionary<string, ApplicationAssembly> (StringComparer.Ordinal);
	public IDictionary<string, AssemblyPdb> PDBs { get; private set; } = new Dictionary<string, AssemblyPdb> (StringComparer.Ordinal);
	public IDictionary<string, string> Configs { get; private set; } = new Dictionary<string, string> (StringComparer.Ordinal);
	public NativeArchitecture Architecture { get; }
	public ulong NumberOfAssemblies => (ulong)(Assemblies?.Count ?? 0);

	AssemblyStoreAspectState storeState;
	string? description;

	AssemblyStore (Stream stream, AssemblyStoreAspectState state, string? description)
		: base (stream)
	{
		storeState = state;
		this.description = description;

		AssemblyStoreHeader? header = state.Format.Header;
		if (header == null) {
			throw new InvalidOperationException ("Internal error: state doesn't contain a valid store header.");
		}

		Architecture = header.Version.ABI switch {
			AssemblyStoreABI.Arm   => NativeArchitecture.Arm,
			AssemblyStoreABI.Arm64 => NativeArchitecture.Arm64,
			AssemblyStoreABI.X86   => NativeArchitecture.X86,
			AssemblyStoreABI.X64   => NativeArchitecture.X64,
			_                      => throw new InvalidOperationException ($"Internal error: unsupported assembly store ABI '{header.Version.ABI}'")
		};
	}

	protected override void Dispose (bool disposing)
	{
		if (Disposed || !disposing) {
			base.Dispose (disposing);
			return;
		}

		// We need to dispose assemblies first, because they might be using substreams
		foreach (var kvp in Assemblies) {
			try {
				kvp.Value.Dispose ();
			} catch (Exception ex) {
				Log.Debug ("Failed to dispose an application assembly", ex);
			}
		}
		Assemblies.Clear ();

		foreach (var kvp in PDBs) {
			try {
				kvp.Value.Dispose ();
			} catch (Exception ex) {
				Log.Debug ("Failed to dispose application assembly PDB", ex);
			}
		}
		PDBs.Clear ();

		base.Dispose (disposing);
	}

	bool Read (Stream storeStream)
	{
		if (!storeState.Format.Read (storeStream)) {
			return false;
		}

		foreach (ApplicationAssembly asm in storeState.Format.Assemblies) {
			Assemblies.Add (asm.FullName, asm);
		}

		foreach (AssemblyPdb pdb in storeState.Format.PDBs) {
			PDBs.Add (pdb.Name, pdb);
		}

		foreach (var kvp in storeState.Format.Configs) {
			Configs.Add (kvp.Key, kvp.Value);
		}

		return true;
	}

	/// <summary>
	/// Loads an assembly store from the given stream and probe state.
	/// </summary>
	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		var storeState = state as AssemblyStoreAspectState;
		if (storeState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		var store = new AssemblyStore (stream, storeState, description);
		if (store.Read (stream)) {
			return store;
		}

		throw new InvalidOperationException ($"Failed to load assembly store '{description}'");
	}

	/// <summary>
	/// We return `BasicAspectState` instance for all failures, since there's no extra information we can
	/// pass on. `context`, if not `null`, is an offset into `stream` to where the actual store data begins.
	/// It is used when the store is stored inside a shared library. It has to be of type `ulong`.
	/// </summary>
	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		// All assembly store files are at least 8 bytes long - space taken up by
		// the magic number + store version.
		if (stream.Length < MinimumStoreSize) {
			Log.Debug ($"AssemblyStore: stream ('{description}') isn't long enough. Need at least {MinimumStoreSize} bytes");
			return new BasicAspectState (false);
		}

		stream.Seek (0, SeekOrigin.Begin);
		using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
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
				validator = new Format_V2 (description);
				break;

			case 3:
				validator = new Format_V3 (description);
				break;

			default:
				Log.Debug ($"AssemblyStore: unsupported store version: {storeVersion.MainVersion}");
				return new BasicAspectState (false);
		}

		if (validator == null) {
			throw new InvalidOperationException ("Internal error: validator should never be null here");
		}

		return validator.Validate (stream);
	}
}
