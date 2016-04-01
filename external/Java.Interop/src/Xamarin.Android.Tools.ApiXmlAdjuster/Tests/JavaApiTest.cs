using NUnit.Framework;
using System;
using System.Xml;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster.Tests
{
	[TestFixture]
	public class JavaApiTest
	{
		JavaApi api;
		
		[TestFixtureSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
		}
		
		[Test]
		public void TestToString ()
		{
			var pkg = api.Packages.First (p => p.Name == "android.database");
			Assert.AreEqual ("[Package] android.database", pkg.ToString ());
			var kls = pkg.Types.First (t => t.FullName == "android.database.ContentObservable");
			Assert.AreEqual ("[Class] android.database.ContentObservable", kls.ToString ());
		}
	}
}

