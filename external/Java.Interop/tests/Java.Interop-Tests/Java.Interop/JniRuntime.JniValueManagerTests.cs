using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniRuntimeJniValueManagerTests : JavaVMFixture {

		[Test]
		public void CreateValue ()
		{
			using (var vm  = new MyValueManager ())
			using (var o = new JavaObject ()) {
				vm.OnSetRuntime (JniRuntime.CurrentRuntime);

				var r = o.PeerReference;
				var x = (IJavaPeerable) vm.CreateValue (ref r, JniObjectReferenceOptions.Copy);
				Assert.AreNotSame (o, x);
				x.Dispose ();

				x = vm.CreateValue<IJavaPeerable> (ref r, JniObjectReferenceOptions.Copy);
				Assert.AreNotSame (o, x);
				x.Dispose ();
			}
		}

		[Test]
		public void GetPeer_ReturnsRegisteredPeerConcurrently ()
		{
			using (var source = new JavaObject ())
			using (var vm = new ConcurrentGetPeerValueManager (source.JniPeerMembers))
			using (var start = new Barrier (2)) {
				vm.OnSetRuntime (JniRuntime.CurrentRuntime);
				vm.InitialPeekBarrier = start;

				var first = Task.Run (() => vm.GetPeer (source.PeerReference));
				var second = Task.Run (() => vm.GetPeer (source.PeerReference));
				Task.WaitAll (first, second);

				Assert.AreEqual (1, vm.CreatePeerCount);
				Assert.AreSame (first.Result, second.Result);
			}
		}

		class ConcurrentGetPeerValueManager : MyValueManager {

			readonly JniPeerMembers peerMembers;
			IJavaPeerable registeredPeer;
			int peekPeerCount;
			int createPeerCount;

			public ConcurrentGetPeerValueManager (JniPeerMembers peerMembers)
			{
				this.peerMembers = peerMembers;
			}

			public Barrier InitialPeekBarrier { get; set; }

			public int CreatePeerCount => createPeerCount;

			public override IJavaPeerable PeekPeer (JniObjectReference reference)
			{
				var peer = Volatile.Read (ref registeredPeer);
				if (Interlocked.Increment (ref peekPeerCount) <= 2 && InitialPeekBarrier != null) {
					var barrier = InitialPeekBarrier;
					if (!barrier.SignalAndWait (TimeSpan.FromSeconds (10))) {
						throw new TimeoutException ("Timed out waiting for concurrent GetPeer() calls.");
					}
				}
				return peer;
			}

			public override IJavaPeerable CreatePeer (
					ref JniObjectReference reference,
					JniObjectReferenceOptions transfer,
					[DynamicallyAccessedMembers (Constructors)]
					Type targetType)
			{
				Interlocked.Increment (ref createPeerCount);
				var peer = new TestPeer (reference, peerMembers);
				Interlocked.CompareExchange (ref registeredPeer, peer, null);
				return peer;
			}

		}

		class TestPeer : IJavaPeerable {

			JniObjectReference reference;
			int identityHashCode;
			JniManagedPeerStates state;

			public TestPeer (JniObjectReference reference, JniPeerMembers peerMembers)
			{
				this.reference = reference;
				JniPeerMembers = peerMembers;
			}

			public int JniIdentityHashCode => identityHashCode;

			public JniObjectReference PeerReference => reference;

			public JniPeerMembers JniPeerMembers { get; }

			public JniManagedPeerStates JniManagedPeerState => state;

			public void SetJniIdentityHashCode (int value)
			{
				identityHashCode = value;
			}

			public void SetPeerReference (JniObjectReference value)
			{
				reference = value;
			}

			public void SetJniManagedPeerState (JniManagedPeerStates value)
			{
				state = value;
			}

			public void UnregisterFromRuntime ()
			{
			}

			public void DisposeUnlessReferenced ()
			{
			}

			public void Disposed ()
			{
			}

			public void Finalized ()
			{
			}

			public void Dispose ()
			{
			}
		}

		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "MyValueManager intentionally uses reflection-backed value manager behavior for tests.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "MyValueManager intentionally uses reflection-backed value manager behavior for tests.")]
		class MyValueManager : JniRuntime.ReflectionJniValueManager {

			public override void WaitForGCBridgeProcessing ()
			{
			}

			public override void CollectPeers ()
			{
			}

			public override void AddPeer (IJavaPeerable reference)
			{
			}

			public override void RemovePeer (IJavaPeerable reference)
			{
			}

			public override void FinalizePeer (IJavaPeerable reference)
			{
			}

			public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
			{
				return null;
			}

			public override IJavaPeerable PeekPeer (JniObjectReference reference)
			{
				return null;
			}

		}

		[Test]
		public void GetValue_ReturnsAlias ()
		{
			var local   = new JavaObject ();
			local.UnregisterFromRuntime ();
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (local.PeerReference));
			// GetObject must always return a value (unless handle is null, etc.).
			// However, since we called local.UnregisterFromRuntime(),
			// JniRuntime.PeekObject() is null (asserted above), but GetObject() must
			// **still** return _something_.
			// In this case, it returns an _alias_.
			// TODO: "most derived type" alias generation. (Not relevant here, but...)
			var p       = local.PeerReference;
			var alias   = JniRuntime.CurrentRuntime.ValueManager.GetValue<IJavaPeerable> (ref p, JniObjectReferenceOptions.Copy);
			Assert.AreNotSame (local, alias);
			alias.Dispose ();
			local.Dispose ();
		}

		[Test]
		public void GetValue_ReturnsNullWithNullHandle ()
		{
			var r = new JniObjectReference ();
			var o = JniRuntime.CurrentRuntime.ValueManager.GetValue (ref r, JniObjectReferenceOptions.Copy);
			Assert.IsNull (o);
		}

		[Test]
		public void PeekValue ()
		{
			JniObjectReference lref;
			using (var o = new JavaObject ()) {
				lref = o.PeerReference.NewLocalRef ();
				Assert.AreSame (o, JniRuntime.CurrentRuntime.ValueManager.PeekValue (lref));
			}
			// At this point, the Java-side object is kept alive by `lref`,
			// but the wrapper instance has been disposed, and thus should
			// be unregistered, and thus unfindable.
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (lref));
			JniObjectReference.Dispose (ref lref);
		}

		public void PeekValue_BoxedObjects ()
		{
			var vm          = JniRuntime.CurrentRuntime.ValueManager;
			var marshaler   = vm.GetValueMarshaler<object> ();
			var ad          = AppDomain.CurrentDomain;

			var proxy       = marshaler.CreateGenericArgumentState (ad);
			Assert.AreSame (ad, vm.PeekValue (proxy.ReferenceValue));
			marshaler.DestroyGenericArgumentState (ad, ref proxy);

			var ex  = new InvalidOperationException ("boo!");
			proxy   = marshaler.CreateGenericArgumentState (ex);
			Assert.AreSame (ex, vm.PeekValue (proxy.ReferenceValue));
			marshaler.DestroyGenericArgumentState (ex, ref proxy);
		}

		[Test]
		public void GetValue_ReturnsNullWithInvalidSafeHandle ()
		{
			var invalid = new JniObjectReference ();
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.GetValue (ref invalid, JniObjectReferenceOptions.CopyAndDispose));
		}

#if !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
		[Test]
		public unsafe void GetValue_FindBestMatchType ()
		{
			using (var t = new JniType (TestType.JniTypeName)) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				using (var w = JniRuntime.CurrentRuntime.ValueManager.GetValue<IJavaPeerable> (ref o, JniObjectReferenceOptions.CopyAndDispose)) {
					Assert.AreEqual (typeof (TestType), w.GetType ());
					Assert.IsTrue (((TestType) w).ExecutedActivationConstructor);
				}
			}
		}
#endif  // !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
	}
}
