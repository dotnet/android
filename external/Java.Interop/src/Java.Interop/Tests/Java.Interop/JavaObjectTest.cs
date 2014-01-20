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
			Console.WriteLine ("JavaReferencedInstanceSurvivesCollection");
			using (var t = new JniType ("java/lang/Object")) {
				var lrefArray = JniArrays.NewObjectArray (1, t.SafeHandle, JniReferenceSafeHandle.Null);
				var grefArray = lrefArray.NewGlobalRef ();
				lrefArray.Dispose ();
				var oldHandle = IntPtr.Zero;
				var w = new Thread (() => {
						var v       = new JavaObject ();
						oldHandle   = v.SafeHandle.DangerousGetHandle ();
						JniArrays.SetObjectArrayElement (grefArray, 0, v.SafeHandle);
				});
				w.Start ();
				w.Join ();
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				var first = JniArrays.GetObjectArrayElement (grefArray, 0);
				var o = (JavaObject) JVM.Current.GetObject (first, JniHandleOwnership.Transfer);
				Assert.IsNotNull (o);
				if (oldHandle != o.SafeHandle.DangerousGetHandle ()) {
					Console.WriteLine ("Yay, object handle changed; value survived a GC!");
				} else {
					Console.WriteLine ("What is this, Android pre-ICS?!");
				}
				o.Dispose ();
				grefArray.Dispose ();
			}
		}

		[Test]
		public void UnreferencedInstanceIsCollected ()
		{
			var oldHandle   = IntPtr.Zero;
			WeakReference r = null;
			var t = new Thread (() => {
					var v     = new JavaObject ();
					oldHandle = v.SafeHandle.DangerousGetHandle ();
					r         = new WeakReference (v);
			});
			t.Start ();
			t.Join ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (r.IsAlive);
			Assert.IsNull (r.Target);
			Assert.IsNull (JVM.Current.GetObject (oldHandle));
		}
	}
}

