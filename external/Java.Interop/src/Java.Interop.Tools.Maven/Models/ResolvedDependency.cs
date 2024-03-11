using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Java.Interop.Tools.Maven.Extensions;

namespace Java.Interop.Tools.Maven.Models;

public class ResolvedDependency
{
	public ResolvedProject Project { get; }
	public string ArtifactId { get; }
	public string? Classifier { get; }
	public string GroupId { get; }
	public string? Optional { get; }
	public string Scope { get; }
	public string? Type { get; }
	public string Version { get; }

	public string ArtifactString => $"{GroupId}:{ArtifactId}";
	public string VersionedArtifactString => $"{GroupId}:{ArtifactId}:{Version}";

	public ResolvedDependency (ResolvedProject project, Dependency dependency)
		: this (project, dependency, false)
	{ }

	internal ResolvedDependency (ResolvedProject project, Dependency dependency, bool shallow)
	{
		Project = project;

		// First fill these in with values from the dependency
		ArtifactId = dependency.ArtifactId.OrEmpty ();
		GroupId = dependency.GroupId.OrEmpty ();
		Classifier = dependency.Classifier;
		Optional = dependency.Optional.OrEmpty ();
		Scope = dependency.Scope.OrEmpty ();
		Type = dependency.Type;
		Version = dependency.Version.OrEmpty ();

		// If we're not shallow, fill in any still missing properties with parent values
		if (!shallow) {
			if (!Classifier.HasValue ())
				Classifier = GetInheritedProperty (this, project, d => d.Classifier);

			if (!Optional.HasValue ())
				Optional = GetInheritedProperty (this, project, d => d.Optional);

			if (!Scope.HasValue ())
				Scope = GetInheritedProperty (this, project, d => d.Scope);

			if (!Type.HasValue ())
				Type = GetInheritedProperty (this, project, d => d.Type);

			if (!Version.HasValue ())
				Version = GetInheritedProperty (this, project, d => d.Version);
		}

		// Default scope to "compile" if not specified
		if (!Scope.HasValue ())
			Scope = "compile";

		// Default optional to "false" if not specified
		if (!Optional.HasValue ())
			Optional = "false";
	}

	public override string ToString () => $"{VersionedArtifactString} - {Scope}";

	static string GetInheritedProperty (ResolvedDependency dependency, ResolvedProject project, Func<ResolvedDependency, string?> property)
	{
		// Check our <dependencyManagement> section
		if (CheckDependencyManagementSection (project, dependency, property, out var result))
			return result;

		// Check imported POMs
		foreach (var imported in project.ImportedPomProjects) {
			var value = GetInheritedProperty (dependency, imported, property);

			if (value.HasValue ())
				return value;
		}

		// Check parent POM
		if (project.Parent is not null && !project.Parent.IsSuperPom)
			return GetInheritedProperty (dependency, project.Parent, property);

		return string.Empty;
	}

	static bool CheckImportedPoms (ResolvedDependency dependency, ResolvedProject project, Func<ResolvedDependency, string?> property, [NotNullWhen (true)] out string? result)
	{
		result = null;

		foreach (var imported in project.ImportedPomProjects) {
			var imported_dep = imported.Resolved.DependencyManagement?.Dependencies.FirstOrDefault (x => x.ArtifactId == dependency.ArtifactId && x.GroupId == dependency.GroupId);

			if (imported_dep != null) {
				result = property (new ResolvedDependency (imported, imported_dep, true));

				if (result.HasValue ())
					return true;
			}

			// Recurse, as imported POMs can also import POMs
			if (CheckImportedPoms (dependency, imported, property, out result))
				return true;
		}

		return false;
	}

	static bool CheckDependencyManagementSection (ResolvedProject project, ResolvedDependency dependency, Func<ResolvedDependency, string?> property, [NotNullWhen (true)] out string? result)
	{
		result = null;

		// Check <dependencyManagement>
		var dep_man = project.Resolved.DependencyManagement?.Dependencies.FirstOrDefault (x => x.ArtifactId == dependency.ArtifactId && x.GroupId == dependency.GroupId);

		if (dep_man != null) {
			result = property (new ResolvedDependency (project, dep_man, true)) ?? string.Empty;
			return result.HasValue ();
		}

		return false;
	}
}
