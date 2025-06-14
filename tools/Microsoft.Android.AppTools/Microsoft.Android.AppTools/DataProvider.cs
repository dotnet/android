using System.IO;

namespace Microsoft.Android.AppTools;

interface IDataProvider
{
	public string? InputPath      { get; }
}

abstract class DataProvider : IDataProvider
{
	protected ILogger Log         { get; }
	protected Stream? InputStream { get; }
	public string? InputPath      { get; }

	protected DataProvider (ILogger log)
	{
		Log = log;
	}

	protected DataProvider (string inputPath, ILogger log)
		: this (log)
	{
		InputPath = inputPath;
	}

	protected DataProvider (Stream inputStream, string? inputPath, ILogger log)
		: this (inputPath ?? "[STREAM]", log)
	{
		InputStream = inputStream;
	}
}
