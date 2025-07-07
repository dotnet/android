using System.IO.Compression;

namespace ApplicationUtility;

class PackageAPK : ApplicationPackage
{
	public override string PackageFormat { get; } = "APK package";
	protected override string NativeLibDirBase => "lib";
	protected override string AndroidManifestPath => "AndroidManifest.xml";

	public PackageAPK (ZipArchive zip, string? description)
		: base (zip, description)
	{}
}
