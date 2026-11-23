using System;
using System.Linq;
using System.Threading.Tasks;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JavaObjectTest
	{
		readonly TestContext testContext;

		public JavaObjectTest (TestContext testContext)
		{
			this.testContext = testContext;
		}

		[TestMethod]
		public void JavaReferencedInstanceSurvivesCollection ()
		{
			Console.WriteLine ("JavaReferencedInstanceSurvivesCollection");
			using (var t = new JniType ("java/lang/Object")) {
				var oldHandle = IntPtr.Zero;
				var array     = new JavaObjectArray<JavaObject> (1);
				FinalizerHelpers.PerformNoPinAction (() => {
						var v       = new JavaObject ();
						oldHandle   = v.PeerReference.Handle;
						array [0] = v;
				});
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				GC.WaitForPendingFinalizers ();
				GC.WaitForPendingFinalizers ();
				var first = array [0];
				Assert.IsNotNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (first.PeerReference));
				var f = first.PeerReference;
				var o = (JavaObject) JniRuntime.CurrentRuntime.ValueManager.GetValue (ref f, JniObjectReferenceOptions.Copy);
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

		// The surfaced-peer registry is process-wide, so other threads can add or
		// remove unrelated peers while a test runs. Count only entries for this peer.
		static int GetSurfacedEntryCount (IJavaPeerable peer)
			=> JniRuntime.CurrentRuntime.ValueManager.GetSurfacedPeers ().Count (
				info => info.SurfacedPeer.TryGetTarget (out var target) && ReferenceEquals (target, peer));

		[TestMethod]
		public void UnregisterFromRuntime ()
		{
			JniObjectReference l = default;
			JavaObject o;
			try {
				using (o = new JavaObject ()) {
					l   = o.PeerReference.NewLocalRef ();
					Assert.AreEqual (JniObjectReferenceType.Global, o.PeerReference.Type);
					Assert.AreEqual (1, GetSurfacedEntryCount (o), "Constructed peer should be surfaced exactly once.");
					Assert.IsNotNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (l));
					Assert.AreNotEqual (l, o.PeerReference);
					Assert.AreSame (o, JniRuntime.CurrentRuntime.ValueManager.PeekValue (l));
				}
				Assert.AreEqual (0, GetSurfacedEntryCount (o), "Disposed peer should no longer be surfaced.");
				Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (l));
			} finally {
				JniObjectReference.Dispose (ref l);
			}
			Assert.ThrowsExactly<ObjectDisposedException> (() => o.UnregisterFromRuntime ());
		}

		[TestMethod]
		public void RegisterWithVM_PermitsAliases ()
		{
			using (var original = new JavaObject ()) {
				var p       = original.PeerReference;
				var alias   = new JavaObject (ref p, JniObjectReferenceOptions.Copy);
				alias.Dispose ();
			}
		}

		[TestMethod]
		[Timeout (60_000, CooperativeCancellation = true)]
		public async Task UnreferencedInstanceIsCollected ()
		{
			JniObjectReference  oldHandle = new JniObjectReference ();
			WeakReference r = null;
			FinalizerHelpers.PerformNoPinAction (() => {
					var v     = new JavaObject ();
					oldHandle = v.PeerReference.NewWeakGlobalRef ();
					r         = new WeakReference (v);
			});
			await GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);
			Assert.IsFalse (r.IsAlive, "Expected the unreferenced instance to be collected.");
			Assert.IsNull (r.Target);
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (oldHandle));
			JniObjectReference.Dispose (ref oldHandle);
		}

		[TestMethod]
		public void Dispose ()
		{
			var d = false;
			var f = false;
			var o = new JavaDisposedObject (() => d = true, () => f = true);
			o.Dispose ();
			Assert.IsTrue (d);
			Assert.IsFalse (f);
		}

		[TestMethod]
		[Timeout (60_000, CooperativeCancellation = true)]
		public async Task Dispose_Finalized ()
		{
			var disposed  = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var finalized = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			FinalizerHelpers.PerformNoPinAction (() => {
				FinalizerHelpers.PerformNoPinAction (() => {
					var v     = new JavaDisposedObject (
							() => disposed.TrySetResult (true),
							() => finalized.TrySetResult (true));
					GC.KeepAlive (v);
				});
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
			});
			await GCBridgeTestHelpers.CollectUnreachablePeersAsync (testContext.CancellationToken);
			Assert.IsFalse (disposed.Task.IsCompleted);
			Assert.IsTrue (finalized.Task.IsCompleted, "Expected JavaDisposedObject.Dispose(disposing: false) to run.");
		}

		[TestMethod]
		public void ObjectDisposed ()
		{
			var o = new JavaObject ();
			o.Dispose ();

			// These should not throw
			var h = o.PeerReference;
			var p = o.JniPeerMembers;

			// These should throw
			Assert.ThrowsExactly<ObjectDisposedException> (() => o.GetHashCode ());
			Assert.ThrowsExactly<ObjectDisposedException> (() => o.UnregisterFromRuntime ());
			Assert.ThrowsExactly<ObjectDisposedException> (() => o.ToString ());
			Assert.ThrowsExactly<ObjectDisposedException> (() => o.Equals (o));
		}

		[TestMethod]
		public unsafe void Ctor ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var lref = t.NewObject (c, null);
				Assert.IsTrue (lref.IsValid);
				using (var o = new JavaObject (ref lref, JniObjectReferenceOptions.Copy)) {
					Assert.IsTrue (lref.IsValid);
					Assert.AreNotEqual (lref, o.PeerReference);
				}
				using (var o = new JavaObject (ref lref, JniObjectReferenceOptions.CopyAndDispose)) {
					Assert.IsFalse (lref.IsValid);
					Assert.AreNotEqual (lref, o.PeerReference);
				}
			}
		}

		[TestMethod]
		public void Ctor_Exceptions ()
		{
			var r   = new JniObjectReference ();
			Assert.ThrowsExactly<ArgumentException> (() => new JavaObject (ref r, JniObjectReferenceOptions.CopyAndDispose));

			Assert.ThrowsExactly<Java.Lang.ClassNotFoundException> (() => new JavaObjectWithMissingJavaPeer ()).Dispose ();
		}

		[TestMethod]
		public void CrossThreadSharingRequresRegistration ()
		{
			JavaObject o = null;
			FinalizerHelpers.PerformNoPinAction (() => {
					o = new JavaObject ();
			});
			o.ToString ();
			o.Dispose ();
		}

		[TestMethod]
		public void NestedDisposeInvocations ()
		{
			var value = new MyDisposableObject ();
			value.Dispose ();
			value.Dispose ();
		}

		[TestMethod]
		public void DisposeAccessesThis ()
		{
			var value = new GetThis ();
			value.Dispose ();
			value.Dispose ();
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class JavaObjectWithMissingJavaPeer : JavaObject {
		internal    const   string  JniTypeName = "__this__/__type__/__had__/__better__/__not__/__Exist__";
	}

	[JniTypeSignature (JniTypeName)]
	class JavaDisposedObject : JavaObject {

		internal    const   string  JniTypeName = "net/dot/jni/test/JavaDisposedObject";

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

	[JniTypeSignature (JniTypeName)]
	class MyDisposableObject : JavaObject {
		internal    const   string  JniTypeName = "net/dot/jni/test/MyDisposableObject";

		bool _isDisposed;

		public MyDisposableObject ()
		{
		}

		protected override void Dispose (bool disposing)
		{
			if (_isDisposed) {
				return;
			}
			_isDisposed = true;
			if (this.PeerReference.IsValid)
				this.Dispose ();
			base.Dispose (disposing);
		}
	}
}
