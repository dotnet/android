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

		public static readonly string ApiPath = Path.Combine (
				TopDir,
				"tests",
				"Xamarin.Android.Tools.ApiXmlAdjuster-Tests",
				"api-24.xml.in");

		public static JavaApi GetLoadedApi ()
		{
			var api = new JavaApi ();
			using (var xr = XmlReader.Create (ApiPath, new XmlReaderSettings { XmlResolver = null }))
				api.Load (xr, false);
			return api;
		}
	}
}

