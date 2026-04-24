using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Interface for extracting sub-aspect data from a container aspect.
/// </summary>
public interface IExtractor
{
	/// <summary>
	/// Extracts aspect data into the specified destination stream.
	/// </summary>
	/// <param name="destinationStream">The stream to write extracted data to.</param>
	/// <returns><c>true</c> if extraction succeeded; <c>false</c> otherwise.</returns>
	bool Extract (Stream destinationStream);

	/// <summary>
	/// Extracts one or more entries, obtaining output streams via the provided delegate.
	/// </summary>
	/// <param name="getOutputStreamForPath">Delegate that returns a writable stream for a given relative path.</param>
	/// <returns><c>true</c> if extraction succeeded; <c>false</c> otherwise.</returns>
	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath);
}
