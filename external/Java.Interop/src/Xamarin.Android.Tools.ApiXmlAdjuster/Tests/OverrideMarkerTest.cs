using System;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Tools.ApiXmlAdjuster.Tests
{
	[TestFixture]
	public class OverrideMarkerTest
	{
		JavaApi api;
		
		[TestFixtureSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
			api.MarkOverrides ();
		}
		
		[Test]
		public void InstantiatedGenericArgumentName ()
		{
			var kls = api.FindNonGenericType ("android.database.ContentObservable") as JavaClass;
			var method = kls.Members.OfType<JavaMethod> ().First (m => m.Name == "registerObserver");
			Assert.IsNotNull (method, "registerObserver() not found.");
			var para = method.Parameters.FirstOrDefault ();
			Assert.IsNotNull (para, "Expected parameter, not found.");
			Assert.AreEqual (method.Parameters.First (), method.Parameters.Last (), "There should be only one parameter.");
			Assert.AreEqual ("T", para.InstantiatedGenericArgumentName, "InstantiatedGenericArgumentName mismatch");
		}
	}
}

