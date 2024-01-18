#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using MavenNet;
using MavenNet.Models;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

public class MavenDownload : AndroidAsyncTask
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

	public async override System.Threading.Tasks.Task RunTaskAsync ()
	{
		var resolved = new List<ITaskItem> ();

		// Note each called function is responsible for raising any errors it encounters to the user
		foreach (var library in AndroidMavenLibraries.OrEmpty ()) {

			// Validate artifact
			var id = library.ItemSpec;
			var version = library.GetRequiredMetadata ("AndroidMavenLibrary", "Version", Log);

			if (version is null)
				continue;
			
			var artifact = MavenExtensions.ParseArtifact (id, version, Log);
			
			if (artifact is null)
				continue;

			// Check for repository files
			if (await GetRepositoryArtifactOrDefault (artifact, library, Log) is TaskItem result) {
				library.CopyMetadataTo (result);
				resolved.Add (result);
				continue;
			}
		}

		ResolvedAndroidMavenLibraries = resolved.ToArray ();
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

		artifact.Repository = repository;

		// Download artifact
		var artifact_file = await MavenExtensions.DownloadPayload (artifact, MavenCacheDirectory, Log, CancellationToken);

		if (artifact_file is null)
			return null;

		// Download POM
		var pom_file = await MavenExtensions.DownloadPom (artifact, MavenCacheDirectory, Log, CancellationToken);

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
		var child_pom_file = item.GetRequiredMetadata ("AndroidMavenLibrary", "ArtifactPom", Log);

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

		artifact.Repository = repository;

		// Download POM
		var pom_file = await MavenExtensions.DownloadPom (artifact, MavenCacheDirectory, Log, CancellationToken);

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
			Log.LogCodedError ("XA4239", Properties.Resources.XA4239, type);

		return repo;
	}
}
