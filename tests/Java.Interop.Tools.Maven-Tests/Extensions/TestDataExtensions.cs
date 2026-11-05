using System.Xml.Linq;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven_Tests.Extensions;

static class TestDataExtensions
{
	public static Project CreateProject (Artifact artifact, Project? parent = null)
	{
		var xml = new XDocument (
			new XElement ("project",
				new XElement ("modelVersion", "4.0.0"),
				new XElement ("groupId", artifact.GroupId),
				new XElement ("artifactId", artifact.Id),
				new XElement ("version", artifact.Version)
				)
			);

		if (parent is not null) {
			var parent_xml = new XElement ("parent",
				new XElement ("groupId", parent.GroupId),
				new XElement ("artifactId", parent.ArtifactId),
				new XElement ("version", parent.Version)
			);

			xml.Root!.Add (parent_xml);
		}

		return Project.Parse (xml.ToString ());
	}

	public static void AddProperty (this Project project, string key, string value)
	{
		var xml = new XElement (key, value);

		project.Properties ??= new ModelProperties ();
		project.Properties.Any.Add (xml);
	}
}
