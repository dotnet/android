using System;
using System.IO;
using System.Text;

namespace ApplicationUtility;

/// <summary>
/// Extracts the Android manifest as plain XML from a manifest aspect or application package.
/// </summary>
[AspectExtractor (containerAspectType: typeof (AndroidManifest), storedAspectType: typeof (AndroidManifest))]
[AspectExtractor (containerAspectType: typeof (PackageAPK),      storedAspectType: typeof (AndroidManifest))]
[AspectExtractor (containerAspectType: typeof (PackageAAB),      storedAspectType: typeof (AndroidManifest))]
[AspectExtractor (containerAspectType: typeof (PackageBase),     storedAspectType: typeof (AndroidManifest))]
class AndroidManifestExtractor : BaseExtractor
{
	public AndroidManifestExtractor (IAspect containerAspect)
		: base (containerAspect)
	{}

	public override bool Extract (Stream destinationStream)
	{
		AndroidManifest? manifest = ContainerAspect switch {
			AndroidManifest => (AndroidManifest)ContainerAspect,
			ApplicationPackage => ((ApplicationPackage)ContainerAspect).AndroidManifest,
			_ => throw new InvalidOperationException ($"Internal error: unsupported container aspect {ContainerAspect}")
		};

		if (manifest == null) {
			Log.Error ($"Android manifest not found in {ContainerAspect.AspectName}");
			return false;
		}

		using var writer = new StreamWriter (
			destinationStream,
			encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
			leaveOpen: true
		);

		writer.Write (manifest.RenderedXML);
		writer.Flush ();
		writer.Close ();

		return true;
	}

	public override bool Extract (GetOutputStreamForPathFn getOutputStreamForPath)
	{
		return Extract (getOutputStreamForPath ("AndroidManifest.xml"));
	}
}
