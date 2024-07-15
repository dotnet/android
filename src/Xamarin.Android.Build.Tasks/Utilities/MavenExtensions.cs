#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
	static readonly char [] artifacts_separators = [';', ',', '\r', '\n'];

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

	public static bool TryParseJavaArtifact (this ITaskItem task, string type, TaskLoggingHelper log, [NotNullWhen (true)]out Artifact? artifact, out bool attributesSpecified)
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

		// Convert "../../src/blah/Blah.csproj" to "Blah.csproj"
		if (type == "ProjectReference")
			item_name = Path.GetFileName (item_name);

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
	public static async Task<string?> DownloadPayload (CachedMavenRepository repository, Artifact artifact, string cacheDir, TaskLoggingHelper log, CancellationToken cancellationToken)
	{
		var output_directory = Path.Combine (cacheDir, repository.Name, artifact.GroupId, artifact.Id, artifact.Version);

		Directory.CreateDirectory (output_directory);

		var filename = $"{artifact.Id}-{artifact.Version}";
		var jar_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.jar"));
		var aar_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.aar"));

		// We don't need to redownload if we already have a cached copy
		if (File.Exists (jar_filename))
			return jar_filename;

		if (File.Exists (aar_filename))
			return aar_filename;

		if (await TryDownloadPayload (repository, artifact, jar_filename, cancellationToken) is not string jar_error)
			return jar_filename;

		if (await TryDownloadPayload (repository, artifact, aar_filename, cancellationToken) is not string aar_error)
			return aar_filename;

		log.LogCodedError ("XA4236", Properties.Resources.XA4236, artifact.GroupId, artifact.Id, Path.GetFileName (jar_filename), jar_error, Path.GetFileName (aar_filename), aar_error);

		return null;
	}

	// Return value is download error message, null represents success (async methods cannot have out parameters)
	static async Task<string?> TryDownloadPayload (CachedMavenRepository repository, Artifact artifact, string filename, CancellationToken cancellationToken)
	{
		var maven_filename = $"{artifact.Id}-{artifact.Version}{Path.GetExtension (filename)}";

		try {
			if ((await repository.GetFilePathAsync (artifact, maven_filename, cancellationToken)) is string path) {
				return null;
			} else {
				// This probably(?) cannot be hit, everything should come back as an exception
				return $"Could not download {maven_filename}";
			}

		} catch (Exception ex) {
			return ex.Unwrap ().Message;
		}
	}

	public static bool IsCompileDependency (this ResolvedDependency dependency) => string.IsNullOrWhiteSpace (dependency.Scope) || dependency.Scope.IndexOf ("compile", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsRuntimeDependency (this ResolvedDependency dependency) => dependency?.Scope != null && dependency.Scope.IndexOf ("runtime", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsOptional (this ResolvedDependency dependency) => dependency?.Optional != null && dependency.Optional.IndexOf ("true", StringComparison.OrdinalIgnoreCase) != -1;
}
