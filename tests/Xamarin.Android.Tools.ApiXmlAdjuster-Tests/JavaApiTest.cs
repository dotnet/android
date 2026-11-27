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
		
		[OneTimeSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
		}
		
		[Test]
		public void TestToString ()
		{
			var pkg = api.AllPackages.First (p => p.Name == "android.database");
			Assert.AreEqual ("[Package] android.database", pkg.ToString ());
			var kls = pkg.AllTypes.First (t => t.FullName == "android.database.ContentObservable");
			Assert.AreEqual ("[Class] android.database.ContentObservable", kls.ToString ());
		}

		[Test]
		public void ParseName ()
		{
			var tn = JavaTypeName.Parse ("java.util.Function<java.util.Map.Entry<K, V>, ? extends U>");
			Assert.AreEqual ("java.util.Function", tn.FullNameNonGeneric, "top failed to parse name");
			Assert.AreEqual (2, tn.GenericArguments.Count, "top incorrect number of parsed generic arguments");
			var ga1 = tn.GenericArguments [0];
			Assert.AreEqual ("java.util.Map.Entry", ga1.FullNameNonGeneric, "genarg#0 name mismatch");
			Assert.AreEqual (2, ga1.GenericArguments.Count, "genarg#0 incorrect number of parsed generic arguments");
			Assert.AreEqual ("K", ga1.GenericArguments [0].FullNameNonGeneric, "genarg#0.1 name mismatch");
			Assert.AreEqual ("V", ga1.GenericArguments [1].FullNameNonGeneric, "genarg#0.2 name mismatch");
			var ga2 = tn.GenericArguments [1];
			Assert.AreEqual ("?", ga2.FullNameNonGeneric, "genarg#1 name mismatch");
			Assert.AreEqual (" extends ", ga2.BoundsType, "genarg#1 incorrect bounds type");
			Assert.AreEqual (1, ga2.GenericConstraints.Count, "genarg#1 incorrect number of parsed generic constraints");
			Assert.AreEqual ("U", ga2.GenericConstraints [0].FullNameNonGeneric, "genarg#1.1 constraint name mismatch");
		}

		[Test]
		public void ParseName2 ()
		{
			string name = "com.good.gd.ndkproxy.auth.GDFingerprintAuthenticationManager.a<com.good.gd.ndkproxy.auth.c.a>.b<com.good.gd.ndkproxy.auth.d.a>";
			var tn = JavaTypeName.Parse (name);
			Assert.IsTrue (tn.GenericParent != null, "result has generic parent");
			Assert.AreEqual ("b", tn.DottedName, "result name mismatch");
			Assert.AreEqual ("com.good.gd.ndkproxy.auth.GDFingerprintAuthenticationManager.a.b", tn.FullNameNonGeneric, "failed to parse name");
			Assert.AreEqual (1, tn.GenericArguments.Count, "result genparams count mismatch");
			Assert.AreEqual ("com.good.gd.ndkproxy.auth.d.a", tn.GenericArguments [0].FullNameNonGeneric, "result genarg name mismatch");
			var p = tn.GenericParent;
			Assert.AreEqual (1, p.GenericArguments.Count, "top genparams count");
			var ga1 = p.GenericArguments [0];
			Assert.AreEqual ("com.good.gd.ndkproxy.auth.c.a", ga1.FullNameNonGeneric, "top genarg name mismatch");
		}
	}
}

