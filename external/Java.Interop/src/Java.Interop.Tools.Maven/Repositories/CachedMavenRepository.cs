using System;
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

		var file = GetArtifactFilePath (artifact, filename);

		if (File.Exists (file)) {
			path = file;
			return true;
		}

		if (repository.TryGetFile (artifact, filename, out var repo_stream)) {
			var directory = GetArtifactDirectory (artifact);
			Directory.CreateDirectory (directory);
			var temporary_file = Path.Combine (directory, Path.GetRandomFileName ());

			try {
				using (var sw = File.Create (temporary_file))
				using (repo_stream)
					repo_stream.CopyTo (sw);

				PublishTemporaryFile (temporary_file, file);
			} catch (Exception ex) {
				DeleteTemporaryFileAfterFailure (temporary_file, ex);
				throw;
			}
			File.Delete (temporary_file);

			path = file;
			return true;
		}

		return false;
	}

	public async Task<string?> GetFilePathAsync (Artifact artifact, string filename, CancellationToken cancellationToken)
	{
		var file = GetArtifactFilePath (artifact, filename);

		if (File.Exists (file))
			return file;

		if (repository.TryGetFile (artifact, filename, out var repo_stream)) {
			var directory = GetArtifactDirectory (artifact);
			Directory.CreateDirectory (directory);
			var temporary_file = Path.Combine (directory, Path.GetRandomFileName ());

			try {
				using (var sw = File.Create (temporary_file))
				using (repo_stream)
					await repo_stream.CopyToAsync (sw, 81920, cancellationToken);

				PublishTemporaryFile (temporary_file, file);
			} catch (Exception ex) {
				DeleteTemporaryFileAfterFailure (temporary_file, ex);
				throw;
			}
			File.Delete (temporary_file);

			return file;
		}

		return null;
	}

	static void PublishTemporaryFile (string temporaryFile, string file)
	{
		try {
			File.Move (temporaryFile, file);
		} catch (IOException) when (File.Exists (file)) {
			// Another process completed the same artifact download first.
		}
	}

	static void DeleteTemporaryFileAfterFailure (string temporaryFile, Exception failure)
	{
		try {
			File.Delete (temporaryFile);
		} catch (Exception cleanupException) when (cleanupException is IOException || cleanupException is UnauthorizedAccessException) {
			failure.Data ["MavenCacheTemporaryFileCleanupException"] = cleanupException;
		}
	}

	/// <summary>
	/// Returns the on-disk path where the given <paramref name="artifact"/> + <paramref name="filename"/>
	/// would be cached under <see cref="CacheDirectory"/>. Does not download or check for existence.
	/// Throws <see cref="InvalidOperationException"/> if the resolved path would not be under
	/// <see cref="CacheDirectory"/>.
	/// </summary>
	public string GetArtifactFilePath (Artifact artifact, string filename)
	{
		var directory = GetArtifactDirectory (artifact);
		var file = Path.Combine (directory, filename);
		var full_file = Path.GetFullPath (file);
		var full_cache = Path.GetFullPath (CacheDirectory);
		if (!full_cache.EndsWith (Path.DirectorySeparatorChar.ToString ()) && !full_cache.EndsWith (Path.AltDirectorySeparatorChar.ToString ()))
			full_cache += Path.DirectorySeparatorChar;
		if (!full_file.StartsWith (full_cache, StringComparison.Ordinal))
			throw new InvalidOperationException ($"Resolved Maven cache path '{full_file}' escapes cache directory '{full_cache}'.");
		return full_file;
	}

	string GetArtifactDirectory (Artifact artifact)
	{
		var version = artifact.Version;
		var output_directory = Path.Combine (CacheDirectory, repository.Name, artifact.GroupId, artifact.Id, version);

		return output_directory;
	}
}
