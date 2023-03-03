using System;
using System.IO;

namespace tmt
{
	abstract class XamarinAppDSO : ITypemap
	{
		// Corresponds to the `FORMAT_TAG` constant in src/monodroid/xamarin-app.hh
		protected const ulong FormatTag_V1 = 0x015E6972616D58;

		protected const string FormatTag = "format_tag";

		AnELF? elf;

		protected AnELF ELF                           => elf ?? throw new InvalidOperationException ("ELF image not loaded");
		protected bool Is64Bit                        => ELF.Is64Bit;
		protected ManagedTypeResolver ManagedResolver { get; }

		public MapArchitecture MapArchitecture        => ELF.MapArchitecture;
		public string FullPath                        { get; } = String.Empty;
		public abstract string Description            { get; }
		public abstract string FormatVersion          { get; }
		public abstract Map Map                       { get; }

		protected XamarinAppDSO (ManagedTypeResolver managedResolver, string fullPath)
		{
			ManagedResolver = managedResolver;
			FullPath = fullPath;
		}

		protected XamarinAppDSO (ManagedTypeResolver managedResolver, AnELF elf)
			: this (managedResolver, Path.GetFullPath (elf.FilePath))
		{
			this.elf = elf;
		}

		public bool CanLoad (Stream stream, string filePath)
		{
			stream.Seek (0, SeekOrigin.Begin);
			if (!AnELF.TryLoad (stream, filePath, out AnELF? elf) || elf == null) {
				Log.Debug ($"AnELF.TryLoad failed (elf == {elf})");
				return false;
			}

			if (!CanLoad (elf)) {
				Log.Debug ($"Cannot load {elf} in {this}");
				return false;
			}

			this.elf = elf;
			return true;
		}

		public abstract bool Load (string outputDirectory, bool generateFiles);
		public abstract bool CanLoad (AnELF elf);

		protected bool HasSymbol (AnELF elf, string symbolName)
		{
			bool ret = elf.HasSymbol (symbolName);
			if (!ret)
				Log.Debug ($"{elf.FilePath} is missing symbol '{symbolName}'");
			return ret;
		}

		protected uint ReadUInt32 (byte[] data, ref ulong offset, bool packed = false)
		{
			const ulong DataSize = 4;

			if ((ulong)data.Length < (offset + DataSize))
				throw new InvalidOperationException ("Not enough data to read a 32-bit integer");

			uint ret = BitConverter.ToUInt32 (data, (int)offset);
			offset += packed ? DataSize : GetPaddedSize<uint> (offset);

			return ret;
		}

		protected ulong ReadUInt64 (byte[] data, ref ulong offset, bool packed = false)
		{
			const ulong DataSize = 8;

			if ((ulong)data.Length < (offset + DataSize))
				throw new InvalidOperationException ("Not enough data to read a 64-bit integer");

			ulong ret = BitConverter.ToUInt64 (data, (int)offset);
			offset += packed ? DataSize : GetPaddedSize<ulong> (offset);

			return ret;
		}

		protected ulong ReadPointer (byte[] data, ref ulong offset, bool packed = false)
		{
			ulong ret;

			if (Is64Bit) {
				ret = ReadUInt64 (data, ref offset, packed);
			} else {
				ret = (ulong)ReadUInt32 (data, ref offset, packed);
			}

			return ret;
		}

		protected ulong GetPaddedSize<S> (ulong sizeSoFar)
		{
			ulong typeSize = GetTypeSize<S> ();

			ulong modulo;
			if (Is64Bit) {
				modulo = typeSize < 8 ? 4u : 8u;
			} else {
				modulo = 4u;
			}

			ulong alignment = sizeSoFar % modulo;
			if (alignment == 0)
				return typeSize;

			return typeSize + (modulo - alignment);
		}

		ulong GetTypeSize<S> ()
		{
			Type type = typeof(S);

			if (type == typeof(string)) {
				// We treat `string` as a generic pointer
				return Is64Bit ? 8u : 4u;
			}

			if (type == typeof(byte)) {
				return 1u;
			}

			if (type == typeof(bool)) {
				return 1u;
			}

			if (type == typeof(Int32) || type == typeof(UInt32)) {
				return 4u;
			}

			if (type == typeof(Int64) || type == typeof(UInt64)) {
				return 8u;
			}

			throw new InvalidOperationException ($"Unable to map managed type {type} to native assembler type");
		}
	}
}
