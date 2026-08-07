#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Java.Interop.Tools.Maven;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven.Repositories;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

public class MavenDownload : AsyncTask
{
	public override string TaskPrefix => "MDT";

	/// <summary>
	/// The cache directory to use for Maven artifacts.
	/// </summary>
	[Required]
	public string MavenCacheDirectory { get; set; } = null!; // NRT enforced by [Required]

	/// <summary>
	/// The set of Maven libraries that we are being asked to acquire.
	/// </summary>
	public ITaskItem []? AndroidMavenLibraries { get; set; }

	/// <summary>
	/// The set of requested Maven libraries that we were able to successfully acquire.
	/// </summary>
	[Output]
	public ITaskItem []? ResolvedAndroidMavenLibraries { get; set; }

	/// <summary>
	/// The set of additional parent and imported POM files needed to verify these Maven libraries.
	/// </summary>
	[Output]
	public ITaskItem []? AndroidAdditionalJavaManifest { get; set; }

	public async override System.Threading.Tasks.Task RunTaskAsync ()
	{
		var resolved = new List<ITaskItem> ();
		var additional_poms = new List<ITaskItem> ();

		// Note each called function is responsible for reporting any errors it encounters to the user
		foreach (var library in AndroidMavenLibraries.OrEmpty ()) {

			// Validate artifact
			var id = library.ItemSpec;
			var version = library.GetRequiredMetadata ("AndroidMavenLibrary", "Version", Log);

			if (version is null)
				continue;

			if (!MavenExtensions.TryParseArtifactWithVersion (id, version, Log, out var artifact))
				continue;

			// Check for repository files
			if (await GetRepositoryArtifactOrDefault (artifact, library, additional_poms) is TaskItem result) {
				library.CopyMetadataTo (result);
				resolved.Add (result);
				continue;
			}
		}

		ResolvedAndroidMavenLibraries = resolved.ToArray ();
		AndroidAdditionalJavaManifest = additional_poms.ToArray ();
	}

	async System.Threading.Tasks.Task<TaskItem?> GetRepositoryArtifactOrDefault (Artifact artifact, ITaskItem item, List<ITaskItem> additionalPoms)
	{
		// Handles a Repository="Central|Google|<url>" entry, like:
		//  <AndroidMavenLibrary 
		//    Include="androidx.core:core" 
		//    Version="1.9.0" 
		//    Repository="Google" />
		// Note if Repository is not specifed, it is defaulted to "Central"

		// Initialize repo
		var repository = GetRepository (item, out var repository_url);

		if (repository is null)
			return null;

		// Allow user to override the Maven filename of the artifact
		var maven_override_filename = item.GetMetadataOrDefault<string> ("ArtifactFilename", null);

		// Download artifact
		var artifact_file = await MavenExtensions.DownloadPayload (repository, artifact, maven_override_filename, Log, CancellationToken);

		if (artifact_file is null)
			return null;

		LogMessage ("Found library '{0}' for Java artifact '{1}'.", artifact_file, artifact);

		var result = new TaskItem (artifact_file);

		result.SetMetadata ("JavaArtifact", artifact.VersionedArtifactString);

		// Allow user to opt out of dependency verification
		if (string.Compare (item.GetMetadataOrDefault ("VerifyDependencies", "true"), "false", true) == 0)
			return result;

		// Resolve and download POM, and any parent or imported POMs
		var resolver = new LoggingPomResolver (repository, repository_url);
		try {
			var project = ResolvedProject.FromArtifact (artifact, resolver);

			// Set the POM file path for _this_ artifact
			var primary_pom = resolver.ResolvedPoms [artifact.VersionedArtifactString];
			result.SetMetadata ("Manifest", primary_pom);

			LogMessage ("Found POM file '{0}' for Java artifact '{1}'.", primary_pom, artifact);

			// Create TaskItems for any other POMs we resolved
			foreach (var kv in resolver.ResolvedPoms.Where (k => k.Key != artifact.VersionedArtifactString)) {

				var pom_item = new TaskItem (kv.Value);
				var pom_artifact = Artifact.Parse (kv.Key);

				pom_item.SetMetadata ("JavaArtifact", pom_artifact.VersionedArtifactString);

				additionalPoms.Add (pom_item);

				LogMessage ("Found POM file '{0}' for Java artifact '{1}'.", kv.Value, pom_artifact);
			}
		} catch (Exception ex) {
			var unresolved_artifact = resolver.UnresolvedArtifact ?? artifact;
			var unresolved_pom = resolver.UnresolvedPomUrl ?? resolver.GetPomUrl (unresolved_artifact);
			var details = string.Format (Properties.Resources.XA4237_Details, unresolved_artifact, unresolved_pom, ex.Unwrap ().Message);
			LogCodedError ("XA4237", Properties.Resources.XA4237, artifact, details);
			return null;
		}

		return result;
	}

	/// <summary>
	/// Maps the well-known <c>Repository</c> metadata shorthands to their repositories.
	/// Returns <see langword="null"/> if <paramref name="type"/> is not a known shorthand,
	/// in which case it is treated as a repository URL.
	/// </summary>
	internal static MavenRepository? GetKnownRepository (string type) =>
		type.ToLowerInvariant () switch {
			"central" => MavenRepository.Central,
			"google" => MavenRepository.Google,
			_ => null
		};

	CachedMavenRepository? GetRepository (ITaskItem item, out string repositoryUrl)
	{
		var type = item.GetMetadataOrDefault ("Repository", "Central");
		repositoryUrl = type.TrimEnd ('/');

		var repo = GetKnownRepository (type);
		if (repo == MavenRepository.Central)
			repositoryUrl = "https://repo1.maven.org/maven2";
		else if (repo == MavenRepository.Google)
			repositoryUrl = "https://dl.google.com/android/maven2";

		if (repo is null && Uri.TryCreate (type, UriKind.Absolute, out var uri) &&
			(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
			if (uri.Scheme == Uri.UriSchemeHttp &&
				!string.Equals (item.GetMetadataOrDefault ("AllowInsecureHttp", "false"), "true", StringComparison.OrdinalIgnoreCase)) {
				LogCodedError ("XA4252", Properties.Resources.XA4252, type);
				return null;
			}

			using var hasher = SHA256.Create ();
			var hash = hasher.ComputeHash (Encoding.UTF8.GetBytes (type));
			var cache_name = Convert.ToBase64String (hash);

			repo = new MavenRepository (type, cache_name);
		}

		if (repo is null)
			LogCodedError ("XA4239", Properties.Resources.XA4239, type);

		return repo is not null ? new CachedMavenRepository (MavenCacheDirectory, repo) : null;
	}
}

// This wrapper around CachedMavenRepository is used to log the POMs that are resolved.
// We need these on-disk file locations so we can pass them as <AndroidAdditionalJavaManifest> items.
class LoggingPomResolver : IProjectResolver
{
	readonly CachedMavenRepository repository;
	readonly string repositoryUrl;

	public Dictionary<string, string> ResolvedPoms { get; } = new Dictionary<string, string> ();
	public Artifact? UnresolvedArtifact { get; private set; }
	public string? UnresolvedPomUrl { get; private set; }

	public LoggingPomResolver (CachedMavenRepository repository, string repositoryUrl)
	{
		this.repository = repository;
		this.repositoryUrl = repositoryUrl.TrimEnd ('/');
	}

	public Project Resolve (Artifact artifact)
	{
		try {
			if (repository.TryGetFilePath (artifact, $"{artifact.Id}-{artifact.Version}.pom", out var path)) {
				using (var stream = File.OpenRead (path)) {
					var pom = Project.Load (stream) ?? throw new InvalidOperationException ($"Could not deserialize POM for {artifact}");

					// Use index instead of Add to handle duplicates
					ResolvedPoms [artifact.VersionedArtifactString] = path;

					return pom;
				}
			}
		} catch {
			RecordUnresolvedPom (artifact);
			throw;
		}

		RecordUnresolvedPom (artifact);
		throw new InvalidOperationException ($"No POM found for {artifact}");
	}

	public string GetPomUrl (Artifact artifact)
		=> $"{repositoryUrl}/{artifact.GroupId.Replace ('.', '/')}/{artifact.Id}/{artifact.Version}/{artifact.Id}-{artifact.Version}.pom";

	void RecordUnresolvedPom (Artifact artifact)
	{
		UnresolvedArtifact = artifact;
		UnresolvedPomUrl = GetPomUrl (artifact);
	}
}
