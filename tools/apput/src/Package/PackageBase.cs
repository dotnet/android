using System.IO.Compression;

namespace ApplicationUtility;

class PackageBase : ApplicationPackage
{
	public override string PackageFormat { get; } = "Base application package";
	protected override string NativeLibDirBase => "lib";
	protected override string AndroidManifestPath => "manifest/AndroidManifest.xml";

	public PackageBase (ZipArchive zip, string? description)
		: base (zip, description)
	{}
}
