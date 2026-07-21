using System;
using System.IO;
using Java.Interop.Tools.Maven;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven.Repositories;

namespace Java.Interop.Tools.Maven_Tests.Extensions;

class MavenProjectResolver : IProjectResolver
{
	const string DotNetPublicMaven = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public-maven/maven/v1";
	readonly IMavenRepository repository;

	public MavenProjectResolver (IMavenRepository repository)
	{
		this.repository = repository;
	}

	static MavenProjectResolver ()
	{
		var cache_path = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "dotnet-android", "MavenCacheDirectory");

		Central = new MavenProjectResolver (new CachedMavenRepository (cache_path, GetRepository (MavenRepository.Central, "central")));
		Google = new MavenProjectResolver (new CachedMavenRepository (cache_path, GetRepository (MavenRepository.Google, "google")));
	}

	static MavenRepository GetRepository (MavenRepository localRepository, string name) =>
		string.Equals (Environment.GetEnvironmentVariable ("RUNNINGONCI"), "true", StringComparison.OrdinalIgnoreCase)
			? new MavenRepository (DotNetPublicMaven, name)
			: localRepository;

	public Project Resolve (Artifact artifact)
	{
		if (repository.TryGetFile (artifact, $"{artifact.Id}-{artifact.Version}.pom", out var stream)) {
			using (stream) {
				return Project.Load (stream) ?? throw new InvalidOperationException ($"Could not deserialize POM for {artifact}");
			}
		}

		throw new InvalidOperationException ($"No POM found for {artifact}");
	}

	public static MavenProjectResolver Google { get; }

	public static MavenProjectResolver Central { get; }
}
