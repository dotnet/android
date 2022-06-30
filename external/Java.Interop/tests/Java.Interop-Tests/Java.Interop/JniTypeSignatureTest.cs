using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeSignatureTest
	{
		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentException> (() => new JniTypeSignature ("java.lang.Object"));
			Assert.Throws<ArgumentException> (() => new JniTypeSignature ("[[I"));
			Assert.Throws<ArgumentException> (() => new JniTypeSignature ("Ljava/lang/Object;"));
			Assert.Throws<ArgumentException> (() => new JniTypeSignature (""));
		}

		[Test]
		public void DefaultConstructor ()
		{
			var t = new JniTypeSignature ();
			Assert.False (t.IsValid);
		}

		[Test]
		public void Sanity ()
		{
			var a = new JniTypeSignature ("java/lang/Object", arrayRank: 2);
			Assert.AreEqual ("java/lang/Object", a.SimpleReference);
			Assert.AreEqual (2, a.ArrayRank);
			Assert.AreEqual ("[[Ljava/lang/Object;", a.Name);
			Assert.AreEqual ("[[Ljava/lang/Object;", a.QualifiedReference);
		}

		[Test]
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

		[Test]
		public void Parse ()
		{
			Assert.Throws<ArgumentNullException> (() => JniTypeSignature.Parse ((string) null));
			Assert.Throws<ArgumentException> (() => JniTypeSignature.Parse (""));
			Assert.Throws<ArgumentException> (() => JniTypeSignature.Parse ("java.lang.String"));
			Assert.Throws<ArgumentException> (() => JniTypeSignature.Parse ("Ljava/lang/String;I"));
			Assert.Throws<ArgumentException> (() => JniTypeSignature.Parse ("ILjava/lang/String;"));

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

		[Test]
		public void TryParse ()
		{
			Assert.False (JniTypeSignature.TryParse ("", out var _));
		}
	}
}

