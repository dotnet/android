using System.Xml;

using ProtoManifest = Aapt.Pb;

namespace ApplicationUtility;

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
