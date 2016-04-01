using System;
using NUnit.Framework;

namespace Xamarin.Android.Tools.ApiXmlAdjuster.Tests
{
	[TestFixture]
	public class TypeResolverTest
	{
		JavaApi api;
		
		[TestFixtureSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.Resolve ();
		}
		
		[Test]
		public void TypeReferenceEquals ()
		{
			var intRef = new JavaTypeReference ("int");
			Assert.AreEqual (intRef, new JavaTypeReference ("int"), "primitive types");
			Assert.AreEqual (JavaTypeReference.Int, intRef, "primitive types 2");
			Assert.AreNotEqual (new JavaTypeReference ("void"), intRef, "primitive types 3");
			Assert.AreNotEqual (intRef, new JavaTypeReference (intRef, "[]"), "primitive types: array vs. non-array");
			Assert.AreEqual (new JavaTypeReference (intRef, "[]"), new JavaTypeReference (intRef, "[]"), "primitive types: array vs. array");

			var dummyType = new JavaClass (new JavaPackage (api) { Name = string.Empty }) { Name = "Dummy" };
			var tps = new JavaTypeParameters (dummyType);
			var gt = new JavaTypeParameter (tps) { Name = "T" };
			Assert.AreEqual (new JavaTypeReference (gt, null), new JavaTypeReference (new JavaTypeParameter (tps) { Name = "T"}, null), "type parameters");
			Assert.AreNotEqual (new JavaTypeReference (gt, null), new JavaTypeReference (new JavaTypeParameter (tps) { Name = "U"}, null), "type parameters 2");
			Assert.AreNotEqual (new JavaTypeReference (gt, null), new JavaTypeReference ("T"), "primitive vs. type parameters");
			Assert.AreNotEqual (new JavaTypeReference (gt, null), new JavaTypeReference (gt, "[]"), "type parameters: array vs. non-array");
			Assert.AreEqual (new JavaTypeReference (gt, "[]"), new JavaTypeReference (gt, "[]"), "type parameters: array vs. array");
			
			var type = new JavaClass (new JavaPackage (api) { Name = string.Empty }) { Name = "T" };
			Assert.AreEqual (new JavaTypeReference (type, null, null), new JavaTypeReference (type, null, null), "type vs. type");
			Assert.AreNotEqual (new JavaTypeReference (type, null, "[]"), new JavaTypeReference (type, null, null), "type: array vs. non array");
			Assert.AreNotEqual (new JavaTypeReference (type, null, "[]"), new JavaTypeReference (type, null, "[][]"), "type: array vs. array of array");
			Assert.AreNotEqual (new JavaTypeReference (type, null, null), new JavaTypeReference (new JavaTypeParameter (tps) { Name = "T"}, null), "type vs. type parameters");

			Assert.AreNotEqual (new JavaTypeReference (gt, "[]"), new JavaTypeReference (type, null, null), "type: array vs. non array");
			Assert.AreNotEqual (new JavaTypeReference (type, null, "[]"), new JavaTypeReference (type, null, "[][]"), "type: array vs. array of array");
		}
		
		[Test]
		public void TestResolvedTypes ()
		{
			var type = api.FindNonGenericType ("android.database.ContentObservable");
			Assert.IsNotNull (type, "type not found");
			var kls = type as JavaClass;
			Assert.IsNotNull (kls, "type was not class");
			Assert.IsNotNull (kls.ResolvedExtends, "extends not resolved.");
			Assert.IsNotNull (kls.ResolvedExtends.ReferencedType, "referenced type is not correctly resolved");
		}
	}
}

