using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven.Repositories;

/// <summary>
/// Wraps an <see cref="IMavenRepository"/> and caches files in a local directory.
/// </summary>
public class CachedMavenRepository : IMavenRepository
{
	public string CacheDirectory { get; }

	public string Name => repository.Name;

	readonly IMavenRepository repository;

	public CachedMavenRepository (string directory, IMavenRepository repository)
	{
		CacheDirectory = directory;
		this.repository = repository;
	}

	public bool TryGetFile (Artifact artifact, string filename, [NotNullWhen (true)] out Stream? stream)
	{
		stream = null;

		if (TryGetFilePath (artifact, filename, out var path)) {
			stream = File.OpenRead (path);
			return true;
		}

		return false;
	}

	public bool TryGetFilePath (Artifact artifact, string filename, [NotNullWhen (true)] out string? path)
	{
		path = null;

		var directory = GetArtifactDirectory (artifact);
		var file = Path.Combine (directory, filename);

		if (File.Exists (file)) {
			path = file;
			return true;
		}

		if (repository.TryGetFile (artifact, filename, out var repo_stream)) {
			Directory.CreateDirectory (directory);

			using (var sw = File.Create (file))
			using (repo_stream)
				repo_stream.CopyTo (sw);

			path = file;
			return true;
		}

		return false;
	}

	public async Task<string?> GetFilePathAsync (Artifact artifact, string filename, CancellationToken cancellationToken)
	{
		var directory = GetArtifactDirectory (artifact);
		var file = Path.Combine (directory, filename);

		if (File.Exists (file))
			return file;

		if (repository.TryGetFile (artifact, filename, out var repo_stream)) {
			Directory.CreateDirectory (directory);

			using (var sw = File.Create (file))
			using (repo_stream)
				await repo_stream.CopyToAsync (sw, 81920, cancellationToken);


			return file;
		}

		return null;
	}

	string GetArtifactDirectory (Artifact artifact)
	{
		var version = artifact.Version;
		var output_directory = Path.Combine (CacheDirectory, repository.Name, artifact.GroupId, artifact.Id, version);

		return output_directory;
	}
}
