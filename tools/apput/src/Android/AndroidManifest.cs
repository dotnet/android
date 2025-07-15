using System;
using System.IO;
using System.Xml;

namespace ApplicationUtility;

public class AndroidManifest : IAspect
{
	public string Description { get; }

	AXMLParser? binaryParser;
	XmlDocument? xmlDoc;

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
		Log.Debug ($"Checking if '{description}' is an Android binary XML document.");
		try {
			stream.Seek (0, SeekOrigin.Begin);

			// The constructor will throw if it cannot recognize the format
			var binaryParser = new AXMLParser (stream);

			// We leave parsing of the data to `LoadAspect`, here we only detect the format
			return new AndroidManifestAspectState (binaryParser);
		} catch (Exception ex) {
			Log.Debug ($"Failed to instantiate AXML binary parser for '{description}'. Exception thrown:", ex);
		}

		Log.Debug ($"Checking if '{description}' is an plain XML document.");
		try {
			return new AndroidManifestAspectState (ParsePlainXML (stream));
		} catch (Exception ex) {
			Log.Debug ($"Failed to parse '{description}' as XML document. Exception thrown:", ex);
		}

		// TODO: AndroidManifest.xml in AAB files is actually a protobuf data dump. Attempt to
		//       deserialize it here.
		return new BasicAspectState (success: false);
	}

	void Read ()
	{
		if (binaryParser == null) {
			throw new NotImplementedException ();
		}

		xmlDoc = binaryParser.Parse ();
		if (xmlDoc == null || !binaryParser.IsValid) {
			Log.Debug ($"AXML parser didn't render a valid document for '{Description}'");
			return;
		}
		Log.Debug ($"'{Description}' loaded and parsed correctly.");
	}

	static XmlDocument ParsePlainXML (Stream stream)
	{
		stream.Seek (0, SeekOrigin.Begin);
		var settings = new XmlReaderSettings {
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
			IgnoreWhitespace = true,
		};

		using var reader = XmlReader.Create (stream, settings);
		var doc = new XmlDocument ();
		doc.Load (reader);

		return doc;
	}
}
