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
	}
}

