using System.IO.Compression;

namespace ApplicationUtility;

class PackageAAB : ApplicationPackage
{
	public override string PackageFormat { get; } = "AAB package";

	public PackageAAB (ZipArchive zip, string? description)
		: base (zip, description)
	{}
}
