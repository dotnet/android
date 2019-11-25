using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeSignatureAttributeTest
	{
		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new JniTypeSignatureAttribute (null));
			Assert.Throws<ArgumentException> (() => new JniTypeSignatureAttribute ("java.lang.Object"));
			Assert.Throws<ArgumentException> (() => new JniTypeSignatureAttribute ("[[I"));
			Assert.Throws<ArgumentException> (() => new JniTypeSignatureAttribute ("Ljava/lang/Object;"));
		}

		[Test]
		public void Sanity ()
		{
			var a   = new JniTypeSignatureAttribute ("java/lang/Object") {
				ArrayRank = 2,
			};
			Assert.AreEqual ("java/lang/Object",    a.SimpleReference);
			Assert.AreEqual (2,                     a.ArrayRank);
		}

		[Test]
		public void ArrayRank_Exceptions ()
		{
			var a   = new JniTypeSignatureAttribute ("I");
			Assert.Throws<ArgumentException> (() => a.ArrayRank = -1);
		}
	}
}

