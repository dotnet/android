using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed class ManifestDocument
{
	internal ManifestElement? Root { get; }

	internal ManifestDocument (ManifestElement? root)
	{
		Root = root;
	}

	public static ManifestDocument CreateDefault (string packageName)
	{
		var root = new ManifestElement ("manifest");
		root.SetNamespaceDeclaration ("android", ManifestConstants.AndroidNamespace);
		root.SetAttribute ("package", packageName);
		return new ManifestDocument (root);
	}

	public static ManifestDocument Load (string file)
	{
		using var stream = File.OpenRead (file);
		return Load (stream);
	}

	public static ManifestDocument Parse (string xml)
	{
		using var reader = new StringReader (xml);
		using var xmlReader = CreateReader (reader);
		return Load (xmlReader);
	}

	public static ManifestDocument Load (Stream stream)
	{
		using var reader = CreateReader (stream);
		return Load (reader);
	}

	static XmlReader CreateReader (Stream stream)
	{
		return XmlReader.Create (stream, new XmlReaderSettings {
			DtdProcessing = DtdProcessing.Ignore,
			IgnoreWhitespace = true,
		});
	}

	static XmlReader CreateReader (TextReader reader)
	{
		return XmlReader.Create (reader, new XmlReaderSettings {
			DtdProcessing = DtdProcessing.Ignore,
			IgnoreWhitespace = true,
		});
	}

	static ManifestDocument Load (XmlReader reader)
	{
		ManifestElement? root = null;
		while (reader.Read ()) {
			if (reader.NodeType == XmlNodeType.Element) {
				root = ReadElement (reader);
				break;
			}
		}
		return new ManifestDocument (root);
	}

	static ManifestElement ReadElement (XmlReader reader)
	{
		var element = new ManifestElement (reader.LocalName, reader.NamespaceURI, reader.Prefix);
		bool isEmpty = reader.IsEmptyElement;

		if (reader.MoveToFirstAttribute ()) {
			do {
				element.AddAttribute (new ManifestAttribute (reader.LocalName, reader.Value, reader.NamespaceURI, reader.Prefix));
			} while (reader.MoveToNextAttribute ());
			reader.MoveToElement ();
		}

		if (isEmpty) {
			return element;
		}

		while (reader.Read ()) {
			switch (reader.NodeType) {
			case XmlNodeType.Element:
				element.Add (ReadElement (reader));
				break;
			case XmlNodeType.Text:
				element.Add (new ManifestText (reader.Value, false));
				break;
			case XmlNodeType.CDATA:
				element.Add (new ManifestText (reader.Value, true));
				break;
			case XmlNodeType.Comment:
				element.Add (new ManifestComment (reader.Value));
				break;
			case XmlNodeType.EndElement:
				return element;
			}
		}

		return element;
	}

	public ManifestDocument Clone ()
	{
		return new ManifestDocument (Root?.Clone ());
	}

	public void Save (Stream stream)
	{
		var settings = new XmlWriterSettings {
			Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
			Indent = true,
		};
		using var writer = XmlWriter.Create (stream, settings);
		writer.WriteStartDocument ();
		if (Root is not null) {
			Root.WriteTo (writer);
		}
		writer.WriteEndDocument ();
	}

	public string ToXmlString ()
	{
		using var stream = new MemoryStream ();
		Save (stream);
		return Encoding.UTF8.GetString (stream.ToArray ());
	}
}

abstract class ManifestNode
{
	public abstract ManifestNode CloneNode ();
	internal abstract void WriteTo (XmlWriter writer);
}

sealed class ManifestText : ManifestNode
{
	readonly string value;
	readonly bool cdata;

	public ManifestText (string value, bool cdata)
	{
		this.value = value;
		this.cdata = cdata;
	}

	public override ManifestNode CloneNode () => new ManifestText (value, cdata);

	internal override void WriteTo (XmlWriter writer)
	{
		if (cdata) {
			writer.WriteCData (value);
		} else {
			writer.WriteString (value);
		}
	}
}

sealed class ManifestComment : ManifestNode
{
	readonly string value;

	public ManifestComment (string value)
	{
		this.value = value;
	}

	public override ManifestNode CloneNode () => new ManifestComment (value);

	internal override void WriteTo (XmlWriter writer)
	{
		writer.WriteComment (value);
	}
}

sealed class ManifestElement : ManifestNode
{
	readonly List<ManifestAttribute> attributes = [];
	readonly List<ManifestNode> children = [];

	public string LocalName { get; }
	public string NamespaceName { get; }
	public string Prefix { get; }

	public ManifestElement (string localName, string namespaceName = "", string prefix = "")
	{
		LocalName = localName;
		NamespaceName = namespaceName;
		Prefix = prefix;
	}

	public IReadOnlyList<ManifestAttribute> Attributes => attributes;

	public void AddAttribute (ManifestAttribute attribute)
	{
		attributes.Add (attribute);
	}

	public string? Attribute (string localName)
	{
		return Attribute (namespaceName: "", localName);
	}

	public string? AndroidAttribute (string localName)
	{
		return Attribute (ManifestConstants.AndroidNamespace, localName);
	}

	public string? Attribute (string namespaceName, string localName)
	{
		return attributes.FirstOrDefault (a => a.NamespaceName == namespaceName && a.LocalName == localName)?.Value;
	}

	public bool HasAttribute (string namespaceName, string localName)
	{
		return attributes.Any (a => a.NamespaceName == namespaceName && a.LocalName == localName);
	}

	public void SetAttribute (string localName, string value)
	{
		SetAttribute ("", "", localName, value);
	}

	public void SetAndroidAttribute (string localName, string value)
	{
		SetAttribute ("android", ManifestConstants.AndroidNamespace, localName, value);
	}

	public void SetNamespaceDeclaration (string prefix, string value)
	{
		foreach (var attribute in attributes) {
			if (attribute.IsNamespaceDeclaration && attribute.LocalName == prefix) {
				attribute.Value = value;
				return;
			}
		}
		attributes.Insert (0, new ManifestAttribute (prefix, value, ManifestConstants.XmlnsNamespace, "xmlns"));
	}

	void SetAttribute (string prefix, string namespaceName, string localName, string value)
	{
		foreach (var attribute in attributes) {
			if (attribute.NamespaceName == namespaceName && attribute.LocalName == localName) {
				attribute.Value = value;
				return;
			}
		}
		attributes.Add (new ManifestAttribute (localName, value, namespaceName, prefix));
	}

	public void Add (ManifestNode node)
	{
		children.Add (node);
	}

	public void AddFirst (ManifestNode node)
	{
		children.Insert (0, node);
	}

	public IEnumerable<ManifestElement> Elements ()
	{
		return children.OfType<ManifestElement> ();
	}

	public IEnumerable<ManifestElement> Elements (string localName)
	{
		return Elements ().Where (e => e.NamespaceName.Length == 0 && e.LocalName == localName);
	}

	public ManifestElement? Element (string localName)
	{
		return Elements (localName).FirstOrDefault ();
	}

	public IEnumerable<ManifestElement> Descendants ()
	{
		foreach (var child in children.OfType<ManifestElement> ()) {
			yield return child;
			foreach (var descendant in child.Descendants ()) {
				yield return descendant;
			}
		}
	}

	public IEnumerable<ManifestElement> DescendantsAndSelf ()
	{
		yield return this;
		foreach (var descendant in Descendants ()) {
			yield return descendant;
		}
	}

	public ManifestElement Clone ()
	{
		var clone = new ManifestElement (LocalName, NamespaceName, Prefix);
		foreach (var attribute in attributes) {
			clone.attributes.Add (attribute.Clone ());
		}
		foreach (var child in children) {
			clone.children.Add (child.CloneNode ());
		}
		return clone;
	}

	public override ManifestNode CloneNode () => Clone ();

	internal override void WriteTo (XmlWriter writer)
	{
		writer.WriteStartElement (Prefix.Length == 0 ? null : Prefix, LocalName, NamespaceName.Length == 0 ? null : NamespaceName);
		foreach (var attribute in attributes) {
			attribute.WriteTo (writer);
		}
		foreach (var child in children) {
			child.WriteTo (writer);
		}
		writer.WriteEndElement ();
	}
}

sealed class ManifestAttribute
{
	public string LocalName { get; }
	public string NamespaceName { get; }
	public string Prefix { get; }
	public string Value { get; set; }

	public ManifestAttribute (string localName, string value, string namespaceName = "", string prefix = "")
	{
		LocalName = localName;
		NamespaceName = namespaceName;
		Prefix = prefix;
		Value = value;
	}

	public bool IsNamespaceDeclaration => NamespaceName == ManifestConstants.XmlnsNamespace || Prefix == "xmlns";

	public ManifestAttribute Clone () => new ManifestAttribute (LocalName, Value, NamespaceName, Prefix);

	public void WriteTo (XmlWriter writer)
	{
		if (IsNamespaceDeclaration) {
			if (Prefix.Length == 0 && LocalName == "xmlns") {
				writer.WriteAttributeString ("", "xmlns", ManifestConstants.XmlnsNamespace, Value);
			} else {
				writer.WriteAttributeString ("xmlns", LocalName, ManifestConstants.XmlnsNamespace, Value);
			}
		} else {
			writer.WriteAttributeString (Prefix.Length == 0 ? null : Prefix, LocalName, NamespaceName.Length == 0 ? null : NamespaceName, Value);
		}
	}
}
