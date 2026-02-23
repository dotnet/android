using System.IO;
using System.IO.Compression;

namespace ApplicationUtility;

class PackageAPK : ApplicationPackage
{
	public override string PackageFormat { get; } = "APK package";
	protected override string NativeLibDirBase => "lib";
	protected override string AndroidManifestPath => "AndroidManifest.xml";

	public PackageAPK (Stream stream, ZipArchive zip, string? description)
		: base (stream, zip, description)
	{}
}
