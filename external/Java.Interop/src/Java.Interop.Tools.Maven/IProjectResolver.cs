using Java.Interop.Tools.Maven.Models;

namespace Java.Interop.Tools.Maven;

public interface IProjectResolver
{
	Project Resolve (Artifact artifact);
}
