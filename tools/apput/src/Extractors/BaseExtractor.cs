using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Base class for all aspect extractors. Provides access to the container aspect.
/// </summary>
abstract class BaseExtractor : IExtractor
{
	protected IAspect ContainerAspect { get; }

	protected BaseExtractor (IAspect containerAspect)
	{
		ContainerAspect = containerAspect;
	}

	public abstract bool Extract (Stream destinationStream);
	public abstract bool Extract (GetOutputStreamForPathFn getOutputStreamForPath);
}
