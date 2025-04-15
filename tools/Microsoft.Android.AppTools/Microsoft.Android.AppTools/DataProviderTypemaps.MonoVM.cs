using System.IO;

namespace Microsoft.Android.AppTools;

interface IDataProviderTypemapsMonoVM : IDataProviderTypemaps
{
}

abstract class DataProviderTypemapsXamarinAppMonoVM : DataProviderXamarinAppMonoVM, IDataProviderTypemapsMonoVM
{
	public DataProviderTypemapsXamarinAppMonoVM (Stream inputStream, string? inputPath, ILogger log)
		: base (inputStream, inputPath, log)
	{
	}

	public static IDataProviderTypemapsMonoVM? Create (Stream inputStream, string? inputPath, ILogger log)
	{
		// TODO: load ELF here, for detection
		return null;

		// switch (format_tag) {
		// 	case Constants.FormatTag_V1:
		// 		return null;

		// 	case Constants.FormatTag_V2:
		// 		return null;

		// 	default:
		// 		//WarnNotSupported (elf, format_tag);
		// 		return null;
		// }
	}
}

class DataProviderTypemapsFastDev : DataProvider, IDataProviderTypemaps
{
	public DataProviderTypemapsFastDev (Stream inputStream, string? inputPath, ILogger log)
		: base (inputStream, inputPath, log)
	{
	}
}
