using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeInfoTest
	{
		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentException> (() => new JniTypeInfo ("java.lang.Object"));
			Assert.Throws<ArgumentException> (() => new JniTypeInfo ("[[I"));
			Assert.Throws<ArgumentException> (() => new JniTypeInfo ("Ljava/lang/Object;"));
		}

		[Test]
		public void Sanity ()
		{
			var a = new JniTypeInfo ("java/lang/Object") {
				ArrayRank = 2,
			};
			Assert.AreEqual ("java/lang/Object", a.JniTypeName);
			Assert.AreEqual (2, a.ArrayRank);
			Assert.AreEqual ("[[Ljava/lang/Object;", a.ToString ());
		}

		[Test]
		public void ArrayRank_Exceptions ()
		{
			var a = new JniTypeInfo ("I");
			Assert.Throws<ArgumentException> (() => a.ArrayRank = -1);
		}

		[Test]
		public void JniTypeReference ()
		{
			var info    = new JniTypeInfo ("java/lang/String");
			Assert.AreEqual ("Ljava/lang/String;",  info.JniTypeReference);
			Assert.AreEqual ("java/lang/String",    info.ToString ());

			info    = new JniTypeInfo ("java/lang/String", arrayRank:1);
			Assert.AreEqual ("[Ljava/lang/String;", info.JniTypeReference);
			Assert.AreEqual ("[Ljava/lang/String;", info.ToString ());

			info    = new JniTypeInfo ("B", typeIsKeyword: true);
			Assert.AreEqual ("B",   info.JniTypeReference);
			Assert.AreEqual ("B",   info.ToString ());

			info    = new JniTypeInfo ("B", typeIsKeyword: true, arrayRank: 2);
			Assert.AreEqual ("[[B", info.JniTypeReference);
			Assert.AreEqual ("[[B", info.ToString ());
		}
	}
}

