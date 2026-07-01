using System;
using Java.Interop.Tools.JavaTypeSystem.Models;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class JavaTypeReferenceTests
	{
		[Test]
		public void TypeReferenceEquals ()
		{
			var int_ref = JavaTypeReference.Int;
			Assert.AreEqual (JavaTypeReference.Int, int_ref, "primitive types 2");

			var pkg = new JavaPackage ("com.example", "com/example", null);
			var dummyType = JavaApiTestHelper.CreateClass (pkg, "Dummy");
			var tps = new JavaTypeParameters (dummyType);
			var gt = new JavaTypeParameter ("T", tps);

			Assert.AreEqual (new JavaTypeReference (gt, null), new JavaTypeReference (new JavaTypeParameter ("T", tps), null), "type parameters");
			Assert.AreNotEqual (new JavaTypeReference (gt, null), new JavaTypeReference (new JavaTypeParameter ("U", tps), null), "type parameters 2");
			Assert.AreNotEqual (new JavaTypeReference (gt, null), new JavaTypeReference (gt, "[]"), "type parameters: array vs. non-array");
			Assert.AreEqual (new JavaTypeReference (gt, "[]"), new JavaTypeReference (gt, "[]"), "type parameters: array vs. array");

			var type = JavaApiTestHelper.CreateClass (pkg, "T");
			Assert.AreEqual (new JavaTypeReference (type, null, null), new JavaTypeReference (type, null, null), "type vs. type");
			Assert.AreNotEqual (new JavaTypeReference (type, null, "[]"), new JavaTypeReference (type, null, null), "type: array vs. non array");
			Assert.AreNotEqual (new JavaTypeReference (type, null, "[]"), new JavaTypeReference (type, null, "[][]"), "type: array vs. array of array");
			Assert.AreNotEqual (new JavaTypeReference (type, null, null), new JavaTypeReference (new JavaTypeParameter ("T", tps), null), "type vs. type parameters");

			Assert.AreNotEqual (new JavaTypeReference (gt, "[]"), new JavaTypeReference (type, null, null), "type: array vs. non array");
			Assert.AreNotEqual (new JavaTypeReference (type, null, "[]"), new JavaTypeReference (type, null, "[][]"), "type: array vs. array of array");
		}
	}
}
