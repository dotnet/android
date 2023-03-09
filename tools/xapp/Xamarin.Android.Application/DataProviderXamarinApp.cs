using System.IO;

using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

class DataProviderXamarinApp : DataProvider
{
	public DataProviderXamarinApp (Stream inputStream, string inputPath, ILogger log)
		: base (inputStream, inputPath, log)
	{
	}
}
