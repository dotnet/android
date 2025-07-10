using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ApplicationUtility;

class Format_V3 : FormatBase
{
	protected override string LogTag => "AssemblyStore/Format_V3";

	public const uint HeaderSize = 5 * sizeof(uint);
	public const uint IndexEntrySize32 = sizeof(uint) + sizeof(uint) + sizeof(byte);
	public const uint IndexEntrySize64 = sizeof(ulong) + sizeof(uint) + sizeof(byte);
	public const uint AssemblyDescriptorSize = 7 * sizeof(uint);

	ulong assemblyNamesOffset;

	public Format_V3 (Stream storeStream, string? description)
		: base (storeStream, description)
	{}

	protected bool EnsureValidState (string where, out IAspectState? retval)
	{
		retval = null;
		if (Header == null || Header.EntryCount == null || Header.IndexEntryCount == null || Header.IndexSize == null) {
			retval = ValidationFailed ($"{LogTag}: invalid header data in {where}.");
			return false;
		}

		if (Descriptors == null || Descriptors.Count == 0) {
			retval = ValidationFailed ($"{LogTag}: no descriptors read in {where}.");
			return false;
		}

		return true;
	}

	protected override IAspectState ValidateInner ()
	{
		Log.Debug ($"{LogTag}: validating store format.");
		if (!EnsureValidState (nameof (ValidateInner), out IAspectState? retval)) {
			return retval!;
		}

		// Repetitive to `EnsureValidState`, but it's better than using `!` all over the place below...
		Debug.Assert (Header != null);
		Debug.Assert (Header.EntryCount != null);
		Debug.Assert (Header.IndexEntryCount != null);
		Debug.Assert (Descriptors != null);

		ulong indexEntrySize = Header.Version.Is64Bit ? IndexEntrySize64 : IndexEntrySize32;
		ulong indexSize = (indexEntrySize * (ulong)Header.IndexEntryCount!);
		ulong descriptorsSize = AssemblyDescriptorSize * (ulong)Header.EntryCount!;
		ulong requiredStreamSize = HeaderSize + indexSize + descriptorsSize;

		// It points to the start of the assembly names block
		assemblyNamesOffset = requiredStreamSize;

		// This is a trick to avoid having to read all the assembly names, but if the stream is valid, it won't be a
		// problem and otherwise, well, we're validating after all. First descriptor's data offset points to the next
		// byte after assembly names block.
		ulong assemblyNamesSize = ((AssemblyStoreAssemblyDescriptorV3)Descriptors[0]).DataOffset - requiredStreamSize;
		requiredStreamSize += assemblyNamesSize;

		foreach (var d in Descriptors) {
			var desc = (AssemblyStoreAssemblyDescriptorV3)d;

			requiredStreamSize += desc.DataSize + desc.DebugDataSize + desc.ConfigDataSize;
		}
		Log.Debug ($"{LogTag}: calculated the required stream size to be {requiredStreamSize}");

		if (requiredStreamSize > Int64.MaxValue) {
			return ValidationFailed ($"{LogTag}: required stream size is too long for the stream API to handle.");
		}

		if ((long)requiredStreamSize != StoreStream.Length) {
			return ValidationFailed ($"{LogTag}: stream has invalid size, expected {requiredStreamSize} bytes, found {StoreStream.Length} instead.");
		} else {
			Log.Debug ($"{LogTag}: stream size is valid.");
		}

		return new AssemblyStoreAspectState (this);
	}

	protected override IList<string> ReadAssemblyNames (BinaryReader reader)
	{
		Debug.Assert (Header != null);
		Debug.Assert (Header.EntryCount != null);

		reader.BaseStream.Seek ((long)assemblyNamesOffset, SeekOrigin.Begin);
		var ret = new List<string> ();

		for (ulong i = 0; i < Header.EntryCount; i++) {
			uint length = reader.ReadUInt32 ();
			if (length == 0) {
				continue;
			}

			byte[] nameBytes = reader.ReadBytes ((int)length);
			ret.Add (Encoding.UTF8.GetString (nameBytes));
		}

		return ret.AsReadOnly ();
	}

	protected override bool ReadAssemblies (BinaryReader reader, out IList<ApplicationAssembly>? assemblies)
	{
		Debug.Assert (Descriptors != null);

		assemblies = null;
		if (!EnsureValidState (nameof (ReadAssemblies), out _)) {
			return false;
		}

		IList<string> assemblyNames = ReadAssemblyNames (reader);
		if (assemblyNames.Count != Descriptors.Count) {
			Log.Debug ($"{LogTag}: assembly name count ({assemblyNames.Count}) is different to descriptor count ({Descriptors.Count})");
			return false;
		}

		var ret = new List<ApplicationAssembly> ();
		for (int i = 0; i < Descriptors.Count; i++) {
			var desc = (AssemblyStoreAssemblyDescriptorV3)Descriptors[i];
			string name = assemblyNames[i];
			var assemblyStream = new SubStream (reader.BaseStream, (long)desc.DataOffset, (long)desc.DataSize);
			IAspectState assemblyState = ApplicationAssembly.ProbeAspect (assemblyStream, name);
			if (!assemblyState.Success) {
				assemblyStream.Dispose ();
				continue;
			}

			var assembly = (ApplicationAssembly)ApplicationAssembly.LoadAspect (assemblyStream, assemblyState, name);
			ret.Add (assembly);
		}

		assemblies = ret.AsReadOnly ();
		return true;
	}
}
