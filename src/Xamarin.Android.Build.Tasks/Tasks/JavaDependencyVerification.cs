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

		//foreach (var pom in AndroidLibraries.Select (al => al.GetMetadata ("Manifest")))
		//	if (pom.HasValue () && pom_resolver.Register (pom) is Artifact art)
		//		poms_to_verify.Add (art);

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
			} else {
				Log.LogError ("Could not verify Java dependencies for artifact '{0}' due to missing POM file(s). See other error(s) for details.", pom);
			}
		}

		return !Log.HasLoggedErrors;
	}

	static bool TryResolveProject (Artifact artifact, IPomResolver resolver, [NotNullWhen (true)]out ResolvedProject? project)
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
	List<Artifact> artifacts = new List<Artifact> ();

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
			log.LogWarning ("Could not determine required version of Java dependency '{0}:{1}'. Validation of this dependency will not take version into account.", dependency.GroupId, dependency.ArtifactId);

		var satisfied = TrySatisfyDependency (dependency);

		if (satisfied)
			return true;

		var suggestion = packages.GetNuGetPackage ($"{dependency.GroupId}:{dependency.ArtifactId}");

		// Message if we couldn't determine the required version
		if (!dependency.Version.HasValue ()) {
			if (suggestion is string nuget)
				log.LogError ("Java dependency '{0}:{1}' is not satisfied. Microsoft maintains the NuGet package '{2}' that could fulfill this dependency.", dependency.GroupId, dependency.ArtifactId, nuget);
			else
				log.LogError ("Java dependency '{0}:{1}' is not satisfied.", dependency.GroupId, dependency.ArtifactId);

			return false;
		}

		// Message if we could determine the required version
		if (suggestion is string nuget2)
			log.LogError ("Java dependency '{0}:{1}' version '{2}' is not satisfied. Microsoft maintains the NuGet package '{3}' that could fulfill this dependency.", dependency.GroupId, dependency.ArtifactId, dependency.Version, nuget2);
		else
			log.LogError ("Java dependency '{0}:{1}' version '{2}' is not satisfied.", dependency.GroupId, dependency.ArtifactId, dependency.Version);

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
				artifacts.Add (art);
			}
		}
	}

	public void AddPackageReferences (ITaskItem []? tasks)
	{
		foreach (var task in tasks.OrEmpty ()) {

			// See if JavaArtifact/JavaVersion overrides were used
			if (TryParseJavaArtifactAndVersion ("PackageReference", task))
				continue;

			// Try parsing the NuGet metadata for Java version information instead
			var artifact = finder?.GetJavaInformation (task.ItemSpec, task.GetMetadataOrDefault ("Version", string.Empty), log);

			if (artifact != null) {
				log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from PackageReference '{3}'", artifact.GroupId, artifact.Id, artifact.Version, task.ItemSpec);
				artifacts.Add (artifact);

				continue;
			}

			log.LogMessage ("No Java artifact information found for PackageReference '{0}'", task.ItemSpec);
		}
	}

	public void AddProjectReferences (ITaskItem []? tasks)
	{
		foreach (var task in tasks.OrEmpty ()) {
			// See if JavaArtifact/JavaVersion overrides were used
			if (TryParseJavaArtifactAndVersion ("ProjectReference", task))
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
				artifacts.Add (art);
			}
		}
	}

	// "type" is PackageReference or ProjectReference
	// Returns "true" if JavaArtifact/JavaVersion is used, even if it was used incorrectly and is useless.
	// This is so the caller will know to try alternate methods if neither JavaArtifact or JavaVersion were specified.
	bool TryParseJavaArtifactAndVersion (string type, ITaskItem task)
	{
		var item_name = task.ItemSpec;

		// Convert "../../src/blah/Blah.csproj" to "Blah.csproj"
		if (type == "ProjectReference")
			item_name = Path.GetFileName (item_name);

		var has_artifact = task.HasMetadata ("JavaArtifact");
		var has_version = task.HasMetadata ("JavaVersion");

		if (has_artifact && !has_version) {
			log.LogError ("'JavaVersion' is required when using 'JavaArtifact' for {0} '{1}'.", type, item_name);
			return true;
		}

		if (!has_artifact && has_version) {
			log.LogError ("'JavaArtifact' is required when using 'JavaVersion' for {0} '{1}'.", type, item_name);
			return true;
		}

		if (has_artifact && has_version) {
			var id = task.GetMetadata ("JavaArtifact");
			var version = task.GetMetadata ("JavaVersion");

			if (string.IsNullOrWhiteSpace (id)) {
				log.LogError ("'JavaArtifact' cannot be empty for {0} '{1}'.", type, item_name);
				return true;
			}

			if (string.IsNullOrWhiteSpace (version)) {
				log.LogError ("'JavaVersion' cannot be empty for {0} '{1}'.", type, item_name);
				return true;
			}

			if (MavenExtensions.TryParseArtifactWithVersion (id, version, log, out var art)) {
				log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from {3} '{4}' (JavaArtifact)", art.GroupId, art.Id, art.Version, type, item_name);
				artifacts.Add (art);
			}

			return true;
		}

		return false;
	}

	bool TrySatisfyDependency (ResolvedDependency dependency)
	{
		if (!dependency.Version.HasValue ())
			return artifacts.Any (a =>
				a.GroupId == dependency.GroupId
				&& a.Id == dependency.ArtifactId);

		var dep_versions = MavenVersionRange.Parse (dependency.Version);

		var satisfied = artifacts.Any (a =>
			a.GroupId == dependency.GroupId
			&& a.Id == dependency.ArtifactId
			&& dep_versions.Any (r => r.ContainsVersion (MavenVersion.Parse (a.Version)))
		);

		return satisfied;
	}
}

class MSBuildLoggingPomResolver : IPomResolver
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
		item.TryParseJavaArtifactAndJavaVersion (itemName, logger, out var artifact);

		if (!File.Exists (filename)) {
			logger.LogError ("Requested POM file '{0}' does not exist.", filename);
			return null;
		}

		try {
			using (var file = File.OpenRead (filename)) {
				var project = Project.Parse (file);
				var registered_artifact = Artifact.Parse (project.ToString ());

				// Return the registered artifact, preferring any overrides specified in the task item
				var final_artifact = new Artifact (
					artifact?.GroupId ?? registered_artifact.GroupId,
					artifact?.Id ?? registered_artifact.Id,
					artifact?.Version ?? registered_artifact.Version
				);

				// Use index instead of Add to handle duplicates
				poms [final_artifact.ToString ()] = project;

				logger.LogDebugMessage ("Registered POM for artifact '{0}' from '{1}'", final_artifact, filename);

				return final_artifact;
			}
		} catch (Exception ex) {
			logger.LogError ("Failed to register POM file '{0}': '{1}'", filename, ex);
			return null;
		}
	}

	public Project ResolveRawProject (Artifact artifact)
	{
		if (poms.TryGetValue (artifact.ToString (), out var project))
			return project;

		logger.LogError ("Unable to resolve POM for artifact '{0}'.", artifact);

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
	LockFile lock_file;
	Dictionary<string, Artifact> cache = new Dictionary<string, Artifact> ();
	Regex tag = new Regex ("artifact_versioned=(?<GroupId>.+)?:(?<ArtifactId>.+?):(?<Version>.+)\\s?", RegexOptions.Compiled);
	Regex tag2 = new Regex ("artifact=(?<GroupId>.+)?:(?<ArtifactId>.+?):(?<Version>.+)\\s?", RegexOptions.Compiled);

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
			log.LogError (e.Message);
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
			log.LogError ("Could not find NuGet package '{0}' version '{1}' in lock file. Ensure NuGet Restore has run since this <PackageReference> was added.", library, version);
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
// https://docs.oracle.com/middleware/1212/core/MAVEN/maven_version.htm#MAVEN8855
public class MavenVersion : IComparable, IComparable<MavenVersion>, IEquatable<MavenVersion>
{
	public string? Major { get; private set; }
	public string? Minor { get; private set; }
	public string? Patch { get; private set; }
	public string RawVersion { get; private set; }
	public bool IsValid { get; private set; } = true;

	private MavenVersion (string rawVersion) => RawVersion = rawVersion;

	public static MavenVersion Parse (string version)
	{
		var mv = new MavenVersion (version);

		if (!version.HasValue ()) {
			mv.IsValid = false;
			return mv;
		}

		// We're going to parse through this assuming it's a valid Maven version
		mv.Major = version.FirstSubset ('.');
		version = version.ChompFirst ('.');

		if (!TryParsePart (mv.Major, out var _, out var _))
			mv.IsValid = false;

		if (!version.HasValue ())
			return mv;

		mv.Minor = version.FirstSubset ('.');
		version = version.ChompFirst ('.');

		if (!TryParsePart (mv.Minor, out var _, out var _))
			mv.IsValid = false;

		if (!version.HasValue ())
			return mv;

		mv.Patch = version.FirstSubset ('.');
		version = version.ChompFirst ('.');

		if (!TryParsePart (mv.Patch, out var _, out var _))
			mv.IsValid = false;

		if (!version.HasValue ())
			return mv;

		// If there's something left, this is a nonstandard Maven version and all bets are off
		mv.IsValid = false;

		return mv;
	}

	public int CompareTo (object obj)
	{
		return CompareTo (obj as MavenVersion);
	}

	public int CompareTo (MavenVersion? other)
	{
		if (other is null)
			return 1;

		// If either instance is nonstandard, Maven does a simple string compare
		if (!IsValid || !other.IsValid)
			return string.Compare (RawVersion, other.RawVersion);

		var major_compare = ComparePart (Major ?? "0", other.Major ?? "0");

		if (major_compare != 0)
			return major_compare;

		var minor_compare = ComparePart (Minor ?? "0", other.Minor ?? "0");

		if (minor_compare != 0)
			return minor_compare;

		return ComparePart (Patch ?? "0", other.Patch ?? "0");
	}

	public bool Equals (MavenVersion other)
	{
		return CompareTo (other) == 0;
	}

	int ComparePart (string a, string b)
	{
		// Check if they're the same string
		if (a == b)
			return 0;

		// Don't need to check the return because this shouldn't be called if IsValid = false
		TryParsePart (a, out var a_version, out var a_qualifier);
		TryParsePart (b, out var b_version, out var b_qualifier);

		// If neither have a qualifier, treat them like numbers
		if (a_qualifier is null && b_qualifier is null)
			return a_version.CompareTo (b_version);

		// If the numeric versions are different, just use those
		if (a_version != b_version)
			return a_version.CompareTo (b_version);

		// Identical versions with different qualifier fields are compared by using basic string comparison.
		if (a_qualifier is not null && b_qualifier is not null)
			return a_qualifier.CompareTo (b_qualifier);

		// All versions with a qualifier are older than the same version without a qualifier (release version).
		if (a_qualifier is not null)
			return -1;

		return 1;
	}

	static bool TryParsePart (string part, out int version, out string? qualifier)
	{
		// These can look like:
		// 1
		// 1-anything
		var version_string = part.FirstSubset ('-');
		qualifier = null;

		// The first piece must be a number
		if (!int.TryParse (version_string, out version))
			return false;

		part = part.ChompFirst ('-');

		if (part.HasValue ())
			qualifier = part;

		return true;
	}
}

public class MavenVersionRange
{
	public string? MinVersion { get; private set; }
	public string? MaxVersion { get; private set; }
	public bool IsMinInclusive { get; private set; } = true;
	public bool IsMaxInclusive { get; private set; }
	public bool HasLowerBound { get; private set; }
	public bool HasUpperBound { get; private set; }

	// Adapted from https://github.com/Redth/MavenNet/blob/master/MavenNet/MavenVersionRange.cs
	// Original version uses NuGetVersion, which doesn't cover all "valid" Maven version cases
	public static IEnumerable<MavenVersionRange> Parse (string range)
	{
		if (!range.HasValue ())
			yield break;

		var versionGroups = new List<string> ();

		// Do a pass over the range string to parse out version groups
		// eg: (1.0],(1.1,]
		var in_group = false;
		var current_group = string.Empty;

		foreach (var c in range) {
			if (c == '(' || c == '[') {
				current_group += c;
				in_group = true;
			} else if (c == ')' || c == ']' || (!in_group && c == ',')) {
				// Don't add the , separating groups
				if (in_group)
					current_group += c;

				in_group = false;

				if (current_group.HasValue ())
					yield return ParseSingle (current_group);

				current_group = string.Empty;
			} else {
				current_group += c;
			}
		}

		if (!string.IsNullOrEmpty (current_group))
			yield return ParseSingle (current_group);
	}

	static MavenVersionRange ParseSingle (string range)
	{
		var mv = new MavenVersionRange ();

		// Check for opening ( or [
		if (range [0] == '(') {
			mv.IsMinInclusive = false;
			range = range.Substring (1);
		} else if (range [0] == '[') {
			range = range.Substring (1);
		}

		var last = range.Length - 1;

		// Check for closing ) or ]
		if (range [last] == ')') {
			mv.IsMaxInclusive = false;
			range = range.Substring (0, last);
		} else if (range [last] == ']') {
			mv.IsMaxInclusive = true;
			range = range.Substring (0, last);
		}

		// Look for a single value
		if (!range.Contains (",")) {
			mv.MinVersion = range;
			mv.HasLowerBound = true;

			// Special case [1.0]
			if (mv.IsMinInclusive && mv.IsMaxInclusive) {
				mv.MaxVersion = range;
				mv.HasUpperBound = true;
			}

			return mv;
		}

		// Split the 2 values (note either can be empty)
		var lower = range.FirstSubset (',').Trim ();
		var upper = range.LastSubset (',').Trim ();

		if (lower.HasValue ()) {
			mv.MinVersion = lower;
			mv.HasLowerBound = true;
		}

		if (upper.HasValue ()) {
			mv.MaxVersion = upper;
			mv.HasUpperBound = true;
		}

		return mv;
	}

	public bool ContainsVersion (MavenVersion version)
	{
		if (HasLowerBound) {
			var min_version = MavenVersion.Parse (MinVersion!);

			if (IsMinInclusive && version.CompareTo (min_version) < 0)
				return false;
			else if (!IsMinInclusive && version.CompareTo (min_version) <= 0)
				return false;
		}

		if (HasUpperBound) {
			var max_version = MavenVersion.Parse (MaxVersion!);

			if (IsMaxInclusive && version.CompareTo (max_version) > 0)
				return false;
			else if (!IsMaxInclusive && version.CompareTo (max_version) >= 0)
				return false;
		}

		return true;
	}
}
