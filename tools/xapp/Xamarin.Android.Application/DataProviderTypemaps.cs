using System.IO;

namespace Xamarin.Android.Application;

class DataProviderTypemaps : DataProvider
{
	public DataProviderTypemaps (Stream inputStream, string inputPath)
		: base (inputStream, inputPath)
	{
	}
}
