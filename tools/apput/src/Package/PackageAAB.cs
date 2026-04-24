using System.IO;
using System.IO.Compression;

namespace ApplicationUtility;

/// <summary>
/// Represents an Android App Bundle (AAB) file.
/// </summary>
class PackageAAB : ApplicationPackage
{
	public override string PackageFormat { get; } = "AAB package";
	protected override string NativeLibDirBase => "base/lib";
	protected override string AndroidManifestPath => "base/manifest/AndroidManifest.xml";

	public PackageAAB (Stream stream, ZipArchive zip, string? description)
		: base (stream, zip, description)
	{}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		return LoadAspect (typeof(PackageAAB), stream, state, description);
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		return ProbeAspect (typeof(PackageAAB), stream, description);
	}
}
