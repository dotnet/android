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

