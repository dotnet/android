using System;
using System.Linq;
using Java.Interop.Tools.JavaTypeSystem.Models;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class JavaTypeModelsTests
	{
		JavaTypeCollection api;
		
		[OneTimeSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
		}
		
		[Test]
		public void TestToString ()
		{
			var pkg = api.Packages["android.database"];
			Assert.AreEqual ("[Package] android.database", pkg.ToString ());

			var kls = pkg.Types.First (t => t.FullName == "android.database.ContentObservable");
			Assert.AreEqual ("[Class] android.database.ContentObservable", kls.ToString ());
		}
	}
}
