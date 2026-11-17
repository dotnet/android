using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniTypeSignatureTest
	{
		[TestMethod]
		public void Constructor_Exceptions ()
		{
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignature ("java.lang.Object"));
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignature ("[[I"));
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignature ("Ljava/lang/Object;"));
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignature (""));
		}

		[TestMethod]
		public void DefaultConstructor ()
		{
			var t = new JniTypeSignature ();
			Assert.IsFalse (t.IsValid);
		}

		[TestMethod]
		public void Sanity ()
		{
			var a = new JniTypeSignature ("java/lang/Object", arrayRank: 2);
			Assert.AreEqual ("java/lang/Object", a.SimpleReference);
			Assert.AreEqual (2, a.ArrayRank);
			Assert.AreEqual ("[[Ljava/lang/Object;", a.Name);
			Assert.AreEqual ("[[Ljava/lang/Object;", a.QualifiedReference);
		}

		[TestMethod]
		public void QualifiedReference ()
		{
			var info    = new JniTypeSignature ("java/lang/String");
			Assert.AreEqual ("Ljava/lang/String;",  info.QualifiedReference);
			Assert.AreEqual ("java/lang/String",    info.SimpleReference);
			Assert.AreEqual ("java/lang/String",    info.Name);

			info    = new JniTypeSignature ("java/lang/String", arrayRank:1);
			Assert.AreEqual ("java/lang/String",    info.SimpleReference);
			Assert.AreEqual ("[Ljava/lang/String;", info.QualifiedReference);
			Assert.AreEqual ("[Ljava/lang/String;", info.Name);

			info    = new JniTypeSignature ("B", keyword: true);
			Assert.AreEqual ("B",   info.Name);
			Assert.AreEqual ("B",   info.QualifiedReference);
			Assert.AreEqual ("B",   info.SimpleReference);

			info    = new JniTypeSignature ("B", arrayRank: 2, keyword: true);
			Assert.AreEqual ("[[B", info.Name);
			Assert.AreEqual ("[[B", info.QualifiedReference);
			Assert.AreEqual ("B",   info.SimpleReference);
		}

		[TestMethod]
		public void Parse ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => JniTypeSignature.Parse ((string) null));
			Assert.ThrowsExactly<ArgumentException> (() => JniTypeSignature.Parse (""));
			Assert.ThrowsExactly<ArgumentException> (() => JniTypeSignature.Parse ("java.lang.String"));
			Assert.ThrowsExactly<ArgumentException> (() => JniTypeSignature.Parse ("Ljava/lang/String;I"));
			Assert.ThrowsExactly<ArgumentException> (() => JniTypeSignature.Parse ("ILjava/lang/String;"));

			AssertGetJniTypeInfoForJniTypeReference ("java/lang/String",    "java/lang/String");
			AssertGetJniTypeInfoForJniTypeReference ("Ljava/lang/String;",  "java/lang/String");
			AssertGetJniTypeInfoForJniTypeReference ("[I",                  "I",                true,   1);
			AssertGetJniTypeInfoForJniTypeReference ("[[I",                 "I",                true,   2);
			AssertGetJniTypeInfoForJniTypeReference ("[Ljava/lang/Object;", "java/lang/Object", false,  1);

			// Yes, these look _really_ weird...
			// Assume: class II {}
			AssertGetJniTypeInfoForJniTypeReference ("II",                  "II");
			// Assume: package Ljava.lang; class String {}
			AssertGetJniTypeInfoForJniTypeReference ("Ljava/lang/String",   "Ljava/lang/String");
		}

		static void AssertGetJniTypeInfoForJniTypeReference (string jniTypeReference, string jniTypeName, bool typeIsKeyword = false, int arrayRank = 0)
		{
			var sig    = JniTypeSignature.Parse (jniTypeReference);
			Assert.AreEqual (jniTypeName,   sig.SimpleReference,    "JniTypeName for: " + jniTypeReference);
			Assert.AreEqual (arrayRank,     sig.ArrayRank,          "ArrayRank for: " + jniTypeReference);
		}

		[TestMethod]
		public void TryParse ()
		{
			Assert.IsFalse (JniTypeSignature.TryParse ("", out var _));
		}
	}
}

