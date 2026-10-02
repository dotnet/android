#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven.Repositories;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static partial class MavenExtensions
{
	static readonly char [] separator = [':'];

	// Removes AggregateException wrapping around an exception
	public static Exception Unwrap (this Exception ex)
	{
		while (ex is AggregateException && ex.InnerException is not null)
			ex = ex.InnerException;

		return ex;
	}

	public static bool TryParseArtifactWithVersion (string id, string version, TaskLoggingHelper log, [NotNullWhen (true)] out Artifact? artifact)
	{
		artifact = null;

		var parts = id.Split (separator, StringSplitOptions.RemoveEmptyEntries);

		if (parts.Length != 2 || parts.Any (string.IsNullOrWhiteSpace)) {
			log.LogCodedError ("XA4235", Properties.Resources.XA4235, id);
			return false;
		}

		artifact = new Artifact (parts [0], parts [1], version);

		return true;
	}

	// Returns artifact output path
	public static async Task<string?> DownloadPayload (CachedMavenRepository repository, Artifact artifact, string? mavenOverrideFilename, TaskLoggingHelper log, CancellationToken cancellationToken)
	{
		var files_to_check = new List<string> ();

		if (mavenOverrideFilename.HasValue ()) {
			files_to_check.Add (repository.GetArtifactFilePath (artifact, mavenOverrideFilename));
		} else {
			files_to_check.Add (repository.GetArtifactFilePath (artifact, $"{artifact.Id}-{artifact.Version}.jar"));
			files_to_check.Add (repository.GetArtifactFilePath (artifact, $"{artifact.Id}-{artifact.Version}.aar"));
		}

		// We don't need to redownload if we already have a cached copy
		foreach (var file in files_to_check) {
			if (File.Exists (file))
				return file;
		}

		// Try to download the file from Maven
		var results = new List<(string file, string error)> ();

		foreach (var file in files_to_check) {
			if (await TryDownloadPayload (repository, artifact, Path.GetFileName (file), cancellationToken) is not string error)
				return file;

			results.Add ((file, error));
		}

		// Couldn't download the artifact, construct an error message for the user
		var error_builder = new StringBuilder ();

		foreach (var error in results)
			error_builder.Append ("- ").Append (Path.GetFileName (error.file)).Append (": ").AppendLine (error.error);

		log.LogCodedError ("XA4236", Properties.Resources.XA4236, artifact.GroupId, artifact.Id, error_builder.ToString ().TrimEnd ());

		return null;
	}

	// Return value is download error message, null represents success (async methods cannot have out parameters)
	static async Task<string?> TryDownloadPayload (CachedMavenRepository repository, Artifact artifact, string filename, CancellationToken cancellationToken)
	{
		try {
			if ((await repository.GetFilePathAsync (artifact, filename, cancellationToken)) is string path) {
				return null;
			} else {
				// This probably(?) cannot be hit, everything should come back as an exception
				return $"Could not download {filename}";
			}

		} catch (Exception ex) {
			return ex.Unwrap ().Message;
		}
	}
}
