using System.IO;
using System.IO.Compression;

namespace ApplicationUtility;

/// <summary>
/// Represents the base module of an Android App Bundle (the <c>base/</c> split).
/// </summary>
class PackageBase : ApplicationPackage
{
	public override string PackageFormat { get; } = "Base application package";
	protected override string NativeLibDirBase => "lib";
	protected override string AndroidManifestPath => "manifest/AndroidManifest.xml";

	public PackageBase (Stream stream, ZipArchive zip, string? description)
		: base (stream, zip, description)
	{}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		return LoadAspect (typeof(PackageBase), stream, state, description);
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		return ProbeAspect (typeof(PackageBase), stream, description);
	}
}
