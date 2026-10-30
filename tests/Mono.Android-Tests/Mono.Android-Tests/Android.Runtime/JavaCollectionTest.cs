using System;
using System.Collections.Generic;
using System.Linq;

using Android.Runtime;

using NUnit.Framework;

namespace Android.RuntimeTests
{
	[TestFixture]
	[Category ("HandwrittenJniBindings")]
	public class JavaCollectionTest
	{
		[TestCase (1, true)]
		[TestCase (4, false)]
		public void RemoveUsesObjectOverload (int value, bool expected)
		{
			using var list = new Java.Util.ArrayList ();
			using var collection = new JavaCollection<int> (list.Handle, JniHandleOwnership.DoNotTransfer);
			collection.Add (1);
			collection.Add (2);
			collection.Add (3);

			Assert.AreEqual (expected, collection.Remove (value));
			Assert.AreEqual (expected ? 2 : 3, collection.Count);
			Assert.IsFalse (collection.Contains (value));
			Assert.IsTrue (collection.Contains (2));
			Assert.IsTrue (collection.Contains (3));
		}

		[Test]
		public void RemoveNull ()
		{
			using var list = new Java.Util.ArrayList ();
			using var collection = new JavaCollection<string> (list.Handle, JniHandleOwnership.DoNotTransfer);
			collection.Add (null);
			collection.Add ("retained");

			Assert.IsTrue (collection.Remove (null));
			Assert.IsFalse (collection.Remove (null));
			Assert.AreEqual (1, collection.Count);
			Assert.IsTrue (collection.Contains ("retained"));
		}

		[Test]
    public void CopyTo ()
    {
      using (var al = new Java.Util.ArrayList ()) {
        al.Add (1);
        al.Add (2);
        al.Add (3);

        using (var c = new JavaCollection (al.Handle, JniHandleOwnership.DoNotTransfer)) {
          var to = new int[3];
          c.CopyTo (to, 0);
          Assert.IsTrue (new[]{1,2,3}.SequenceEqual (to));
        }

        using (var c = new JavaCollection<int> (al.Handle, JniHandleOwnership.DoNotTransfer)) {
          var to = new int[3];
          c.CopyTo (to, 0);
          Assert.IsTrue (new[]{1,2,3}.SequenceEqual (to));
        }
      }
    }
	}
}

