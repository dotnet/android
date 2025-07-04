using System.IO.Compression;

namespace ApplicationUtility;

class PackageBase : ApplicationPackage
{
	public override string PackageFormat { get; } = "Base application package";

	public PackageBase (ZipArchive zip, string? description)
		: base (zip, description)
	{}
}
