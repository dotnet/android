using System.Xml;

namespace ApplicationUtility;

class AndroidManifestAspectState : IAspectState
{
	public bool Success => true;
	public AXMLParser? BinaryParser { get; }
	public XmlDocument? Xml { get; }

	public AndroidManifestAspectState (AXMLParser? binaryParser)
	{
		BinaryParser = binaryParser;
	}

	public AndroidManifestAspectState (XmlDocument? xmlDoc)
	{
		Xml = xmlDoc;
	}
}
