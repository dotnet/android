using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Delegate that returns a writable <see cref="System.IO.Stream"/> for the given relative path.
/// Used by extractors that produce multiple output files.
/// </summary>
public delegate Stream GetOutputStreamForPathFn (string path);
