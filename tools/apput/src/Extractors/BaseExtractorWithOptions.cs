namespace ApplicationUtility;

abstract class BaseExtractorWithOptions<TOptions> : BaseExtractor, IExtractorWithOptions<TOptions>
{
	public TOptions Options { get; }

	protected BaseExtractorWithOptions (IAspect containerAspect, TOptions options)
		: base (containerAspect)
	{
		Options = options;
	}
}
