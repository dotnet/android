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

		[Test]
		public void Dispose ()
		{
			var d = false;
			var f = false;
			var o = new JavaDisposedObject (() => d = true, () => f = true);
			o.Dispose ();
			Assert.IsTrue (d);
			Assert.IsFalse (f);
		}

		[Test]
		public void Dispose_Finalized ()
		{
			var d = false;
			var f = false;
			var t = new Thread (() => {
				var v     = new JavaDisposedObject (() => d = true, () => f = true);
				GC.KeepAlive (v);
			});
			t.Start ();
			t.Join ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (d);
			Assert.IsTrue (f);
		}

		[Test]
		public void ObjectDisposed ()
		{
			var o = new JavaObject ();
			o.Dispose ();

			// These should not throw
			var h = o.SafeHandle;
			var p = o.JniPeerMembers;

			// These should throw
			Assert.Throws<ObjectDisposedException> (() => o.GetHashCode ());
			Assert.Throws<ObjectDisposedException> (() => o.Register ());
			Assert.Throws<ObjectDisposedException> (() => o.ToString ());
			Assert.Throws<ObjectDisposedException> (() => o.Equals (o));
		}

		[Test]
		public void Ctor ()
		{
			using (var t = new JniType ("java/lang/Object"))
			using (var c = t.GetConstructor ("()V")) {
				var lref = t.NewObject (c);
				using (var o = new JavaObject (lref, JniHandleOwnership.DoNotTransfer)) {
					Assert.IsFalse (lref.IsInvalid);
					Assert.AreNotSame (lref, o.SafeHandle);
				}
				using (var o = new JavaObject (lref, JniHandleOwnership.Transfer)) {
					Assert.IsTrue (lref.IsInvalid);
					Assert.AreNotSame (lref, o.SafeHandle);
				}
			}
		}

		[Test]
		public void Ctor_Exceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new JavaObject (null, JniHandleOwnership.Transfer));
			Assert.Throws<ArgumentException> (() => new JavaObject (new JniInvocationHandle (IntPtr.Zero), JniHandleOwnership.Transfer));

			// Note: This may break if/when JavaVM provides "default"
			Assert.Throws<NotSupportedException> (() => new JavaObjectWithNoJavaPeer ());
			Assert.Throws<JavaException> (() => new JavaObjectWithMissingJavaPeer ());
		}
	}

	class JavaObjectWithNoJavaPeer : JavaObject {
	}

	[JniTypeInfo ("__this__/__type__/__had__/__better__/__not__/__Exist__")]
	class JavaObjectWithMissingJavaPeer : JavaObject {
	}

	[JniTypeInfo ("java/lang/Object")]
	class JavaDisposedObject : JavaObject {

		public Action   OnDisposed;
		public Action   OnFinalized;

		public JavaDisposedObject (Action disposed, Action finalized)
		{
			OnDisposed  = disposed;
			OnFinalized = finalized;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing)
				OnDisposed ();
			else
				OnFinalized ();
		}
	}
}

