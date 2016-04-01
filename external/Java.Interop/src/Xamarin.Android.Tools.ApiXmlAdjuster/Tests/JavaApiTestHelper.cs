using System;
using System.IO;
using System.Xml;

namespace Xamarin.Android.Tools.ApiXmlAdjuster.Tests
{
	public class JavaApiTestHelper
	{
		static  readonly    string  TopDir  = Path.Combine (
				Path.GetDirectoryName (typeof (JavaApiTestHelper).Assembly.Location),
				"..",
				"..");

		static  readonly    string  ApiPath = Path.Combine (
				TopDir,
				"src",
				"Xamarin.Android.Tools.ApiXmlAdjuster",
				"Tests",
				"api-10.xml.in");

		public static JavaApi GetLoadedApi ()
		{
			var api = new JavaApi ();
			using (var xr = XmlReader.Create (ApiPath))
				api.Load (xr, false);
			return api;
		}
	}
}

