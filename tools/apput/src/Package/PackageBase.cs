using System.IO;
using System.IO.Compression;

namespace ApplicationUtility;

class PackageBase : ApplicationPackage
{
	public override string PackageFormat { get; } = "Base application package";
	protected override string NativeLibDirBase => "lib";
	protected override string AndroidManifestPath => "manifest/AndroidManifest.xml";

	public PackageBase (Stream stream, ZipArchive zip, string? description)
		: base (stream, zip, description)
	{}
}
