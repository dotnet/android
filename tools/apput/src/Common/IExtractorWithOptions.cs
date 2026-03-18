namespace ApplicationUtility;

/// <summary>
/// An <see cref="IExtractor"/> that also carries typed extraction options.
/// </summary>
/// <typeparam name="TOptions">The type of options used to configure extraction behavior.</typeparam>
public interface IExtractorWithOptions<TOptions> : IExtractor
{
	public TOptions Options { get; }
}
