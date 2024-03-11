using System;
using System.Collections.Generic;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven;

public class DefaultProjectResolver : IProjectResolver
{
	readonly Dictionary<string, Project> poms = new ();

	public void Register (Project project)
	{
		poms.Add (project.VersionedArtifactString, project);
	}

	public virtual Project Resolve (Artifact artifact)
	{
		if (poms.TryGetValue (artifact.VersionedArtifactString, out var project))
			return project;

		throw new InvalidOperationException ($"No POM registered for {artifact}");
	}
}
