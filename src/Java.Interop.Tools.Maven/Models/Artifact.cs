using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop.Tools.Maven.Models;

public class Artifact
{
	public string GroupId { get; }

	public string Id { get; }

	public string Version { get; }

	public string ArtifactString => $"{GroupId}:{Id}";

	// Format should match Project.ArtifactString for comparisons.
	public string VersionedArtifactString => $"{GroupId}:{Id}:{Version}";

	public Artifact (string groupId, string artifactId, string version)
	{
		if (groupId is null)
			throw new ArgumentNullException (nameof (groupId));
		if (artifactId is null)
			throw new ArgumentNullException (nameof (artifactId));
		if (version is null)
			throw new ArgumentNullException (nameof (version));
		if (!IsValidCoordinate (groupId))
			throw new ArgumentException ($"Invalid Maven groupId: '{groupId}'", nameof (groupId));
		if (!IsValidCoordinate (artifactId))
			throw new ArgumentException ($"Invalid Maven artifactId: '{artifactId}'", nameof (artifactId));
		// Allow empty version (callers may construct a partial coordinate when
		// the version is to be inherited from a parent POM), but reject
		// whitespace-only or otherwise malformed non-empty values.
		if (version.Length > 0 && !IsValidVersion (version))
			throw new ArgumentException ($"Invalid Maven version: '{version}'", nameof (version));

		Id = artifactId;
		GroupId = groupId;
		Version = version;
	}

	public static Artifact Parse (string value)
	{
		if (TryParse (value, out var artifact))
			return artifact;

		throw new ArgumentException ($"Invalid artifact format: {value}");
	}

	public static bool TryParse (string? value, [NotNullWhen (true)]out Artifact? artifact)
	{
		artifact = null;

		if (value is null)
			return false;

		var parts = value.Split ([':'], 4);

		if (parts.Length != 3)
			return false;

		// Parsed coordinates must have all three parts fully populated.
		if (!IsValidCoordinate (parts [0]) || !IsValidCoordinate (parts [1]) || !IsValidVersion (parts [2]))
			return false;

		artifact = new Artifact (parts [0], parts [1], parts [2]);

		return true;
	}

	// Per https://maven.apache.org/pom.html#Maven_Coordinates groupId/artifactId
	// must match [A-Za-z0-9_\-.]+
	static bool IsValidCoordinate (string value)
	{
		if (string.IsNullOrWhiteSpace (value))
			return false;
		foreach (var c in value) {
			if (!((c >= 'A' && c <= 'Z') ||
				(c >= 'a' && c <= 'z') ||
				(c >= '0' && c <= '9') ||
				c == '_' || c == '-' || c == '.'))
				return false;
		}
		return true;
	}

	// Maven versions are permissive; reject only obviously broken values
	// (empty/whitespace, embedded whitespace, path separators, or ':' which
	// would break parsing).
	static bool IsValidVersion (string value)
	{
		if (string.IsNullOrWhiteSpace (value))
			return false;
		foreach (var c in value) {
			if (c == ':' || c == '/' || c == '\\' || char.IsWhiteSpace (c))
				return false;
		}
		return true;
	}

	public override string ToString () => VersionedArtifactString;
}
