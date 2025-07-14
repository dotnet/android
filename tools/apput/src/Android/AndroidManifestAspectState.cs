namespace ApplicationUtility;

class AndroidManifestAspectState : IAspectState
{
	public bool Success => true;
	public AXMLParser? BinaryParser { get; }

	public AndroidManifestAspectState (AXMLParser? binaryParser)
	{
		BinaryParser = binaryParser;
	}
}
