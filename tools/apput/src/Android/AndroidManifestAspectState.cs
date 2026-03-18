using System.Xml;

using ProtoManifest = Aapt.Pb;

namespace ApplicationUtility;

/// <summary>
/// Preserves state from <see cref="AndroidManifest.ProbeAspect"/> for use in <see cref="AndroidManifest.LoadAspect"/>.
/// Carries the parser or document used during probing so parsing is not repeated.
/// </summary>
class AndroidManifestAspectState : IAspectState
{
	public bool Success => true;
	public AXMLParser? BinaryParser { get; }
	public XmlDocument? Xml { get; }
	public ProtoManifest.XmlNode? ProtoManifestRoot { get; }

	public AndroidManifestAspectState (AXMLParser binaryParser)
	{
		BinaryParser = binaryParser;
	}

	public AndroidManifestAspectState (XmlDocument xmlDoc)
	{
		Xml = xmlDoc;
	}

	public AndroidManifestAspectState (ProtoManifest.XmlNode rootNode)
	{
		ProtoManifestRoot = rootNode;
	}
}
