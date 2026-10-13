using System;
using System.Collections.Generic;
using System.Linq;

using Android.Runtime;

using NUnit.Framework;

namespace Android.RuntimeTests
{
	[TestFixture]
	public class JavaCollectionTest
	{
		[Test]
		[Category ("JniSafetyPreparation")]
		public void GenericRemoveUsesObjectArgumentAndBooleanResult ()
		{
			using var list = new Java.Util.ArrayList ();
			list.Add (1);
			list.Add (2);
			using var collection = new JavaCollection<int> (list.Handle, JniHandleOwnership.DoNotTransfer);

			Assert.IsTrue (collection.Remove (2));
			Assert.AreEqual (1, collection.Count);
			Assert.IsFalse (collection.Contains (2));
			Assert.IsFalse (collection.Remove (3));
			Assert.AreEqual (1, collection.Count);
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
