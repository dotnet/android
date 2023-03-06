using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

class InputReaderAab : InputReaderZip
{
	protected override string DexDirPath            => "base/dex";
	protected override string InternalAssetsDirPath => "base/assets/xa-internal";
	protected override string ManifestDirPath       => "base/manifest";
	protected override string NativeLibsDirPath     => "base/lib";
	protected override string AssembliesDirPath     => "base/root/assemblies";
	protected override string ArchiveType           =>"AAB";

	public InputReaderAab (ZipArchive archive, string archivePath)
		: base (archive, archivePath)
	{}
}
