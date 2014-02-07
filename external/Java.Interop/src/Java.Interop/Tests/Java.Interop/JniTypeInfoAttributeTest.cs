using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTypeInfoAttributeTest
	{
		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new JniTypeInfoAttribute (null));
			Assert.Throws<ArgumentException> (() => new JniTypeInfoAttribute ("java.lang.Object"));
			Assert.Throws<ArgumentException> (() => new JniTypeInfoAttribute ("[[I"));
			Assert.Throws<ArgumentException> (() => new JniTypeInfoAttribute ("Ljava/lang/Object;"));
		}

		[Test]
		public void Sanity ()
		{
			var a = new JniTypeInfoAttribute ("java/lang/Object") {
				ArrayRank = 2,
			};
			Assert.AreEqual ("java/lang/Object", a.JniTypeName);
			Assert.AreEqual (2, a.ArrayRank);
			Assert.AreEqual ("[[Ljava/lang/Object;", a.ToString ());
		}

		[Test]
		public void ArrayRank_Exceptions ()
		{
			var a = new JniTypeInfoAttribute ("I");
			Assert.Throws<ArgumentException> (() => a.ArrayRank = -1);
		}
	}
}

