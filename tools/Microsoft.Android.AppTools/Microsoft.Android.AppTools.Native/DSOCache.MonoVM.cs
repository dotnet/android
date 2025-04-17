using System;
using System.Collections.Generic;
using System.IO;

using ELFSharp.ELF.Sections;

namespace Microsoft.Android.AppTools.Native;

// Must be kept in sync with `struct DSOCacheEntry` in src/native/mono/monodroid/xamarin-app.hh
sealed class DSOCacheEntryMonoVM
{
	public readonly ulong     hash;
	public readonly bool      ignore;
	public readonly string    name;
	public readonly IntPtr    handle = IntPtr.Zero;

	public DSOCacheEntryMonoVM (ILogger log, BinaryReader reader, AnELF elf, ISymbolEntry symbolEntry)
	{
		bool is64Bit = elf.Is64Bit;
		ulong sizeSoFar = 0;
		ulong entryOffset = (ulong)reader.BaseStream.Position;

		sizeSoFar += NativeHelpers.ReadField (reader, ref hash, sizeSoFar, is64Bit);
		sizeSoFar += NativeHelpers.ReadField (reader, ref ignore, sizeSoFar, is64Bit);

		ulong pointerOffset = NativeHelpers.GetPadding<string> (sizeSoFar, is64Bit) + sizeSoFar + entryOffset;
		name = elf.GetStringFromPointerField (symbolEntry, pointerOffset) ?? Constants.UnableToLoadDataForPointer;
		sizeSoFar += NativeHelpers.ReadField (reader, ref name, sizeSoFar, is64Bit);
		sizeSoFar += NativeHelpers.ReadField (reader, ref handle, sizeSoFar, is64Bit);
	}
}

class DSOCacheMonoVM
{
	public List<DSOCacheEntryMonoVM> Entries { get; } = new List<DSOCacheEntryMonoVM> ();

	public DSOCacheMonoVM (ILogger log, byte[] data, AnELF elf, ISymbolEntry symbolEntry)
	{
		using var stream = new MemoryStream (data);
		using var reader = new BinaryReader (stream);

		while (stream.Position < stream.Length) {
			Entries.Add (new DSOCacheEntryMonoVM (log, reader, elf, symbolEntry));
		}
	}
}
