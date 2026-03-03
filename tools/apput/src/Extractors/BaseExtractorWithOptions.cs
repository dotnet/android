using System.IO;

namespace ApplicationUtility;

abstract class BaseExtractorWithOptions<TOptions> : BaseExtractor, IExtractorWithOptions<TOptions>
{
	protected TOptions Options { get; }

	protected BaseExtractorWithOptions (IAspect containerAspect, TOptions options)
		: base (containerAspect)
	{
		Options = options;
	}

	public abstract bool Extract (Stream destinationStream, TOptions options);
}
