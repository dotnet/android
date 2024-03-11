using Java.Interop.Tools.Maven;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven_Tests.Extensions;

namespace Java.Interop.Tools.Maven_Tests;

public class PropertySubstitutionTests
{
	[Test]
	public void Resolve_ExplicitProperty ()
	{
		// This POM:
		// <version>${mavenVersion}</version>
		// <properties>
		//   <mavenVersion>3.0</mavenVersion>
		// </properties>
		var artifact = new Artifact ("bar", "foo", "${mavenVersion}");

		var project = TestDataExtensions.CreateProject (artifact);
		project.AddProperty ("mavenVersion", "2.0.6");

		var resolver = new DefaultProjectResolver ();
		resolver.Register (project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual ("2.0.6", result.Version);
	}

	[Test]
	public void Resolve_ExplicitPropertyFromParent ()
	{
		// This POM:
		// <version>${mavenVersion}</version>
		// Parent POM:
		// <properties>
		//   <mavenVersion>3.0</mavenVersion>
		// </properties>
		var artifact = new Artifact ("bar", "foo", "${mavenVersion}");
		var parent_artifact = new Artifact ("bar-parent", "foo", "1.0");

		var parent_project = TestDataExtensions.CreateProject (parent_artifact);
		var project = TestDataExtensions.CreateProject (artifact, parent_project);

		parent_project.AddProperty ("mavenVersion", "2.0.6");

		var resolver = new DefaultProjectResolver ();
		resolver.Register (parent_project);
		resolver.Register (project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual ("2.0.6", result.Version);
	}

	[Test]
	public void Resolve_ProjectProperty ()
	{
		// This POM:
		// <groupId>bar</groupId>
		// <artifactId>foo</artifactId>
		// <version>2.0</version>
		// <name>${project.groupId}:${project.artifactId}:${project.version}</name>
		var artifact = new Artifact ("bar", "foo", "2.0");

		var project = TestDataExtensions.CreateProject (artifact);
		project.Name = "${project.groupId}:${project.artifactId}:${project.version}";

		var resolver = new DefaultProjectResolver ();
		resolver.Register (project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual ("bar:foo:2.0", result.Name);
	}

	[Test]
	public void Resolve_ProjectPropertyFromParent ()
	{
		// This POM:
		// <groupId>bar</groupId>
		// <artifactId>foo</artifactId>
		// <name>${project.groupId}:${project.artifactId}:${project.version}</name>
		// Parent POM:
		// <version>2.0</version>
		var artifact = new Artifact ("bar", "foo", "");
		var parent_artifact = new Artifact ("bar-parent", "foo", "2.0");

		var parent_project = TestDataExtensions.CreateProject (parent_artifact);
		var project = TestDataExtensions.CreateProject (artifact, parent_project);

		project.Name = "${project.groupId}:${project.artifactId}:${project.version}";

		var resolver = new DefaultProjectResolver ();
		resolver.Register (parent_project);
		resolver.Register (project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual ("bar:foo:2.0", result.Name);
	}

	[Test]
	public void Resolve_RecursiveProperties ()
	{
		// This POM:
		// <groupId>bar</groupId>
		// <artifactId>foo</artifactId>
		// <version>${mavenVersion}</version>
		// Parent POM:
		// <version>2.0</version>
		// <properties>
		//   <mavenVersion>${project.version}</mavenVersion>
		// </properties>
		var artifact = new Artifact ("bar", "foo", "${mavenVersion}");
		var parent_artifact = new Artifact ("bar-parent", "foo", "2.0");

		var parent_project = TestDataExtensions.CreateProject (parent_artifact);
		var project = TestDataExtensions.CreateProject (artifact, parent_project);

		parent_project.AddProperty ("mavenVersion", "${project.version}");

		var resolver = new DefaultProjectResolver ();
		resolver.Register (parent_project);
		resolver.Register (project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual ("2.0", result.Version);
	}

	[Test]
	public void Resolve_ChildPropertiesTakePrecedenceOverParentProperties ()
	{
		// This POM:
		// <properties>
		//   <mavenVersion>2.0</mavenVersion>
		// </properties>
		// Parent POM:
		// <version>${mavenVersion}</version>
		// <properties>
		//   <mavenVersion>1.0</mavenVersion>
		// </properties>
		var artifact = new Artifact ("bar", "foo", "");
		var parent_artifact = new Artifact ("bar-parent", "foo", "${mavenVersion}");

		var parent_project = TestDataExtensions.CreateProject (parent_artifact);
		var project = TestDataExtensions.CreateProject (artifact, parent_project);

		project.AddProperty ("mavenVersion", "2.0");
		parent_project.AddProperty ("mavenVersion", "1.0");

		var resolver = new DefaultProjectResolver ();
		resolver.Register (parent_project);
		resolver.Register (project);

		var result = ResolvedProject.FromArtifact (artifact, resolver);

		Assert.AreEqual ("2.0", result.Version);
	}
}
