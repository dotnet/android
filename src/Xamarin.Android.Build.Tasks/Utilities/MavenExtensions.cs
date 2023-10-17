using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MavenNet;
using MavenNet.Models;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static class MavenExtensions
{
	static readonly char [] separator = [':'];

	/// <summary>
	/// Shortcut for !string.IsNullOrWhiteSpace (s)
	/// </summary>
	public static bool HasValue ([NotNullWhen (true)] this string? s) => !string.IsNullOrWhiteSpace (s);

	// Helps to 'foreach' into a possibly null array
	public static T [] OrEmpty<T> (this T []? value)
	{
		return value ?? Enumerable.Empty<T> ().ToArray ();
	}

	public static Artifact? ParseArtifact (string id, string version, TaskLoggingHelper log)
	{
		var parts = id.Split (separator, StringSplitOptions.RemoveEmptyEntries);

		if (parts.Length != 2 || parts.Any (string.IsNullOrWhiteSpace)) {
			log.LogError ("Artifact specification '{0}' is invalid.", id);
			return null;
		}

		var artifact = new Artifact (parts [1], parts [0], version);

		return artifact;
	}

	// TODO: Fix this in MavenNet
	public static void SetRepository (this Artifact artifact, MavenRepository repository)
	{
		var method = artifact.GetType ().GetProperty ("Repository");

		method.GetSetMethod (true).Invoke (artifact, new [] { repository });
	}

	// TODO: Fix this in MavenNet
	public static Project ParsePom (string pomFile)
	{
		var parser = typeof (Project).Assembly.GetType ("MavenNet.PomParser");
		var method = parser.GetMethod ("Parse");

		using var sr = File.OpenRead (pomFile);

		var pom = method.Invoke (null, new [] { sr }) as Project;

		return pom!;
	}

	public static Artifact? CheckForNeededParentPom (string pomFile)
		=> ParsePom (pomFile).GetParentPom ();

	public static Artifact? GetParentPom (this Project? pom)
	{
		if (pom?.Parent != null)
			return new Artifact (pom.Parent.ArtifactId, pom.Parent.GroupId, pom.Parent.Version);

		return null;
	}

	// Returns artifact output path
	public static async Task<string?> DownloadPayload (Artifact artifact, string cacheDir, TaskLoggingHelper log)
	{
		var version = artifact.Versions.First ();

		var output_directory = Path.Combine (cacheDir, artifact.GetRepositoryCacheName (), artifact.GroupId, artifact.Id, version);

		Directory.CreateDirectory (output_directory);

		var filename = $"{artifact.GroupId}_{artifact.Id}";
		var jar_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.jar"));
		var aar_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.aar"));

		// We don't need to redownload if we already have a cached copy
		if (File.Exists (jar_filename))
			return jar_filename;

		if (File.Exists (aar_filename))
			return aar_filename;

		if (await TryDownloadPayload (artifact, jar_filename) is not string jar_error)
			return jar_filename;

		if (await TryDownloadPayload (artifact, aar_filename) is not string aar_error)
			return aar_filename;

		log.LogError ("Cannot download artifact '{0}:{1}'.\n- {2}: {3}\n- {4}: {5}", artifact.GroupId, artifact.Id, Path.GetFileName (jar_filename), jar_error, Path.GetFileName (aar_filename), aar_error);

		return null;
	}

	// Returns artifact output path
	public static async Task<string?> DownloadPom (Artifact artifact, string cacheDir, TaskLoggingHelper log, bool isParent = false)
	{
		var version = artifact.Versions.First ();
		var output_directory = Path.Combine (cacheDir, artifact.GetRepositoryCacheName (), artifact.GroupId, artifact.Id, version);

		Directory.CreateDirectory (output_directory);

		var filename = $"{artifact.GroupId}_{artifact.Id}";
		var pom_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.pom"));

		// We don't need to redownload if we already have a cached copy
		if (File.Exists (pom_filename))
			return pom_filename;

		if (await TryDownloadPayload (artifact, pom_filename) is not string pom_error)
			return pom_filename;

		log.LogError ("Cannot download {4}POM file for artifact '{0}:{1}'.\n- {2}: {3}", artifact.GroupId, artifact.Id, Path.GetFileName (pom_filename), pom_error, isParent ? "parent " : "");

		return null;
	}

	// Return value indicates download success
	static async Task<string?> TryDownloadPayload (Artifact artifact, string filename)
	{
		try {
			using var src = await artifact.OpenLibraryFile (artifact.Versions.First (), Path.GetExtension (filename));
			using var sw = File.Create (filename);

			await src.CopyToAsync (sw);

			return null;
		} catch (Exception ex) {
			return ex.Message;
		}
	}

	public static string GetRepositoryCacheName (this Artifact artifact)
	{
		var type = artifact.Repository;

		if (type is MavenCentralRepository)
			return "central";

		if (type is GoogleMavenRepository)
			return "google";

		if (type is UrlMavenRepository url) {
			using var hasher = SHA256.Create ();
			var hash = hasher.ComputeHash (Encoding.UTF8.GetBytes (url.BaseUri.ToString ()));
			return Convert.ToBase64String (hash);
		}

		// Should never be hit
		throw new ArgumentException ($"Unexpected repository type: {type.GetType ()}");
	}

	public static void FixDependency (Project project, Project? parent, Dependency dependency)
	{
		// Handle Parent POM
		if ((string.IsNullOrEmpty (dependency.Version) || string.IsNullOrEmpty (dependency.Scope)) && parent != null) {
			var parent_dependency = parent.FindParentDependency (dependency);

			// Try to fish a version out of the parent POM
			if (string.IsNullOrEmpty (dependency.Version))
				dependency.Version = ReplaceVersionProperties (parent, parent_dependency?.Version);

			// Try to fish a scope out of the parent POM
			if (string.IsNullOrEmpty (dependency.Scope))
				dependency.Scope = parent_dependency?.Scope;
		}

		var version = dependency.Version;

		if (string.IsNullOrWhiteSpace (version))
			return;

		version = ReplaceVersionProperties (project, version);

		// VersionRange.Parse cannot handle single number versions that we sometimes see in Maven, like "1".
		// Fix them to be "1.0".
		// https://github.com/NuGet/Home/issues/10342
		if (version != null && !version.Contains ("."))
			version += ".0";

		dependency.Version = version;
	}

	static string? ReplaceVersionProperties (Project project, string? version)
	{
		// Handle versions with Properties, like:
		// <properties>
		//   <java.version>1.8</java.version>
		//   <gson.version>2.8.6</gson.version>
		// </properties>
		// <dependencies>
		//   <dependency>
		//     <groupId>com.google.code.gson</groupId>
		//     <artifactId>gson</artifactId>
		//     <version>${gson.version}</version>
		//   </dependency>
		// </dependencies>
		if (string.IsNullOrWhiteSpace (version) || project?.Properties == null)
			return version;

		foreach (var prop in project.Properties.Any)
			version = version?.Replace ($"${{{prop.Name.LocalName}}}", prop.Value);

		return version;
	}

	public static bool IsCompileDependency (this Dependency dependency) => string.IsNullOrWhiteSpace (dependency.Scope) || string.IndexOf("compile", StringComparison.OrdinalIgnoreCase) != -1;

	public static bool IsRuntimeDependency (this Dependency dependency) => dependency?.Scope != null && string.IndexOf("runtime", StringComparison.OrdinalIgnoreCase) != -1;

	public static Dependency? FindParentDependency (this Project project, Dependency dependency)
	{
		return project.DependencyManagement?.Dependencies?.FirstOrDefault (
			d => d.GroupAndArtifactId () == dependency.GroupAndArtifactId () && d.Classifier != "sources");
	}

	public static string GroupAndArtifactId (this Dependency dependency) => $"{dependency.GroupId}.{dependency.ArtifactId}";
}
