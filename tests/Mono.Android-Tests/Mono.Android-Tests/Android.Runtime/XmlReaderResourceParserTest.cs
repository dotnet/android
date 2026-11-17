using System;

using Android.App;
using Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MyResource        = global::Xamarin.Android.RuntimeTests.Resource;

namespace Android.RuntimeTests {

	[TestClass]
	public class XmlReaderResourceParserTest {

		[TestMethod]
		public void ToLocalJniHandle ()
		{
			var p = Application.Context.Resources.GetXml (MyResource.Xml.XmlReaderResourceParser);
			JNIEnv.DeleteLocalRef (XmlReaderResourceParser.ToLocalJniHandle (p));
		}
	}
}
