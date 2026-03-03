using System.IO;

namespace ApplicationUtility;

public interface IExtractorWithOptions<TOptions> : IExtractor
{
	bool Extract (Stream destinationStream, TOptions options);
}
