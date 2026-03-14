using System.IO;

namespace ApplicationUtility;

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
