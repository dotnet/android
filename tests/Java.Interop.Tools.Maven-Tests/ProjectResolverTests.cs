using System;
using Java.Interop.Tools.Maven;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven_Tests.Extensions;

namespace Java.Interop.Tools.Maven_Tests;

public class ProjectResolverTests
{
	[Test]
	public void ResolveRawProject_Success ()
	{
		var artifact = new Artifact ("bar", "foo", "1.0");
		var project = TestDataExtensions.CreateProject (artifact);
		var resolver = new DefaultProjectResolver ();

		resolver.Register (project);

		var result = resolver.Resolve (artifact);

		Assert.AreEqual (project, result);
	}

	[Test]
	public void ResolveRawProject_PomNotFound ()
	{
		var resolver = new DefaultProjectResolver ();

		Assert.Throws<InvalidOperationException> (() => resolver.Resolve (new Artifact ("bar", "foo", "1.0")));
	}

	[Test]
	public void Resolve_Success ()
	{
		var artifact = new Artifact ("bar", "foo", "1.0");
		var parent_artifact = new Artifact ("bar-parent", "foo", "1.0");

		var parent_project = TestDataExtensions.CreateProject (parent_artifact);
		var project = TestDataExtensions.CreateProject (artifact, parent_project);

		var resolver = new DefaultProjectResolver ();

		resolver.Register (project);
		resolver.Register (parent_project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual (project, result.Raw);
		Assert.AreEqual (parent_project, result.Parent.Raw);
	}

	[Test]
	public void Resolve_ParentPomNotFound ()
	{
		var artifact = new Artifact ("bar", "foo", "1.0");
		var parent_artifact = new Artifact ("bar-parent", "foo", "1.0");

		var parent_project = TestDataExtensions.CreateProject (parent_artifact);
		var project = TestDataExtensions.CreateProject (artifact, parent_project);

		var resolver = new DefaultProjectResolver ();

		// Note we are not adding the parent project to the resolver, so it will not be found
		resolver.Register (project);

		Assert.Throws<InvalidOperationException> (() => ResolvedProject.FromArtifact (artifact, resolver));
	}

	[Test]
	public void Resolve_PomNotFound ()
	{
		var resolver = new DefaultProjectResolver ();

		Assert.Throws<InvalidOperationException> (() => resolver.Resolve (new Artifact ("bar", "foo", "1.0")));
	}
}
