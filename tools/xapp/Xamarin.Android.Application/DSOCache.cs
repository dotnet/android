using System;
using System.Collections.Generic;
using System.IO;

using ELFSharp.ELF.Sections;
using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

// Must be kept in sync with `struct DSOCacheEntry` in src/monodroid/jni/xamarin-app.hh
sealed class DSOCacheEntry
{
	public readonly ulong     hash;
	public readonly bool      ignore;
	public readonly string    name;
	public readonly IntPtr    handle = IntPtr.Zero;

	public DSOCacheEntry (BinaryReader reader, AnELF elf, ISymbolEntry symbolEntry)
	{
		bool is64Bit = elf.Is64Bit;
		ulong sizeSoFar = 0;

		sizeSoFar += Util.ReadField (reader, ref hash, sizeSoFar, is64Bit);
		sizeSoFar += Util.ReadField (reader, ref ignore, sizeSoFar, is64Bit);

		name = elf.GetStringFromPointerField (symbolEntry, sizeSoFar) ?? Constants.UnableToLoadDataForPointer;
		sizeSoFar += Util.ReadField (reader, ref name, sizeSoFar, is64Bit);
		sizeSoFar += Util.ReadField (reader, ref handle, sizeSoFar, is64Bit);
	}

	public static ulong GetSize (AnELF elf)
	{
		ulong size = 0;

		size += elf.GetPaddedSize<ulong> (size);  // hash
		size += elf.GetPaddedSize<bool> (size);   // ignore
		size += elf.GetPaddedSize<string> (size); // name
		size += elf.GetPaddedSize<IntPtr> (size); // handle

		return size;
	}
}

class DSOCache
{
	public List<DSOCacheEntry> Entries { get; } = new List<DSOCacheEntry> ();

	public DSOCache (byte[] data, AnELF elf, ISymbolEntry symbolEntry)
	{
		using var stream = new MemoryStream (data);
		using var reader = new BinaryReader (stream);

		ulong structSize = DSOCacheEntry.GetSize (elf);
		int n = 0;
		while (stream.Position < stream.Length) {
			Console.WriteLine ($"DSO entry #{n++}");
			Entries.Add (new DSOCacheEntry (reader, elf, symbolEntry));
		}
	}
}
