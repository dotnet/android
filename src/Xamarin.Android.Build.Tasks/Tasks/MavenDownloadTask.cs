using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MavenNet;
using MavenNet.Models;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MonoDroid.Utils;

namespace Xamarin.Android.Tasks;

public class MavenDownloadTask : AndroidAsyncTask
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
	/// The set of requested Maven libraries that we were able to successfully download.
	/// </summary>
	[Output]
	public ITaskItem []? ResolvedAndroidMavenLibraries { get; set; }

	public async override System.Threading.Tasks.Task RunTaskAsync ()
	{
		var resolved = new List<ITaskItem> ();

		// Note each called function is responsible for raising any errors it encounters to the user
		foreach (var library in AndroidMavenLibraries.OrEmpty ()) {

			// Validate artifact
			var id = library.ItemSpec;
			var version = library.GetRequiredMetadata ("Version", Log);

			if (version is null)
				continue;
			
			var artifact = MavenExtensions.ParseArtifact (id, version, Log);
			
			if (artifact is null)
				continue;

			// Check for local files
			if (GetLocalArtifactOrDefault (library, Log) is TaskItem cached_result) {
				library.CopyMetadataTo (cached_result);
				resolved.Add (cached_result);
				continue;
			}

			// Check for repository files
			if (await GetRepositoryArtifactOrDefault (artifact, library, Log) is TaskItem result) {
				library.CopyMetadataTo (result);
				resolved.Add (result);
				continue;
			}
		}

		ResolvedAndroidMavenLibraries = resolved.ToArray ();
	}

	TaskItem? GetLocalArtifactOrDefault (ITaskItem item, TaskLoggingHelper log)
	{
		// Handles a Repository="file" entry, like:
		//  <AndroidMavenLibrary 
		//    Include="my.company:package" 
		//    Version="1.0.0" 
		//    Repository="File"
		//    PackageFile="C:\packages\mypackage-1.0.0.jar"
		//    PomFile="C:\packages\mypackage-1.0.0.pom" />
		var type = item.GetMetadataOrDefault ("Repository", "Central");

		if (type.Equals ("file", StringComparison.InvariantCultureIgnoreCase)) {
			var artifact_file = item.GetMetadataOrDefault ("PackageFile", string.Empty);
			var pom_file = item.GetMetadataOrDefault ("PomFile", string.Empty);

			if (!artifact_file.HasValue () || !pom_file.HasValue ()) {
				log.LogError ("'PackageFile' and 'PomFile' must be specified when using a 'File' repository.");
				return null;
			}

			if (!File.Exists (artifact_file)) {
				log.LogError ("Specified package file '{0}' does not exist.", artifact_file);
				return null;
			}

			if (!File.Exists (pom_file)) {
				log.LogError ("Specified pom file '{0}' does not exist.", pom_file);
				return null;
			}

			var result = new TaskItem (artifact_file);

			result.SetMetadata ("ArtifactSpec", item.ItemSpec);
			result.SetMetadata ("ArtifactFile", artifact_file);
			result.SetMetadata ("ArtifactPom", pom_file);

			return result;
		}

		return null;
	}

	async System.Threading.Tasks.Task<TaskItem?> GetRepositoryArtifactOrDefault (Artifact artifact, ITaskItem item, TaskLoggingHelper log)
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

		artifact.SetRepository (repository);

		// Download artifact
		var artifact_file = await MavenExtensions.DownloadPayload (artifact, MavenCacheDirectory, Log);

		if (artifact_file is null)
			return null;

		// Download POM
		var pom_file = await MavenExtensions.DownloadPom (artifact, MavenCacheDirectory, Log);

		if (pom_file is null)
			return null;

		var result = new TaskItem (artifact_file);

		result.SetMetadata ("ArtifactSpec", item.ItemSpec);
		result.SetMetadata ("ArtifactFile", artifact_file);
		result.SetMetadata ("ArtifactPom", pom_file);

		return result;
	}

	async System.Threading.Tasks.Task<TaskItem?> TryGetParentPom (ITaskItem item, TaskLoggingHelper log)
	{
		var child_pom_file = item.GetRequiredMetadata ("ArtifactPom", Log);

		// Shouldn't be possible because we just created this items
		if (child_pom_file is null)
			return null;

		// No parent POM needed
		if (!(MavenExtensions.CheckForNeededParentPom (child_pom_file) is Artifact artifact))
			return null;

		// Initialize repo (parent will be in same repository as child)
		var repository = GetRepository (item);

		if (repository is null)
			return null;

		artifact.SetRepository (repository);

		// Download POM
		var pom_file = await MavenExtensions.DownloadPom (artifact, MavenCacheDirectory, Log);

		if (pom_file is null)
			return null;

		var result = new TaskItem ($"{artifact.GroupId}:{artifact.Id}");

		result.SetMetadata ("Version", artifact.Versions.FirstOrDefault ());
		result.SetMetadata ("ArtifactPom", pom_file);

		// Copy repository data
		item.CopyMetadataTo (result);

		return result;
	}

	MavenRepository? GetRepository (ITaskItem item)
	{
		var type = item.GetMetadataOrDefault ("Repository", "Central");

		var repo = type.ToLowerInvariant () switch {
			"central" => MavenRepository.FromMavenCentral (),
			"google" => MavenRepository.FromGoogle (),
			_ => (MavenRepository?) null
		};

		if (repo is null && type.StartsWith ("http", StringComparison.OrdinalIgnoreCase))
			repo = MavenRepository.FromUrl (type);

		if (repo is null)
			Log.LogError ("Unknown Maven repository: '{0}'.", type);

		return repo;
	}
}
