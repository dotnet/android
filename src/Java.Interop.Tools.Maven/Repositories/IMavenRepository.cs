using System.Diagnostics.CodeAnalysis;
using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven.Repositories;

public interface IMavenRepository
{
	// The on-disk cache for this repository will be in a sub-directory with this name, and thus must be
	// compatible with file system naming rules. For example, "central" or "google".
	string Name { get; }
	bool TryGetFile (Artifact artifact, string filename, [NotNullWhen (true)] out Stream? stream);
}
