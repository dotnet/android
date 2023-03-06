using System;
using System.IO;

namespace Xamarin.Android.Application;

class InputReaderAssemblyStore : InputReader
{
	Stream? inputStream;
	string? filePath;
	DataProviderAssemblyStore? store;

	public override bool SupportsAssemblyExtraction => true;
	public override bool SupportsAssemblyStore      => true;
	public override bool SupportsXamarinApp         => false;
	public override bool SupportsTypemaps           => false;
	public override bool SupportsAppInfo            => false;

	public InputReaderAssemblyStore (string filePath)
	{
		this.filePath = filePath;
		inputStream = null;
	}

	public InputReaderAssemblyStore (Stream inputStream, string? filePath = null)
	{
		this.inputStream = inputStream;
		filePath = null;
	}

	protected override bool DoExtractAssembly (string assemblyNameRegex, string outputDirectory, bool decompress)
	{
		DataProviderAssemblyStore? assemblyStore = ReadAssemblyStore ();
		if (assemblyStore == null) {
			return false;
		}

		return assemblyStore.ExtractAssembly (assemblyNameRegex, outputDirectory, decompress);
	}

	protected override DataProviderAssemblyStore? ReadAssemblyStore ()
	{
		if (store != null) {
			return  store;
		}

		if (inputStream == null) {
			if (String.IsNullOrEmpty (filePath)) {
				// TODO: log
				return null;
			}

			inputStream = File.OpenRead (filePath);
		}

		store = new DataProviderAssemblyStore (inputStream, filePath);
		return store;
	}
}
