using System;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaObjectTest
	{
		[Test]
		public void JavaReferencedInstanceSurvivesCollection ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var lrefArray = JniArrays.NewObjectArray (1, t.SafeHandle, JniReferenceSafeHandle.Null);
				JniArrays.SetObjectArrayElement (lrefArray, 0, new JavaObject ().SafeHandle);
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				var first = JniArrays.GetObjectArrayElement (lrefArray, 0);
				var o = (JavaObject) JVM.Current.GetObject (first, JniHandleOwnership.Transfer);
				Assert.IsNotNull (o);
				o.Dispose ();
				lrefArray.Dispose ();
			}
		}

		[Test]
		public void UnreferencedInstanceIsCollected ()
		{
			WeakReference r = null;
			var t = new Thread (() => r = new WeakReference (new JavaObject ()));
			t.Start ();
			t.Join ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (r.IsAlive);
			Assert.IsNull (r.Target);
		}
	}
}

