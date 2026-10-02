#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Java.Interop.Tools.Maven.Models;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static partial class MavenExtensions
{
	static readonly char [] artifacts_separators = [';', ',', '\r', '\n', '\t', ' '];

	/// <summary>
	/// Shortcut for !string.IsNullOrWhiteSpace (s)
	/// </summary>
	public static bool HasValue ([NotNullWhen (true)] this string? s) => !string.IsNullOrWhiteSpace (s);

	// Helps to 'foreach' into a possibly null array
	public static T [] OrEmpty<T> (this T []? value)
	{
		return value ?? [];
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

		if (artifacts.Count > 1) {
			log.LogCodedError ("XA4256", Properties.Resources.XA4256, "JavaArtifact", type, task.ItemSpec);
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

	public static bool IsCompileDependency (this ResolvedDependency dependency) => string.IsNullOrWhiteSpace (dependency.Scope) || dependency.Scope.IndexOf ("compile", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsRuntimeDependency (this ResolvedDependency dependency) => dependency?.Scope != null && dependency.Scope.IndexOf ("runtime", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsOptional (this ResolvedDependency dependency) => dependency?.Optional != null && dependency.Optional.IndexOf ("true", StringComparison.OrdinalIgnoreCase) != -1;
}
