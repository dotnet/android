using System;
using System.IO;

namespace ApplicationUtility;

public class AndroidManifest : IAspect
{
	public string Description { get; }

	AXMLParser? binaryParser;

	AndroidManifest (AXMLParser binaryParser, string? description)
	{
		Description = String.IsNullOrEmpty (description) ? "Android manifest" : description;
		this.binaryParser = binaryParser;
	}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		var manifestState = state as AndroidManifestAspectState;
		if (manifestState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		AndroidManifest ret;
		if (manifestState.BinaryParser != null) {
			ret = new AndroidManifest (manifestState.BinaryParser, description);
		} else {
			throw new NotImplementedException ();
		}
		ret.Read ();

		return ret;
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		try {
			stream.Seek (0, SeekOrigin.Begin);

			// The constructor will throw if it cannot recognize the format
			var binaryParser = new AXMLParser (stream);

			// We leave parsing of the data to `LoadAspect`, here we only detect the format
			return new AndroidManifestAspectState (binaryParser);
		} catch (Exception ex) {
			Log.Debug ($"Failed to instantiate AXML binary parser for '{description}'", ex);
		}

		// TODO: detect plain XML
		throw new NotImplementedException ();
	}

	void Read ()
	{
		throw new NotImplementedException ();
	}
}
