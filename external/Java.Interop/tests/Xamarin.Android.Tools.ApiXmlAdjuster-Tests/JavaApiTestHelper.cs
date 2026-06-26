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
				"TestData",
				"api-24.xml.in");

		public static JavaApi GetLoadedApi ()
		{
			// The shared api-24.xml.in contains a handful of `annotated-visibility="TESTS"`
			// markers used by Java.Interop.Tools.JavaTypeSystem-Tests. ApiXmlAdjuster's loader
			// does not understand that attribute, so strip it before parsing.
			var text = File.ReadAllText (ApiPath).Replace (" annotated-visibility=\"TESTS\"", "");
			var api = new JavaApi ();
			using (var sr = new StringReader (text))
			using (var xr = XmlReader.Create (sr, new XmlReaderSettings { XmlResolver = null }))
				api.Load (xr, false);
			return api;
		}
	}
}


