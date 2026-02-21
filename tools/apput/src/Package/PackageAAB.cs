using System.IO.Compression;

namespace ApplicationUtility;

class PackageAAB : ApplicationPackage
{
	public override string PackageFormat { get; } = "AAB package";
	protected override string NativeLibDirBase => "base/lib";
	protected override string AndroidManifestPath => "base/manifest/AndroidManifest.xml";

	public PackageAAB (ZipArchive zip, string? description)
		: base (zip, description)
	{}
}
