using System;

using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

class InputReaderApk : InputReaderZip
{
	protected override string DexDirPath            => String.Empty;
	protected override string InternalAssetsDirPath => "assets/xa-internal";
	protected override string ManifestDirPath       => String.Empty;
	protected override string NativeLibsDirPath     => "lib";
	protected override string AssembliesDirPath     => "assemblies";
	protected override string ArchiveType           =>"APK";

	public InputReaderApk (ZipArchive archive, string archivePath)
		: base (archive, archivePath)
	{}
}
