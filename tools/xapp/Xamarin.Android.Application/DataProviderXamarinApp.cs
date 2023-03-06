using System.IO;

namespace Xamarin.Android.Application;

class DataProviderXamarinApp : DataProvider
{
	public DataProviderXamarinApp (Stream inputStream, string inputPath)
		: base (inputStream, inputPath)
	{
	}
}
