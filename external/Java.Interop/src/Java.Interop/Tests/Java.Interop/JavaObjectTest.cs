using System;
using System.Reflection;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaObjectTest : JavaVMFixture
	{
		[Test]
		public void JavaReferencedInstanceSurvivesCollection ()
		{
			Console.WriteLine ("JavaReferencedInstanceSurvivesCollection");
			using (var t = new JniType ("java/lang/Object")) {
				var oldHandle = IntPtr.Zero;
				var array     = new JavaObjectArray<JavaObject> (1);
				var w = new Thread (() => {
						var v       = new JavaObject ();
						oldHandle   = v.PeerReference.Handle;
						array [0] = v;
				});
				w.Start ();
				w.Join ();
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				GC.WaitForPendingFinalizers ();
				var first = array [0];
				Assert.IsNotNull (JniRuntime.Current.PeekObject (first.PeerReference));
				var f = first.PeerReference;
				var o = (JavaObject) JniRuntime.Current.GetObject (ref f, JniObjectReferenceOptions.CreateNewReference);
				Assert.AreSame (first, o);
				if (oldHandle != o.PeerReference.Handle) {
					Console.WriteLine ("Yay, object handle changed; value survived a GC!");
				} else {
					Console.WriteLine ("What is this, Android pre-ICS?!");
				}
				o.Dispose ();
				array.Dispose ();
			}
		}

		[Test]
		public void UnregisterFromRuntime ()
		{
			int registeredCount = JniRuntime.Current.GetSurfacedObjects ().Count;
			JniObjectReference l;
			JavaObject o;
			using (o = new JavaObject ()) {
				l   = o.PeerReference.NewLocalRef ();
				Assert.AreEqual (JniObjectReferenceType.Global, o.PeerReference.Type);
				Assert.AreEqual (registeredCount+1, JniRuntime.Current.GetSurfacedObjects ().Count);
				Assert.IsNotNull (JniRuntime.Current.PeekObject (l));
				Assert.AreNotSame (l, o.PeerReference);
				Assert.AreSame (o, JniRuntime.Current.PeekObject (l));
			}
			Assert.AreEqual (registeredCount, JniRuntime.Current.GetSurfacedObjects ().Count);
			Assert.IsNull (JniRuntime.Current.PeekObject (l));
			JniEnvironment.References.Dispose (ref l);
			Assert.Throws<ObjectDisposedException> (() => o.UnregisterFromRuntime ());
		}

		[Test]
		public void RegisterWithVM_ThrowsOnDuplicateEntry ()
		{
			using (var original = new JavaObject ()) {
				var p       = original.PeerReference;
				Assert.Throws<NotSupportedException> (() => new JavaObject (ref p, JniObjectReferenceOptions.CreateNewReference));
			}
		}

		[Test]
		public void UnreferencedInstanceIsCollected ()
		{
			JniObjectReference  oldHandle = new JniObjectReference ();
			WeakReference r = null;
			var t = new Thread (() => {
					var v     = new JavaObject ();
					oldHandle = v.PeerReference.NewWeakGlobalRef ();
					r         = new WeakReference (v);
			});
			t.Start ();
			t.Join ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (r.IsAlive);
			Assert.IsNull (r.Target);
			Assert.IsNull (JniRuntime.Current.PeekObject (oldHandle));
			JniEnvironment.References.Dispose (ref oldHandle);
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
			var h = o.PeerReference;
			var p = o.JniPeerMembers;

			// These should throw
			Assert.Throws<ObjectDisposedException> (() => o.GetHashCode ());
			Assert.Throws<ObjectDisposedException> (() => o.UnregisterFromRuntime ());
			Assert.Throws<ObjectDisposedException> (() => o.ToString ());
			Assert.Throws<ObjectDisposedException> (() => o.Equals (o));
		}

		[Test]
		public unsafe void Ctor ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var lref = t.NewObject (c, null);
				Assert.IsTrue (lref.IsValid);
				using (var o = new JavaObject (ref lref, JniObjectReferenceOptions.CreateNewReference)) {
					Assert.IsTrue (lref.IsValid);
					Assert.AreNotSame (lref, o.PeerReference);
				}
				using (var o = new JavaObject (ref lref, JniObjectReferenceOptions.DisposeSourceReference)) {
					Assert.IsFalse (lref.IsValid);
					Assert.AreNotSame (lref, o.PeerReference);
				}
			}
		}

		[Test]
		public void Ctor_Exceptions ()
		{
			var r   = new JniObjectReference ();
			Assert.Throws<ArgumentException> (() => new JavaObject (ref r, JniObjectReferenceOptions.DisposeSourceReference));

			// Note: This may break if/when JavaVM provides "default"
			Assert.Throws<NotSupportedException> (() => new JavaObjectWithNoJavaPeer ());
			Assert.Throws<JavaException> (() => new JavaObjectWithMissingJavaPeer ()).Dispose ();
		}

		[Test]
		public void CrossThreadSharingRequresRegistration ()
		{
			JavaObject o = null;
			var t = new Thread (() => {
					o = new JavaObject ();
			});
			t.Start ();
			t.Join ();
			o.ToString ();
			o.Dispose ();
		}
	}

	class JavaObjectWithNoJavaPeer : JavaObject {
	}

	[JniTypeSignature ("__this__/__type__/__had__/__better__/__not__/__Exist__")]
	class JavaObjectWithMissingJavaPeer : JavaObject {
	}

	[JniTypeSignature ("java/lang/Object")]
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

