using System;

using Android.App;
using Android.Runtime;

using NUnit.Framework;

using MyResource        = global::Xamarin.Android.RuntimeTests.Resource;

namespace Android.RuntimeTests {

	[TestFixture]
	public class XmlReaderResourceParserTest {

		[Test]
		public void ToLocalJniHandle ()
		{
			var p = Application.Context.Resources.GetXml (MyResource.Xml.XmlReaderResourceParser);
			JNIEnv.DeleteLocalRef (XmlReaderResourceParser.ToLocalJniHandle (p));
		}
	}
}
