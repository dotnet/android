using System;

using Xamarin.Tools.Zip;

namespace Microsoft.Android.AppTools;

class InputReaderApk : InputReaderZip
{
	public override string DexDirPath            => String.Empty;
	public override string InternalAssetsDirPath => "assets/xa-internal";
	public override string ManifestDirPath       => String.Empty;
	public override string NativeLibsDirPath     => "lib";
	public override string AssembliesDirPath     => "assemblies";
	public override string ArchiveType           =>"APK";

	public InputReaderApk (ZipArchive archive, string archivePath, ILogger log)
		: base (archive, archivePath, log)
	{}
}
