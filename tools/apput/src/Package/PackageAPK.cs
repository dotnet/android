using System.IO.Compression;

namespace ApplicationUtility;

class PackageAPK : ApplicationPackage
{
	public override string PackageFormat { get; } = "APK package";

	public PackageAPK (ZipArchive zip, string? description)
		: base (zip, description)
	{}
}
