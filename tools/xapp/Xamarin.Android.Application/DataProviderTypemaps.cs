using System.IO;

using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

interface IDataProviderTypemaps : IDataProvider
{
}

abstract class DataProviderTypemapsXamarinApp : DataProviderXamarinApp, IDataProviderTypemaps
{
	public DataProviderTypemapsXamarinApp (Stream inputStream, string? inputPath, ILogger log)
		: base (inputStream, inputPath, log)
	{
	}

	public static DataProviderXamarinApp? Create (ILogger log, AnELF elf, ulong format_tag, Stream inputStream, string? inputPath)
	{
		switch (format_tag) {
			case Constants.FormatTag_V1:
				return null;

			case Constants.FormatTag_V2:
				return null;

			default:
				//WarnNotSupported (elf, format_tag);
				return null;
		}
	}
}

class DataProviderTypeMapsXamarinApp_V2
class DataProviderTypemapsFastDev : DataProvider, IDataProviderTypemaps
{
	public DataProviderTypemapsFastDev (Stream inputStream, string? inputPath, ILogger log)
		: base (inputStream, inputPath, log)
	{
	}
}
