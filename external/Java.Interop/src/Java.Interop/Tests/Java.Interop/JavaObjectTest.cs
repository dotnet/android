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
				var lrefArray = JniEnvironment.Arrays.NewObjectArray (1, t.SafeHandle, JniReferenceSafeHandle.Null);
				var grefArray = lrefArray.NewGlobalRef ();
				lrefArray.Dispose ();
				var oldHandle = IntPtr.Zero;
				var w = new Thread (() => {
						var v       = new JavaObject ();
						oldHandle   = v.SafeHandle.DangerousGetHandle ();
						v.Register ();
						JniEnvironment.Arrays.SetObjectArrayElement (grefArray, 0, v.SafeHandle);
				});
				w.Start ();
				w.Join ();
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				var first = JniEnvironment.Arrays.GetObjectArrayElement (grefArray, 0);
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
		public void Register ()
		{
			int registeredCount = JVM.Current.GetSurfacedObjects ().Count;
			IntPtr h;
			JavaObject o;
			using (o = new JavaObject ()) {
				var l   = o.SafeHandle;
				h       = o.SafeHandle.DangerousGetHandle ();
				Assert.AreEqual (JniReferenceType.Local, o.SafeHandle.ReferenceType);
				Assert.AreEqual (registeredCount, JVM.Current.GetSurfacedObjects ().Count);
				Assert.IsNull (JVM.Current.GetObject (h));
				o.Register ();
				Assert.AreNotSame (l, o.SafeHandle);
				Assert.AreEqual (JniReferenceType.Global, o.SafeHandle.ReferenceType);
				h = o.SafeHandle.DangerousGetHandle ();
				Assert.AreEqual (registeredCount + 1, JVM.Current.GetSurfacedObjects ().Count);
				Assert.AreSame (o, JVM.Current.GetObject (h));
			}
			Assert.AreEqual (registeredCount, JVM.Current.GetSurfacedObjects ().Count);
			Assert.IsNull (JVM.Current.GetObject (h));
			Assert.Throws<ObjectDisposedException> (() => o.Register ());
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
					v.Register ();
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

