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
		var repository = GetRepository (item);

		if (repository is null)
			return null;

		// Download artifact
		var artifact_file = await MavenExtensions.DownloadPayload (repository, artifact, MavenCacheDirectory, Log, CancellationToken);

		if (artifact_file is null)
			return null;

		Log.LogMessage ("Found library '{0}' for Java artifact '{1}'.", artifact_file, artifact);

		var result = new TaskItem (artifact_file);

		result.SetMetadata ("JavaArtifact", $"{artifact.GroupId}:{artifact.Id}");
		result.SetMetadata ("JavaVersion", artifact.Version);

		// Allow user to opt out of dependency verification
		if (string.Compare (item.GetMetadataOrDefault ("VerifyDependencies", "true"), "false", true) == 0)
			return result;

		// Resolve and download POM, and any parent or imported POMs
		try {
			var resolver = new LoggingPomResolver (repository);
			var project = ResolvedProject.FromArtifact (artifact, resolver);

			// Set the POM file path for _this_ artifact
			var primary_pom = resolver.ResolvedPoms [artifact.VersionedArtifactString];
			result.SetMetadata ("Manifest", primary_pom);

			Log.LogMessage ("Found POM file '{0}' for Java artifact '{1}'.", primary_pom, artifact);

			// Create TaskItems for any other POMs we resolved
			foreach (var kv in resolver.ResolvedPoms.Where (k => k.Key != artifact.VersionedArtifactString)) {

				var pom_item = new TaskItem (kv.Value);
				var pom_artifact = Artifact.Parse (kv.Key);

				pom_item.SetMetadata ("JavaArtifact", $"{pom_artifact.GroupId}:{pom_artifact.Id}");
				pom_item.SetMetadata ("JavaVersion", pom_artifact.Version);

				additionalPoms.Add (pom_item);

				Log.LogMessage ("Found POM file '{0}' for Java artifact '{1}'.", kv.Value, pom_artifact);
			}
		} catch (Exception ex) {
			Log.LogCodedError ("XA4237", Properties.Resources.XA4237, artifact, ex.Unwrap ().Message);
			return null;
		}

		return result;
	}

	CachedMavenRepository? GetRepository (ITaskItem item)
	{
		var type = item.GetMetadataOrDefault ("Repository", "Central");

		var repo = type.ToLowerInvariant () switch {
			"central" => MavenRepository.Central,
			"google" => MavenRepository.Google,
			_ => null
		};

		if (repo is null && type.StartsWith ("http", StringComparison.OrdinalIgnoreCase)) {
			using var hasher = SHA256.Create ();
			var hash = hasher.ComputeHash (Encoding.UTF8.GetBytes (type));
			var cache_name = Convert.ToBase64String (hash);

			repo = new MavenRepository (type, cache_name);
		}

		if (repo is null)
			Log.LogCodedError ("XA4239", Properties.Resources.XA4239, type);

		return repo is not null ? new CachedMavenRepository (MavenCacheDirectory, repo) : null;
	}
}

// This wrapper around CachedMavenRepository is used to log the POMs that are resolved.
// We need these on-disk file locations so we can pass them as <AndroidAdditionalJavaManifest> items.
class LoggingPomResolver : IProjectResolver
{
	readonly CachedMavenRepository repository;

	public Dictionary<string, string> ResolvedPoms { get; } = new Dictionary<string, string> ();

	public LoggingPomResolver (CachedMavenRepository repository)
	{
		this.repository = repository;
	}

	public Project Resolve (Artifact artifact)
	{
		if (repository.TryGetFilePath (artifact, $"{artifact.Id}-{artifact.Version}.pom", out var path)) {
			using (var stream = File.OpenRead (path)) {
				var pom = Project.Load (stream) ?? throw new InvalidOperationException ($"Could not deserialize POM for {artifact}");

				// Use index instead of Add to handle duplicates
				ResolvedPoms [artifact.VersionedArtifactString] = path;

				return pom;
			}
		}

		throw new InvalidOperationException ($"No POM found for {artifact}");
	}
}
