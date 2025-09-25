using System;
using System.IO;
using System.Xml;

namespace ApplicationUtility;

public class AndroidManifest : IAspect
{
	public string Description { get; }
	public string? PackageName { get; }

	XmlDocument? xmlDoc;
	XmlNamespaceManager? nsmgr;

	AndroidManifest (AXMLParser binaryParser, string? description)
	{
		Description = String.IsNullOrEmpty (description) ? "Android manifest" : description;
		Read (binaryParser);

		nsmgr = PrepareForReading (xmlDoc);
		PackageName = TryGetPackageName (xmlDoc, nsmgr);
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

	void Read (AXMLParser? binaryParser)
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

	static XmlNamespaceManager? PrepareForReading (XmlDocument? doc)
	{
		if (doc == null) {
			return null;
		}

		var nsmgr = new XmlNamespaceManager (doc.NameTable);
		nsmgr.AddNamespace ("android", "http://schemas.android.com/apk/res/android");
		return nsmgr;
	}

	static bool ValidXmlContext (XmlDocument? doc, XmlNamespaceManager? nsmgr, out XmlElement? root)
	{
		root = null;
		if (doc == null || nsmgr == null) {
			return false;
		}

		root = doc.DocumentElement;
		return root != null;
	}

	static string? TryGetMainActivity (XmlDocument? doc, XmlNamespaceManager? nsmgr)
	{
		if (!ValidXmlContext (doc, nsmgr, out XmlElement? root)) {
			return null;
		}

		XmlNodeList? activities = root!.SelectNodes ("//manifest/application/activity", nsmgr!);
		if (activities == null || activities.Count == 0) {
			return null;
		}

		foreach (XmlNode activity in activities) {
			string? name = GetLauncherActivityName (activity);
			if (name == null) {
				continue;
			}

			return name;
		}

		return null;

		string? GetLauncherActivityName (XmlNode activity)
		{
			XmlNodeList? intentFilters = activity.SelectNodes ("./intent-filter", nsmgr!);
			if (intentFilters == null || intentFilters.Count == 0) {
				return null;
			}

			bool isMain = false;
			foreach (XmlNode intentFilter in intentFilters) {
				XmlNodeList? actions = activity.SelectNodes ("./action", nsmgr!);
				if (actions == null || actions.Count == 0) {
					continue;
				}

				if (!HaveNodeWithNameAttribute (actions, "android.intent.action.MAIN")) {
					continue;
				}

				XmlNodeList? categories = activity.SelectNodes ("./category", nsmgr!);
				if (categories == null || categories.Count == 0) {
					continue;
				}

				if (!HaveNodeWithNameAttribute (categories, "android.intent.category.LAUNCHER")) {
					continue;
				}

				isMain = true;
				break;
			}

			if (!isMain) {
				return null;
			}

			var attr = activity.Attributes?.GetNamedItem ("android:name");
			if (attr == null) {
				return null;
			}

			return attr.Value;
		}

		bool HaveNodeWithNameAttribute (XmlNodeList list, string nameValue)
		{
			foreach (XmlNode? node in list) {
				var attr = node?.Attributes?.GetNamedItem ("android:name");
				if (attr == null || String.IsNullOrEmpty (attr.Value)) {
					continue;
				}

				if (attr.Value == nameValue) {
					return true;
				}
			}

			return false;
		}
	}

	static string? TryGetPackageName (XmlDocument? doc, XmlNamespaceManager? nsmgr)
	{
		Log.Debug ("Trying to read package name");
		if (!ValidXmlContext (doc, nsmgr, out XmlElement? root) || root == null) {
			return null;
		}

		XmlNode? manifest = root.SelectSingleNode ("//manifest", nsmgr);
		if (manifest == null || manifest.Attributes == null) {
			Log.Debug ("`manifest` element not found or it has no attributes");
			return null;
		}

		XmlNode? package = manifest.Attributes.GetNamedItem ("package");
		if (package == null) {
			Log.Debug ("`package` attribute in the `manifest` element not found");
		}

		return package == null ? null : package.Value;
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
