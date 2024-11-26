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
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static class MavenExtensions
{
	static readonly char [] separator = [':'];
	static readonly char [] artifacts_separators = [';', ',', '\r', '\n', '\t', ' '];

	/// <summary>
	/// Shortcut for !string.IsNullOrWhiteSpace (s)
	/// </summary>
	public static bool HasValue ([NotNullWhen (true)] this string? s) => !string.IsNullOrWhiteSpace (s);

	// Helps to 'foreach' into a possibly null array
	public static T [] OrEmpty<T> (this T []? value)
	{
		return value ?? Array.Empty<T> ();
	}

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

	public static bool TryParseArtifacts (string id, TaskLoggingHelper log, out List<Artifact> artifacts)
	{
		artifacts = new List<Artifact> ();
		var result = true;

		var arts = id.Split (artifacts_separators, StringSplitOptions.RemoveEmptyEntries);

		foreach (var art in arts) {

			if (Artifact.TryParse (art, out var a)) {
				artifacts.Add (a);
				continue;
			}

			log.LogCodedError ("XA4249", Properties.Resources.XA4249, art);
			result = false;
		}

		return result;
	}

	public static bool TryParseJavaArtifact (this ITaskItem task, string type, TaskLoggingHelper log, [NotNullWhen (true)] out Artifact? artifact, out bool attributesSpecified)
	{
		var result = TryParseJavaArtifacts (task, type, log, out var artifacts, out attributesSpecified);

		if (!result) {
			artifact = null;
			return false;
		}

		// TODO: Need a new message saying that only one JavaArtifact is allowed
		if (artifacts.Count > 1) {
			log.LogCodedError ("XA4243", Properties.Resources.XA4243, "JavaArtifact", type, task.ItemSpec);
			artifact = null;
			return false;
		}

		artifact = artifacts.FirstOrDefault ();

		return artifact is not null;
	}

	public static bool TryParseJavaArtifacts (this ITaskItem task, string type, TaskLoggingHelper log, out List<Artifact> artifacts, out bool attributesSpecified)
	{
		artifacts = new List<Artifact> ();
		var item_name = task.ItemSpec;

		var has_artifact = task.HasMetadata ("JavaArtifact");

		// Lets callers know if user attempted to specify JavaArtifact, even if they did it incorrectly
		attributesSpecified = has_artifact;

		if (has_artifact) {
			var id = task.GetMetadata ("JavaArtifact");

			if (string.IsNullOrWhiteSpace (id)) {
				log.LogCodedError ("XA4244", Properties.Resources.XA4244, "JavaArtifact", type, item_name);
				return false;
			}

			if (TryParseArtifacts (id, log, out var parsed)) {
				foreach (var art in parsed) {
					log.LogMessage ("Found Java dependency '{0}:{1}' version '{2}' from {3} '{4}' (JavaArtifact)", art.GroupId, art.Id, art.Version, type, item_name);
					artifacts.Add (art);
				}

				return true;
			}
		}

		return false;
	}

	// Returns artifact output path
	public static async Task<string?> DownloadPayload (CachedMavenRepository repository, Artifact artifact, string cacheDir, string? mavenOverrideFilename, TaskLoggingHelper log, CancellationToken cancellationToken)
	{
		var output_directory = Path.Combine (cacheDir, repository.Name, artifact.GroupId, artifact.Id, artifact.Version);

		Directory.CreateDirectory (output_directory);

		var files_to_check = new List<string> ();

		if (mavenOverrideFilename.HasValue ()) {
			files_to_check.Add (Path.Combine (output_directory, mavenOverrideFilename));
		} else {
			files_to_check.Add (Path.Combine (output_directory, $"{artifact.Id}-{artifact.Version}.jar"));
			files_to_check.Add (Path.Combine (output_directory, $"{artifact.Id}-{artifact.Version}.aar"));
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
			error_builder.AppendLine ($"- {Path.GetFileName (error.file)}: {error.error}");

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

	public static bool IsCompileDependency (this ResolvedDependency dependency) => string.IsNullOrWhiteSpace (dependency.Scope) || dependency.Scope.IndexOf ("compile", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsRuntimeDependency (this ResolvedDependency dependency) => dependency?.Scope != null && dependency.Scope.IndexOf ("runtime", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsOptional (this ResolvedDependency dependency) => dependency?.Optional != null && dependency.Optional.IndexOf ("true", StringComparison.OrdinalIgnoreCase) != -1;
}
