namespace ApplicationUtility;

/// <summary>
/// Base class for extractors that require typed options to control extraction behavior.
/// </summary>
/// <typeparam name="TOptions">The type of options used to configure extraction.</typeparam>
abstract class BaseExtractorWithOptions<TOptions> : BaseExtractor, IExtractorWithOptions<TOptions>
{
	public TOptions Options { get; }

	protected BaseExtractorWithOptions (IAspect containerAspect, TOptions options)
		: base (containerAspect)
	{
		Options = options;
	}
}
