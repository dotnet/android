using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Java.Interop.Tools.Maven.Extensions;

namespace Java.Interop.Tools.Maven.Models;

public partial class Project
{
	static readonly XmlSerializer xml_serializer = new (typeof (Project));

	public static Project Load (Stream stream)
	{
		using (var xr = new XmlTextReader (stream) { Namespaces = false })
			return Load (xr);
	}

	public static Project Load (XmlReader reader)
		=> (Project) xml_serializer.Deserialize (reader);

	public static Project Parse (string xml)
	{
		using (var sr = new StringReader (xml))
		using (var xr = new XmlTextReader (sr) { Namespaces = false })
			return Load (xr);
	}

	public bool TryGetParentPomArtifact ([NotNullWhen (true)] out Artifact? parent)
	{
		parent = null;

		if (Parent is not null) {
			parent = new Artifact (Parent.GroupId.OrEmpty (), Parent.ArtifactId.OrEmpty (), Parent.Version.OrEmpty ());
			return true;
		}

		return false;
	}

	public override string ToString () => VersionedArtifactString;

	public string ToXml ()
	{
		var serializer = new XmlSerializer (typeof (Project));

		using (var sw = new StringWriter ()) {
			serializer.Serialize (sw, this);
			return sw.ToString ();
		}
	}

	// Format should match Artifact.VersionedArtifactString for comparisons.
	public string VersionedArtifactString => $"{GroupId}:{ArtifactId}:{Version}";
}
