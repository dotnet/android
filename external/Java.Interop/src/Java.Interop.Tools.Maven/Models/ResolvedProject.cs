using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Java.Interop.Tools.Maven.Extensions;

namespace Java.Interop.Tools.Maven.Models;

public class ResolvedProject
{
	readonly ResolvedProject? parent;
	readonly IProjectResolver? resolver;

	Project? resolved_project;

	public Project Raw { get; }
	public Project Resolved => resolved_project ?? throw new InvalidOperationException ("Call the Resolve method before accessing this.");

	public virtual bool IsSuperPom => false;
	public string ArtifactId => Resolved.ArtifactId.OrEmpty ();
	public string GroupId => Resolved.GroupId.HasValue () || IsSuperPom ? Resolved.GroupId.OrEmpty () : Parent.GroupId;
	public string Version => Resolved.Version.HasValue () || IsSuperPom ? Resolved.Version.OrEmpty () : Parent.Version;
	public string Name => Resolved.Name.HasValue () || IsSuperPom ? Resolved.Name.OrEmpty () : Parent.Name;

	public List<ResolvedDependency> Dependencies { get; } = new List<ResolvedDependency> ();
	public List<ResolvedProject> ImportedPomProjects { get; } = new (); // Projects imported via a scope = "import", type = "pom" dependencyManagement section

	public ResolvedProject Parent {
		get {
			if (parent is null && IsSuperPom)
				throw new InvalidOperationException ("Super POM does not have a parent, check IsSuperPom before calling");

			return parent ?? throw new InvalidOperationException ("Parent should not be null");
		}
	}

	public ResolvedProject (Project project, ResolvedProject parent, IProjectResolver resolver)
	{
		Raw = project;
		this.parent = parent;
		this.resolver = resolver;
	}

	public void Resolve () => ResolveCore (new PropertyStack ());

	public static ResolvedProject FromArtifact (Artifact artifact, IProjectResolver resolver)
	{
		var project = FromArtifactCore (artifact, resolver);
		project.Resolve ();

		return project;
	}

	static ResolvedProject FromArtifactCore (Artifact artifact, IProjectResolver resolver)
	{
		var raw = resolver.Resolve (artifact);

		// POM has a parent, resolve it
		if (raw.TryGetParentPomArtifact (out var parentArtifact)) {
			var parent = FromArtifactCore (parentArtifact, resolver);
			return new ResolvedProject (raw, parent, resolver);
		}

		var project = new ResolvedProject (raw, SuperPom.Instance, resolver);

		return project;
	}

	void ResolveCore (PropertyStack properties)
	{
		if (IsSuperPom)
			return;

		// A newly constructed ResolvedProject contains only raw values. We need to
		// go through every Project value and replace any specified properties
		// (ex: ${project.version} or ${mavenVersion}) with the resolved values.  This has to
		// start at the child and work its way up to the parent, because properties
		// specified in the child override those in the parent.
		var xml = Raw.ToXml ();
		xml = ReplaceProperties (xml, this, properties);

		resolved_project = Project.Parse (xml);

		properties.Push (Raw.Properties);
		parent?.ResolveCore (properties);

		// Now that we've resolved all properties, we can figure out our dependencies.
		ImportDependencyManagementPoms ();
		ResolveDependencies ();
	}

	[return: NotNullIfNotNull (nameof (value))]
	string? ReplaceProperties (string? value, ResolvedProject project, PropertyStack properties)
	{
		if (value is null)
			return null;

		if (!value.Contains ("${") || project.IsSuperPom)
			return value;

		properties.Push (project.Raw.Properties);

		var old_value = string.Empty;

		// Properties can be nested, so we need to keep replacing until we don't find any more.
		// We check against the old value to make sure we don't get stuck in an infinite loop.
		while (value.Contains ("${") && value != old_value) {
			old_value = value;

			// Replace ${project.*} properties
			value = ReplaceProjectProperties (value, project);

			// Replace explicit <property> properties
			value = properties.Apply (value);
		}

		value = ReplaceProperties (value, project.Parent, properties);
		properties.Pop ();

		return value;
	}

	string ReplaceProjectProperties (string value, ResolvedProject project)
	{
		// Technically this can be any element in the XML, but we're only going to suppport
		// some common ones for now to keep things simple.
		if (project.Raw.GroupId.HasValue ())
			value = value.Replace ("${project.groupId}", project.Raw.GroupId);

		if (project.Raw.ArtifactId.HasValue ())
			value = value.Replace ("${project.artifactId}", project.Raw.ArtifactId);

		if (project.Raw.Version.HasValue ())
			value = value.Replace ("${project.version}", project.Raw.Version);

		if (project.Raw.Name.HasValue ())
			value = value.Replace ("${project.name}", project.Raw.Name);

		if (project.Raw.Parent?.Version.HasValue () == true)
			value = value.Replace ("${project.parent.version}", project.Raw.Parent.Version);

		if (project.Raw.Parent?.GroupId.HasValue () == true)
			value = value.Replace ("${project.parent.groupId}", project.Raw.Parent.GroupId);

		return value;
	}

	void ImportDependencyManagementPoms ()
	{
		if (resolver is null)
			return;

		foreach (var pom_import in GetPomImportDependencies ()) {
			var pom = FromArtifact (pom_import.ToArtifact (), resolver);
			pom.Resolve ();
			ImportedPomProjects.Add (pom);
		}
	}

	IEnumerable<Dependency> GetPomImportDependencies ()
		=> Resolved.DependencyManagement?.Dependencies.Where (x => x.Type == "pom" && x.Scope == "import") ?? Array.Empty<Dependency> ();

	void ResolveDependencies ()
	{
		// Add _our_ specified dependencies
		foreach (var dependency in Resolved.Dependencies)
			Dependencies.Add (new ResolvedDependency (this, dependency));

		// Add dependencies from our parent (the null check is for the super POM)
		if (parent is ResolvedProject rp && !rp.IsSuperPom)
			foreach (var dependency in parent!.Dependencies)
				Dependencies.Add (dependency);
	}
}

// Sentinel class for the super POM, which every POM implicitly inherits if it doesn't specify a parent
public class SuperPom : ResolvedProject
{
	SuperPom () : base (new Project (), null!, null!)
	{
	}

	public override bool IsSuperPom => true;

	public static SuperPom Instance { get; } = new SuperPom ();
}
