using System;

using Android.App;
using Android.Runtime;

using NUnit.Framework;

using MyResource        = global::Xamarin.Android.RuntimeTests.Resource;

namespace Android.RuntimeTests {

	[TestFixture]
	public class XmlReaderPullParserTest {

		[Test]
		public void ToLocalJniHandle ()
		{
			var p = Application.Context.Resources.GetXml (MyResource.Xml.XmlReaderResourceParser);
			JNIEnv.DeleteLocalRef (XmlReaderPullParser.ToLocalJniHandle (p));
		}
	}
}
