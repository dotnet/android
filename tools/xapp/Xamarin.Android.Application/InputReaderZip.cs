using System;

using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

abstract class InputReaderZip : InputReader
{
	public override bool SupportsAssemblyExtraction => true;
	public override bool SupportsAssemblyStore      => true;
	public override bool SupportsXamarinApp         => true;
	public override bool SupportsTypemaps           => true;
	public override bool SupportsAppInfo            => true;

	protected ZipArchive Archive { get; }
	protected string ArchivePath { get; }

	protected abstract string DexDirPath            { get; }
	protected abstract string InternalAssetsDirPath { get; }
	protected abstract string ManifestDirPath       { get; }
	protected abstract string NativeLibsDirPath     { get; }
	protected abstract string AssembliesDirPath     { get; }
	protected abstract string ArchiveType           { get; }

	protected InputReaderZip (ZipArchive archive, string archivePath)
	{
		Archive = archive;
		ArchivePath = archivePath;
	}

	protected override bool DoExtractAssembly (string assemblyNameRegex, string outputDirectory, bool decompress )
	{
		throw new NotImplementedException ();
	}

	protected override DataProviderAppInfo? ReadAppInfo ()
	{
		return new DataProviderAppInfo (Archive, ArchivePath, ArchiveType);
	}

	protected override DataProviderXamarinApp? ReadXamarinApp ()
	{
		throw new NotImplementedException ();
	}

	protected override DataProviderTypemaps? ReadTypemaps ()
	{
		throw new NotImplementedException ();
	}

	protected override DataProviderAssemblyStore? ReadAssemblyStore ()
	{
		return null;
	}
}
