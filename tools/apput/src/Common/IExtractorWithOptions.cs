namespace ApplicationUtility;

public interface IExtractorWithOptions<TOptions> : IExtractor
{
	public TOptions Options { get; }
}
