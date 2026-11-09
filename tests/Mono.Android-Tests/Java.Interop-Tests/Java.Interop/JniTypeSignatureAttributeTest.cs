using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniTypeSignatureAttributeTest
	{
		[TestMethod]
		public void Constructor_Exceptions ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => new JniTypeSignatureAttribute (null));
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignatureAttribute ("java.lang.Object"));
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignatureAttribute ("[[I"));
			Assert.ThrowsExactly<ArgumentException> (() => new JniTypeSignatureAttribute ("Ljava/lang/Object;"));
		}

		[TestMethod]
		public void Sanity ()
		{
			var a   = new JniTypeSignatureAttribute ("java/lang/Object") {
				ArrayRank = 2,
			};
			Assert.AreEqual ("java/lang/Object",    a.SimpleReference);
			Assert.AreEqual (2,                     a.ArrayRank);
		}

		[TestMethod]
		public void ArrayRank_Exceptions ()
		{
			var a   = new JniTypeSignatureAttribute ("I");
			Assert.ThrowsExactly<ArgumentException> (() => a.ArrayRank = -1);
		}
	}
}

