using System;
using System.Collections.Generic;

namespace tmt
{
	class XamarinAppReleaseDSO : XamarinAppDSO
	{
		XamarinAppReleaseDSO_Version? xapp;

		public override string FormatVersion => xapp?.FormatVersion ?? "0";
		protected override string LogTag => "ReleaseDSO";
		public override string Description => xapp?.Description ?? "Xamarin App Release DSO Forwarder";
		public override Map Map => XAPP.Map;

		XamarinAppReleaseDSO_Version XAPP => xapp ?? throw new InvalidOperationException ("Format implementation not found");

		public XamarinAppReleaseDSO (ManagedTypeResolver managedResolver, string fullPath)
			: base (managedResolver, fullPath)
		{}

		protected XamarinAppReleaseDSO (ManagedTypeResolver managedResolver, AnELF elf)
			: base (managedResolver, elf)
		{}

		public override bool CanLoad (AnELF elf)
		{
			Log.Debug (LogTag, $"Checking if {elf.FilePath} is a Release DSO");

			xapp = null;
			ulong format_tag = 0;
			if (elf.HasSymbol (FormatTag))
				format_tag = elf.GetUInt64 (FormatTag);

			XamarinAppReleaseDSO_Version? reader = null;
			switch (format_tag) {
				case 0:
				case FormatTag_V1:
					format_tag = 1;
					reader = new XamarinAppReleaseDSO_V1 (ManagedResolver, elf);
					break;

				case FormatTag_V2:
					format_tag = 2;
					reader = new XamarinAppReleaseDSO_V2 (ManagedResolver, elf);
					break;

				default:
					Log.Debug (LogTag, $"{elf.FilePath} format (0x{format_tag:x}) is not supported by this version of TMT");
					return false;
			}

			if (reader == null || !reader.CanLoad (elf)) {
				return false;
			}

			xapp = reader;
			return true;
		}

		public override bool Load (string outputDirectory, bool generateFiles)
		{
			return XAPP.Load (outputDirectory, generateFiles);
		}
	}

	abstract class XamarinAppReleaseDSO_Version : XamarinAppDSO
	{
		public override string Description => "Xamarin App Release DSO";

		protected XamarinAppReleaseDSO_Version (ManagedTypeResolver managedResolver, AnELF elf)
			: base (managedResolver, elf)
		{}

		protected Map MakeMap (List<MapEntry> managedToJava, List<MapEntry> javaToManaged)
		{
			return new Map (MapKind.Release, ELF.MapArchitecture, managedToJava, javaToManaged, FormatVersion);
		}

		public override bool Load (string outputDirectory, bool generateFiles)
		{
			if (!LoadMaps ()) {
				return false;
			}

			if (generateFiles) {
				string rawOutputFile = Utilities.GetOutputFileBaseName (outputDirectory, FormatVersion, MapKind.Release, ELF.MapArchitecture);
				SaveRaw (rawOutputFile, "raw");
			}

			return Convert ();
		}

		protected abstract bool LoadMaps ();
		protected abstract bool Convert ();
		protected abstract void SaveRaw (string baseOutputFilePath, string extension);
	}
}
