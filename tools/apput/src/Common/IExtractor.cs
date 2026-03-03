using System.IO;

namespace ApplicationUtility;

public interface IExtractor
{
	bool Extract (Stream destinationStream);
}
