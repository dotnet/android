using System;
using System.IO;

namespace tmt
{
	abstract class XamarinAppDSO : ITypemap
	{
		// Corresponds to the `FORMAT_TAG` constant in src/monodroid/xamarin-app.hh
		protected const ulong FormatTag_V1 = 0x00015E6972616D58;
		protected const ulong FormatTag_V2 = 0x00025E6972616D58;

		protected const string FormatTag = "format_tag";

		AnELF? elf;

		protected AnELF ELF                           => elf ?? throw new InvalidOperationException ("ELF image not loaded");
		protected bool Is64Bit                        => ELF.Is64Bit;
		protected ManagedTypeResolver ManagedResolver { get; }

		public MapArchitecture MapArchitecture        => ELF.MapArchitecture;
		public string FullPath                        { get; } = String.Empty;
		protected abstract string LogTag              { get; }
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
			return Helpers.ReadUInt32 (data, ref offset, Is64Bit, packed);
		}

		protected ulong ReadUInt64 (byte[] data, ref ulong offset, bool packed = false)
		{
			return Helpers.ReadUInt64 (data, ref offset, Is64Bit, packed);
		}

		protected ulong ReadPointer (byte[] data, ref ulong offset, bool packed = false)
		{
			return Helpers.ReadPointer (data, ref offset, Is64Bit, packed);
		}

		protected ulong GetPaddedSize<S> (ulong sizeSoFar)
		{
			return Helpers.GetPaddedSize<S> (sizeSoFar, Is64Bit);
		}
	}
}
