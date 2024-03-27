#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Java.Interop.Tools.Maven;
using Java.Interop.Tools.Maven.Models;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.ProjectModel;

namespace Xamarin.Android.Tasks;

public class JavaDependencyVerification : AndroidTask
{
	public override string TaskPrefix => "JDV";

	/// <summary>
	/// Java libraries whose dependencies we are being asked to verify.
	/// </summary>
	public ITaskItem []? AndroidLibraries { get; set; }

	/// <summary>
	/// Additional POM files (like parent POMs) that we should use to resolve dependencies.
	/// </summary>
	public ITaskItem []? AdditionalManifests { get; set; }

	/// <summary>
	/// NuGet packages this project consumes, which may fulfill Java dependencies.
	/// </summary>
	public ITaskItem []? PackageReferences { get; set; }

	/// <summary>
	/// Projects this project references, which may fulfill Java dependencies.
	/// </summary>
	public ITaskItem []? ProjectReferences { get; set; }

	/// <summary>
	/// Dependencies that we should ignore if they are missing.
	/// </summary>
	public ITaskItem []? IgnoredDependencies { get; set; }

	/// <summary>
	/// The file location of 'microsoft-packages.json'.
	/// </summary>
	public string? MicrosoftPackagesFile { get; set; }

	public string? ProjectAssetsLockFile { get; set; }

	public override bool RunTask ()
	{
		// Bail if no <AndroidLibrary> specifies a "Manifest" we need to verify
		if (!(AndroidLibraries?.Select (al => al.GetMetadata ("Manifest")).Any (al => al.HasValue ()) ?? false))
			return true;

		// Populate the POM resolver with the POMs we know about
		var pom_resolver = new MSBuildLoggingPomResolver (Log);
		var poms_to_verify = new List<Artifact> ();

		foreach (var pom in AndroidLibraries ?? [])
			if (pom_resolver.RegisterFromAndroidLibrary (pom) is Artifact art)
				poms_to_verify.Add (art);

		foreach (var pom in AdditionalManifests ?? [])
			pom_resolver.RegisterFromAndroidAdditionalJavaManifest (pom);

		// If there were errors loading the requested POMs, bail
		if (Log.HasLoggedErrors)
			return false;

		// Populate the dependency resolver with every dependency we know about
		var resolver = new DependencyResolver (ProjectAssetsLockFile, Log);

		resolver.AddAndroidLibraries (AndroidLibraries);
		resolver.AddPackageReferences (PackageReferences);
		resolver.AddProjectReferences (ProjectReferences);
		resolver.AddIgnoredDependencies (IgnoredDependencies);

		// Parse microsoft-packages.json so we can provide package recommendations
		var ms_packages = new MicrosoftNuGetPackageFinder (MicrosoftPackagesFile, Log);

		// Verify dependencies
		foreach (var pom in poms_to_verify) {
			if (TryResolveProject (pom, pom_resolver, out var resolved_pom)) {
				foreach (var dependency in resolved_pom.Dependencies.Where (d => (d.IsRuntimeDependency () || d.IsCompileDependency ()) && !d.IsOptional ()))
					resolver.EnsureDependencySatisfied (dependency, ms_packages);
			}
		}

		return !Log.HasLoggedErrors;
	}

	static bool TryResolveProject (Artifact artifact, IProjectResolver resolver, [NotNullWhen (true)]out ResolvedProject? project)
	{
		// ResolvedProject.FromArtifact will throw if a POM cannot be resolved, but our MSBuildLoggingPomResolver
		// has already logged the failure as an MSBuild error.  We don't want to log it again as an unhandled exception.
		try {
			project = ResolvedProject.FromArtifact (artifact, resolver);
			return true;
		} catch {
			project = null;
			return false;
		}
	}
}

class DependencyResolver
{
	readonly Dictionary<string, Artifact> artifacts = new ();

	readonly NuGetPackageVersionFinder? finder;
	readonly TaskLoggingHelper log;

	public DependencyResolver (string? lockFile, TaskLoggingHelper log)
	{
		this.log = log;

		if (File.Exists (lockFile))
			finder = NuGetPackageVersionFinder.Create (lockFile!, log);
	}

	public bool EnsureDependencySatisfied (ResolvedDependency dependency, MicrosoftNuGetPackageFinder packages)
	{
		if (!dependency.Version.HasValue ())
			log.LogMessage ("Could not determine required version of Java dependency '{0}:{1}'. Validation of this dependency will not take version into account.", dependency.GroupId, dependency.ArtifactId);

		var satisfied = TrySatisfyDependency (dependency);

		if (satisfied)
			return true;

		var suggestion = packages.GetNuGetPackage ($"{dependency.GroupId}:{dependency.ArtifactId}");
		var artifact_spec = dependency.Version.HasValue () ? dependency.VersionedArtifactString : dependency.ArtifactString;

		if (suggestion is string nuget)
			log.LogCodedError ("XA4242", Properties.Resources.XA4242, artifact_spec, nuget);
		else
			log.LogCodedError ("XA4241", Properties.Resources.XA4241, artifact_spec);

		return false;
	}

	public void AddAndroidLibraries (ITaskItem []? tasks)
	{
		foreach (var task in tasks.OrEmpty ()) {
			var id = task.GetMetadataOrDefault ("JavaArtifact", string.Empty);
			var version = task.GetMetadataOrDefault ("JavaVersion", string.Empty);

			// TODO: Should raise an error if JavaArtifact is specified but JavaVersion is not
			if (!id.HasValue () || !version.HasValue ())
				continue;

			if (version != null && MavenExtensions.TryParseArtifactWithVersion (id, version, log, out var art)) {
				log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from AndroidLibrary '{3}'", art.GroupId, art.Id, art.Version, task.ItemSpec);
				artifacts.Add (art.ArtifactString, art);
			}
		}
	}

	public void AddPackageReferences (ITaskItem []? tasks)
	{
		foreach (var task in tasks.OrEmpty ()) {

			// See if JavaArtifact/JavaVersion overrides were used
			if (task.TryParseJavaArtifactAndJavaVersion ("PackageReference", log, out var explicit_artifact, out var attributes_specified)) {
				artifacts.Add (explicit_artifact.ArtifactString, explicit_artifact);
				continue;
			}

			// If user tried to specify JavaArtifact or JavaVersion, but did it incorrectly, we do not perform any fallback
			if (attributes_specified)
				continue;

			// Try parsing the NuGet metadata for Java version information instead
			var artifact = finder?.GetJavaInformation (task.ItemSpec, task.GetMetadataOrDefault ("Version", string.Empty), log);

			if (artifact != null) {
				log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from PackageReference '{3}'", artifact.GroupId, artifact.Id, artifact.Version, task.ItemSpec);
				artifacts.Add (artifact.ArtifactString, artifact);

				continue;
			}

			log.LogMessage ("No Java artifact information found for PackageReference '{0}'", task.ItemSpec);
		}
	}

	public void AddProjectReferences (ITaskItem []? tasks)
	{
		foreach (var task in tasks.OrEmpty ()) {
			// See if JavaArtifact/JavaVersion overrides were used
			if (task.TryParseJavaArtifactAndJavaVersion ("ProjectReference", log, out var explicit_artifact, out var attributes_specified)) {
				artifacts.Add (explicit_artifact.ArtifactString, explicit_artifact);
				continue;
			}

			// If user tried to specify JavaArtifact or JavaVersion, but did it incorrectly, we do not perform any fallback
			if (attributes_specified)
				continue;

			// There currently is no alternate way to figure this out. Perhaps in
			// the future we could somehow parse the project to find it automatically?
		}
	}

	public void AddIgnoredDependencies (ITaskItem []? tasks)
	{
		foreach (var task in tasks.OrEmpty ()) {
			var id = task.ItemSpec;
			var version = task.GetRequiredMetadata ("AndroidIgnoredJavaDependency", "Version", log);

			if (version is null)
				continue;

			if (version != null && MavenExtensions.TryParseArtifactWithVersion (id, version, log, out var art)) {
				log.LogMessage ("Ignoring Java dependency '{0}:{1}' version '{2}'", art.GroupId, art.Id, art.Version);
				artifacts.Add (art.ArtifactString, art);
			}
		}
	}

	bool TrySatisfyDependency (ResolvedDependency dependency)
	{
		if (!dependency.Version.HasValue ())
			return artifacts.ContainsKey (dependency.ArtifactString);

		var dep_versions = MavenVersionRange.Parse (dependency.Version);

		if (artifacts.TryGetValue (dependency.ArtifactString, out var artifact))
			return dep_versions.Any (r => r.ContainsVersion (MavenVersion.Parse (artifact.Version)));

		return false;
	}
}

class MSBuildLoggingPomResolver : IProjectResolver
{
	readonly TaskLoggingHelper logger;
	readonly Dictionary<string, Project> poms = new ();

	public MSBuildLoggingPomResolver (TaskLoggingHelper logger)
	{
		this.logger = logger;
	}

	public Artifact? RegisterFromAndroidLibrary (ITaskItem item)
	{
		var pom_file = item.GetMetadata ("Manifest");

		if (!pom_file.HasValue ())
			return null;

		return RegisterFromTaskItem (item, "AndroidLibrary", pom_file);
	}

	public Artifact? RegisterFromAndroidAdditionalJavaManifest (ITaskItem item)
		=> RegisterFromTaskItem (item, "AndroidAdditionalJavaManifest", item.ItemSpec);

	Artifact? RegisterFromTaskItem (ITaskItem item, string itemName, string filename)
	{
		item.TryParseJavaArtifactAndJavaVersion (itemName, logger, out var artifact, out var _);

		if (!File.Exists (filename)) {
			logger.LogCodedError ("XA4245", Properties.Resources.XA4245, filename);
			return null;
		}

		try {
			using (var file = File.OpenRead (filename)) {
				var project = Project.Load (file);
				var registered_artifact = Artifact.Parse (project.VersionedArtifactString);

				// Return the registered artifact, preferring any overrides specified in the task item
				var final_artifact = new Artifact (
					artifact?.GroupId ?? registered_artifact.GroupId,
					artifact?.Id ?? registered_artifact.Id,
					artifact?.Version ?? registered_artifact.Version
				);

				// Use index instead of Add to handle duplicates
				poms [final_artifact.VersionedArtifactString] = project;

				logger.LogDebugMessage ("Registered POM for artifact '{0}' from '{1}'", final_artifact, filename);

				return final_artifact;
			}
		} catch (Exception ex) {
			logger.LogCodedError ("XA4246", Properties.Resources.XA4246, filename, ex.Message);
			return null;
		}
	}

	public Project Resolve (Artifact artifact)
	{
		if (poms.TryGetValue (artifact.VersionedArtifactString, out var project))
			return project;

		logger.LogCodedError ("XA4247", Properties.Resources.XA4247, artifact);

		throw new InvalidOperationException ($"No POM registered for {artifact}");
	}
}

class MicrosoftNuGetPackageFinder
{
	readonly PackageListFile? package_list;

	public MicrosoftNuGetPackageFinder (string? file, TaskLoggingHelper log)
	{
		if (file is null || !File.Exists (file)) {
			log.LogMessage ("'microsoft-packages.json' file not found, Android NuGet suggestions will not be provided");
			return;
		}

		try {
			var json = File.ReadAllText (file);
			package_list = JsonConvert.DeserializeObject<PackageListFile> (json);
		} catch (Exception ex) {
			log.LogMessage ("There was an error reading 'microsoft-packages.json', Android NuGet suggestions will not be provided: {0}", ex);
		}
	}

	public string? GetNuGetPackage (string javaId)
	{
		return package_list?.Packages?.FirstOrDefault (p => p.JavaId?.Equals (javaId, StringComparison.OrdinalIgnoreCase) == true)?.NuGetId;
	}

	public class PackageListFile
	{
		[JsonProperty ("packages")]
		public List<Package>? Packages { get; set; }
	}

	public class Package
	{
		[JsonProperty ("javaId")]
		public string? JavaId { get; set; }

		[JsonProperty ("nugetId")]
		public string? NuGetId { get; set; }
	}
}

public class NuGetPackageVersionFinder
{
	readonly LockFile lock_file;
	readonly Dictionary<string, Artifact> cache = new Dictionary<string, Artifact> ();
	readonly Regex tag = new Regex ("artifact_versioned=(?<GroupId>.+)?:(?<ArtifactId>.+?):(?<Version>.+)\\s?", RegexOptions.Compiled);
	readonly Regex tag2 = new Regex ("artifact=(?<GroupId>.+)?:(?<ArtifactId>.+?):(?<Version>.+)\\s?", RegexOptions.Compiled);

	NuGetPackageVersionFinder (LockFile lockFile)
	{
		lock_file = lockFile;
	}

	public static NuGetPackageVersionFinder? Create (string filename, TaskLoggingHelper log)
	{
		try {
			var lock_file_format = new LockFileFormat ();
			var lock_file = lock_file_format.Read (filename);
			return new NuGetPackageVersionFinder (lock_file);
		} catch (Exception e) {
			log.LogMessage ("Could not parse NuGet lock file. Java dependencies fulfilled by NuGet packages may not be available: '{0}'.", e.Message);
			return null;
		}
	}

	public Artifact? GetJavaInformation (string library, string version, TaskLoggingHelper log)
	{
		// Check if we already have this one in the cache
		var dictionary_key = $"{library.ToLowerInvariant ()}:{version}";

		if (cache.TryGetValue (dictionary_key, out var artifact))
			return artifact;

		// Find the LockFileLibrary
		var nuget = lock_file.GetLibrary (library, new NuGet.Versioning.NuGetVersion (version));

		if (nuget is null) {
			log.LogCodedError ("XA4248", Properties.Resources.XA4248, library, version);
			return null;
		}

		foreach (var path in lock_file.PackageFolders)
			if (CheckFilePath (path.Path, nuget) is Artifact art) {
				cache.Add (dictionary_key, art);
				return art;
			}

		return null;
	}

	Artifact? CheckFilePath (string nugetPackagePath, LockFileLibrary package)
	{
		// Check NuGet tags
		var nuspec = package.Files.FirstOrDefault (f => f.EndsWith (".nuspec", StringComparison.OrdinalIgnoreCase));

		if (nuspec is null)
			return null;

		nuspec = Path.Combine (nugetPackagePath, package.Path, nuspec);

		if (!File.Exists (nuspec))
			return null;

		var reader = new NuGet.Packaging.NuspecReader (nuspec);
		var tags = reader.GetTags ();

		// Try the first tag format
		var match = tag.Match (tags);

		// Try the second tag format
		if (!match.Success)
			match = tag2.Match (tags);

		if (!match.Success)
			return null;

		// TODO: Define a well-known file that can be included in the package like "java-package.txt"

		return new Artifact (match.Groups ["GroupId"].Value, match.Groups ["ArtifactId"].Value, match.Groups ["Version"].Value);
	}
}
