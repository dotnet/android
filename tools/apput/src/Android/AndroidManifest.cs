using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using ProtoManifest = Aapt.Pb;

namespace ApplicationUtility;

// TODO: implement support for AndroidManifest.xml in AAB packages. It's protobuf data, not binary/text XML
public class AndroidManifest : IAspect
{
	public string Description           { get; }
	public AndroidManifestFormat Format { get; } = AndroidManifestFormat.Unknown;
	public string? MainActivity         { get; }
	public string? MinSdkVersion        { get; }
	public string? PackageName          { get; }
	public List<string>? Permissions    { get; }
	public XmlDocument? RawXML          => xmlDoc;
	public string? TargetSdkVersion     { get; }

	readonly XmlDocument? xmlDoc;
	XmlNamespaceManager? nsmgr;

	AndroidManifest (XmlDocument? doc, string? description, AndroidManifestFormat format)
	{
		xmlDoc = doc ?? throw new ArgumentNullException (nameof (doc));
		Format = format;
		Description = String.IsNullOrEmpty (description) ? "Android manifest" : description;

		nsmgr = PrepareForReading (xmlDoc);
		PackageName = TryGetPackageName (xmlDoc, nsmgr);
		MainActivity = TryGetMainActivity (xmlDoc, nsmgr);
		(MinSdkVersion, TargetSdkVersion) = TryGetSdkVersions (xmlDoc, nsmgr);
		Permissions = TryGetPermissions (xmlDoc, nsmgr);
	}

	AndroidManifest (AXMLParser binaryParser, string? description)
		: this (Read (binaryParser, description), description, AndroidManifestFormat.Binary)
	{}

	AndroidManifest (ProtoManifest.XmlNode rootNode, string? description)
		: this (Read (rootNode, description), description, AndroidManifestFormat.Protobuf)
	{}

	public static IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		var manifestState = state as AndroidManifestAspectState;
		if (manifestState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		AndroidManifest ret;
		if (manifestState.BinaryParser != null) {
			ret = new AndroidManifest (manifestState.BinaryParser, description);
		} else if (manifestState.ProtoManifestRoot != null) {
			ret = new AndroidManifest (manifestState.ProtoManifestRoot, description);
		} else {
			throw new NotImplementedException ();
		}

		return ret;
	}

	public static IAspectState ProbeAspect (Stream stream, string? description)
	{
		Log.Debug ($"Checking if '{description}' is an Android binary XML document.");

		// We leave parsing of the data to `LoadAspect`, here we only detect the format
		try {
			stream.Seek (0, SeekOrigin.Begin);

			// The constructor will throw if it cannot recognize the format
			var binaryParser = new AXMLParser (stream);

			LogKind ("Android binary XML document");
			return new AndroidManifestAspectState (binaryParser);
		} catch (Exception ex) {
			Log.Debug ($"Failed to instantiate AXML binary parser for '{description}'. Exception thrown:", ex);
		}

		Log.Debug ($"Checking if '{description}' is an plain XML document.");
		try {
			stream.Seek (0, SeekOrigin.Begin);
			XmlDocument doc = ParsePlainXML (stream);
			LogKind ("plain XML document");
			return new AndroidManifestAspectState (doc);
		} catch (Exception ex) {
			Log.Debug ($"Failed to parse '{description}' as an XML document. Exception thrown:", ex);
		}

		Log.Debug ($"Checking if '{description}' is a protobuf XML document.");
		try {
			stream.Seek (0, SeekOrigin.Begin);
			ProtoManifest.XmlNode rootNode = ProtoManifest.XmlNode.Parser.ParseFrom (stream);
			LogKind ("protobuf XML document");
			return new AndroidManifestAspectState (rootNode);
		} catch (Exception ex) {
			Log.Debug ($"Failed to parse '{description}' as a protobuf XML document. Exception thrown:", ex);
		}

		return new BasicAspectState (success: false);

		void LogKind (string kind)
		{
			Log.Debug ($"Manifest '{description}' is: {kind}");
		}
	}

	static XmlDocument? Read (ProtoManifest.XmlNode proot, string? description)
	{
		if (proot.Element == null) {
			Log.Debug ($"Manifest '{description}' protobuf has no root element");
			return null;
		}

		var doc = new XmlDocument ();
		var nsmgr = new XmlNamespaceManager (doc.NameTable);

		foreach (ProtoManifest.XmlNamespace ns in proot.Element.NamespaceDeclaration) {
			nsmgr.AddNamespace (ns.Prefix, ns.Uri);
		}

		XmlNode docRoot = ToDotNetXml (doc, nsmgr, proot);
		doc.AppendChild (docRoot);
		AddChildren (doc, nsmgr, proot, docRoot);

		return doc;
	}

	static void AddChildren (XmlDocument doc, XmlNamespaceManager nsmgr, ProtoManifest.XmlNode pnode, XmlNode parent)
	{
		ProtoManifest.XmlElement pelement = pnode.Element;
		if (pelement == null || pelement.Child.Count == 0) {
			return;
		}

		foreach (ProtoManifest.XmlNode pchild in pelement.Child) {
			XmlNode child = ToDotNetXml (doc, nsmgr, pchild);
			parent.AppendChild (child);
			AddChildren (doc, nsmgr, pchild, child);
		}
	}

	static XmlNode ToDotNetXml (XmlDocument doc, XmlNamespaceManager nsmgr, ProtoManifest.XmlNode pnode)
	{
		if (pnode.Element == null) {
			return doc.CreateTextNode (pnode.Text ?? String.Empty);
		}

		XmlElement element;
		(bool hasNamespace, bool hasPrefix) = WithNamespace (nsmgr, pnode, out string? prefix, out string? ns);
		if (!hasNamespace) {
			element = doc.CreateElement (pnode.Element.Name);
		} else {
			if (hasPrefix) {
				element = doc.CreateElement (prefix, pnode.Element.Name, ns);
			} else {
				element = doc.CreateElement (pnode.Element.Name, ns);
			}
		}

		foreach (ProtoManifest.XmlAttribute pattr in pnode.Element.Attribute) {
			(hasNamespace, hasPrefix) = WithNamespace (nsmgr, pattr, out prefix, out ns);

			XmlAttribute attr;
			if (!hasNamespace) {
				attr = doc.CreateAttribute (pattr.Name);
			} else {
				if (hasPrefix) {
					attr = doc.CreateAttribute (prefix, pattr.Name, ns);
				} else {
					attr = doc.CreateAttribute (pattr.Name, ns);
				}
			}
			attr.Value = pattr.Value;
			element.SetAttributeNode (attr);
		}

		return element;
	}

	static (bool hasNamespace, bool hasPrefix) WithNamespaceMaybe (XmlNamespaceManager nsmgr, string? ns, out string? nsOut, out string? prefix)
	{
		prefix = null;
		nsOut = ns;
		if (String.IsNullOrEmpty (ns)) {
			return (false, false);
		}

		prefix = nsmgr.LookupPrefix (ns);
		return (true, !String.IsNullOrEmpty (prefix));
	}

	static (bool hasNamespace, bool hasPrefix) WithNamespace (XmlNamespaceManager nsmgr, ProtoManifest.XmlNode protoNode, out string? prefix, out string? ns)
	{
		return WithNamespaceMaybe (nsmgr, protoNode.Element.NamespaceUri, out ns, out prefix);
	}

	static (bool hasNamespace, bool hasPrefix) WithNamespace (XmlNamespaceManager nsmgr, ProtoManifest.XmlAttribute protoAttr, out string? prefix, out string? ns)
	{
		return WithNamespaceMaybe (nsmgr, protoAttr.NamespaceUri, out ns, out prefix);
	}

	static XmlDocument? Read (AXMLParser? binaryParser, string? description)
	{
		if (binaryParser == null) {
			throw new NotImplementedException ();
		}

		XmlDocument? doc = binaryParser.Parse ();
		if (doc == null || !binaryParser.IsValid) {
			Log.Debug ($"AXML parser didn't render a valid document for '{description}'");
			return null;
		}
		Log.Debug ($"'{description}' loaded and parsed correctly.");
		return doc;
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

	static List<string>? TryGetPermissions (XmlDocument? doc, XmlNamespaceManager? nsmgr)
	{
		if (!ValidXmlContext (doc, nsmgr, out XmlElement? root)) {
			return null;
		}

		XmlNodeList? permissions = root!.SelectNodes ("//manifest/uses-permission");
		if (permissions == null || permissions.Count == 0) {
			return null;
		}

		var ret = new List<string> ();
		foreach (XmlNode permission in permissions) {
			var name = permission.Attributes?.GetNamedItem ("android:name");
			if (name == null || String.IsNullOrEmpty (name.Value)) {
				continue;
			}

			ret.Add (name.Value);
		}

		ret.Sort ();
		return ret;
	}

	static (string? minSdk, string? targetSdk) TryGetSdkVersions (XmlDocument? doc, XmlNamespaceManager? nsmgr)
	{
		if (!ValidXmlContext (doc, nsmgr, out XmlElement? root)) {
			return (null, null);
		}

		XmlNode? usesSdk = root!.SelectSingleNode ("//manifest/uses-sdk");
		if (usesSdk == null) {
			return (null, null);
		}

		var minSdkVersionAttr = usesSdk.Attributes?.GetNamedItem ("android:minSdkVersion");
		var targetSdkVersionAttr = usesSdk.Attributes?.GetNamedItem ("android:targetSdkVersion");

		return (minSdkVersionAttr?.Value, targetSdkVersionAttr?.Value);
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
				XmlNodeList? actions = intentFilter.SelectNodes ("./action", nsmgr!);
				if (actions == null || actions.Count == 0) {
					continue;
				}

				if (!HaveNodeWithNameAttribute (actions, "android.intent.action.MAIN")) {
					continue;
				}

				XmlNodeList? categories = intentFilter.SelectNodes ("./category", nsmgr!);
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
