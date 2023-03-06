using System.IO;

namespace Xamarin.Android.Application;

abstract class DataProvider
{
	protected Stream? InputStream { get; }
	public string? InputPath   { get; }

	protected DataProvider (string inputPath)
	{
		InputPath = inputPath;
	}

	protected DataProvider (Stream inputStream, string? inputPath)
		: this (inputPath ?? "[STREAM]")
	{
		InputStream = inputStream;
	}
}
