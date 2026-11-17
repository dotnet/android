using Java.Interop.Tools.Maven.Extensions;

namespace Java.Interop.Tools.Maven.Models;

public partial class Dependency
{
	public Artifact ToArtifact ()
		=> new Artifact (GroupId.OrEmpty (), ArtifactId.OrEmpty (), Version.OrEmpty ());
}
