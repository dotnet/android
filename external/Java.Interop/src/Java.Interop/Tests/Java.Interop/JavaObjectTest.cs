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
				array.RegisterWithVM ();
				var w = new Thread (() => {
						var v       = new JavaObject ();
						oldHandle   = v.PeerReference.Handle;
						v.RegisterWithVM ();
						array [0] = v;
				});
				w.Start ();
				w.Join ();
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				GC.WaitForPendingFinalizers ();
				var first = array [0];
				Assert.IsNotNull (JavaVM.Current.PeekObject (first.PeerReference));
				var f = first.PeerReference;
				var o = (JavaObject) JavaVM.Current.GetObject (ref f, JniObjectReferenceOptions.CreateNewReference);
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
		public void RegisterWithVM ()
		{
			int registeredCount = JavaVM.Current.GetSurfacedObjects ().Count;
			JniObjectReference l;
			JavaObject o;
			using (o = new JavaObject ()) {
				l   = o.PeerReference.NewLocalRef ();
				Assert.AreEqual (JniObjectReferenceType.Local, o.PeerReference.Type);
				Assert.AreEqual (registeredCount, JavaVM.Current.GetSurfacedObjects ().Count);
				Assert.IsNull (JavaVM.Current.PeekObject (l));
				o.RegisterWithVM ();
				Assert.AreNotSame (l, o.PeerReference);
				Assert.AreEqual (JniObjectReferenceType.Global, o.PeerReference.Type);
				JniEnvironment.References.Dispose (ref l);
				l = o.PeerReference.NewLocalRef ();
				Assert.AreEqual (registeredCount + 1, JavaVM.Current.GetSurfacedObjects ().Count);
				Assert.AreSame (o, JavaVM.Current.PeekObject (l));
			}
			Assert.AreEqual (registeredCount, JavaVM.Current.GetSurfacedObjects ().Count);
			Assert.IsNull (JavaVM.Current.PeekObject (l));
			JniEnvironment.References.Dispose (ref l);
			Assert.Throws<ObjectDisposedException> (() => o.RegisterWithVM ());
		}

		[Test]
		public void RegisterWithVM_ThrowsOnDuplicateEntry ()
		{
			using (var original = new JavaObject ()) {
				original.RegisterWithVM ();
				var p       = original.PeerReference;
				var alias   = new JavaObject (ref p, JniObjectReferenceOptions.CreateNewReference);
				Assert.Throws<NotSupportedException> (() => alias.RegisterWithVM ());
				alias.Dispose ();
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
					v.RegisterWithVM ();
			});
			t.Start ();
			t.Join ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (r.IsAlive);
			Assert.IsNull (r.Target);
			Assert.IsNull (JavaVM.Current.PeekObject (oldHandle));
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
			Assert.Throws<ObjectDisposedException> (() => o.RegisterWithVM ());
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
					o.RegisterWithVM ();
			});
			t.Start ();
			t.Join ();
			o.ToString ();
			o.Dispose ();
		}

		[Test]
		public void CrossThreadSharingNotSupported ()
		{
			if (!HaveSafeHandles) {
				Assert.Ignore ("SafeHandles not used. Cross-thread sharing can't be checked.");
				return;
			}

			JavaObject o = null;
			var t = new Thread (() => o = new JavaObject ());
			t.Start ();
			t.Join ();
			Assert.Throws<NotSupportedException> (() => o.ToString ());
			o.Dispose ();
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

