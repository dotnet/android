using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniMarshalTests
	{
		[Test]
		public void RecursiveEquals ()
		{
			Assert.IsTrue (JniMarshal.RecursiveEquals (null, null));
			Assert.IsFalse (JniMarshal.RecursiveEquals (null, new object ()));
			Assert.IsFalse (JniMarshal.RecursiveEquals (new object (), null));
			Assert.IsTrue (JniMarshal.RecursiveEquals (1, 1));
			Assert.IsFalse (JniMarshal.RecursiveEquals (1, 2));
			Assert.IsTrue (JniMarshal.RecursiveEquals (new[]{ 1, 2, 3 }, new[]{ 1, 2, 3 }));
			Assert.IsFalse (JniMarshal.RecursiveEquals (new[]{ 1, 2, 3 }, new[]{ 1, 2 }));
			Assert.IsFalse (JniMarshal.RecursiveEquals (new[]{ 1, 2 }, new[]{ 1, 2, 3 }));
			Assert.IsFalse (JniMarshal.RecursiveEquals (new[]{ 1, 2 }, null));
			Assert.IsFalse (JniMarshal.RecursiveEquals (null, new[]{ 1, 2 }));
			Assert.IsTrue (JniMarshal.RecursiveEquals (
				new[]{new[]{1,2,3}, new[]{4,5,6}},
				new[]{new[]{1,2,3}, new[]{4,5,6}}
			));
			Assert.IsFalse (JniMarshal.RecursiveEquals (
				new[]{new[]{1,2,3}, new[]{4,5}},
				new[]{new[]{1,2,3}, new[]{4,5,6}}
			));
		}
	}
}

