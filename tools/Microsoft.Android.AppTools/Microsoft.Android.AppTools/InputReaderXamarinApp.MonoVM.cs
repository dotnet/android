using System.IO;

namespace Microsoft.Android.AppTools;

class InputReaderXamarinAppMonoVM : InputReader
{
	Stream? inputStream;
	string? filePath;
	DataProviderXamarinAppMonoVM? xamarinApp;
	IDataProviderTypemapsMonoVM? typeMaps;

	public override bool SupportsAssemblyExtraction => false;
	public override bool SupportsAssemblyStore      => false;
	public override bool SupportsXamarinApp         => true;
	public override bool SupportsTypemaps           => true;
	public override bool SupportsAppInfo            => false;

	public InputReaderXamarinAppMonoVM (string filePath, ILogger log)
		: base (log)
	{
		this.filePath = filePath;
		inputStream = null;
	}

	public InputReaderXamarinAppMonoVM (Stream inputStream, string? filePath, ILogger log)
		: base (log)
	{
		this.inputStream = inputStream;
		filePath = null;
	}

	protected override DataProviderXamarinAppMonoVM? ReadXamarinAppMonoVM ()
	{
		return CreateProvider (
			filePath,
			ref inputStream,
			ref xamarinApp,
			(Stream s, string? path, ILogger logger) => new DataProviderXamarinAppMonoVM (s, path, logger)
		);
	}

	protected override IDataProviderTypemaps? ReadTypemaps ()
	{
		return CreateProvider (
			filePath,
			ref inputStream,
			ref typeMaps,
			(Stream s, string? path, ILogger logger) => DataProviderTypemapsXamarinAppMonoVM.Create (s, path, logger)
		);
	}
}
