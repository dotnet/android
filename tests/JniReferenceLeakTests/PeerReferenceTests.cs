using System.Runtime.CompilerServices;

using Java.Interop;

namespace JniReferenceLeakTests;

[TestClass]
public sealed class PeerReferenceTests
{
	[TestMethod]
	public void UnregisterFromRuntimeRemovesSurfacedPeer ()
	{
		using (var warmup = new JavaObject ()) {
		}
		ReferenceTestHelpers.CollectGarbage ();

		JniObjectReference localReference;
		JavaObject instance;
		using (instance = new JavaObject ()) {
			localReference = instance.PeerReference.NewLocalRef ();
			Assert.AreEqual (JniObjectReferenceType.Global, instance.PeerReference.Type);
			Assert.AreEqual (1, CountSurfacedPeer (instance));
			Assert.AreSame (instance, JniRuntime.CurrentRuntime.ValueManager.PeekPeer (localReference));
		}

		Assert.AreEqual (0, CountSurfacedPeer (instance));
		Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekPeer (localReference));
		JniObjectReference.Dispose (ref localReference);
		Assert.Throws<ObjectDisposedException> (() => instance.UnregisterFromRuntime ());
	}

	[TestMethod]
	public void AddPeerDoesNotRegisterDuplicates ()
	{
		var instance = new JavaObject ();
		try {
			Assert.AreEqual (1, CountSurfacedPeer (instance));
			JniRuntime.CurrentRuntime.ValueManager.AddPeer (instance);
			Assert.AreEqual (1, CountSurfacedPeer (instance));
		} finally {
			instance.Dispose ();
		}

		Assert.AreEqual (0, CountSurfacedPeer (instance));
	}

	[TestMethod]
	public void RepeatedConstructPeerDoesNotLeakGlobalReferences ()
	{
		ReferenceTestHelpers.AssertNoGlobalReferenceLeak (() => {
			using var original = new JavaObject ();
			var handle = original.PeerReference;
			var peer = (IJavaPeerable)RuntimeHelpers.GetUninitializedObject (typeof (JavaObject));
			peer.SetPeerReference (new JniObjectReference (handle.Handle));

			try {
				var first = new JniObjectReference (handle.Handle);
				JniRuntime.CurrentRuntime.ValueManager.ConstructPeer (peer, ref first, JniObjectReferenceOptions.Copy);

				var second = new JniObjectReference (handle.Handle);
				JniRuntime.CurrentRuntime.ValueManager.ConstructPeer (peer, ref second, JniObjectReferenceOptions.Copy);
			} finally {
				peer.Dispose ();
			}
		});
	}

	[TestMethod]
	public void WeakPeerIsCollectedWithoutLeakingReferences ()
	{
		if (AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.IsNativeAotRuntime", out bool isNativeAot) && isNativeAot) {
			Assert.Inconclusive ("Failing in NativeAOT: https://github.com/dotnet/android/issues/11690");
		}

		ReferenceTestHelpers.CollectGarbage ();
		int before = JniEnvironment.Runtime.ValueManager.GetSurfacedPeers ().Count;

		WeakReference<LeakRunnable> []? weakReferences = null;
		Exception? threadException = null;
		var thread = new Thread (() => {
			try {
				weakReferences = CreateWeakReferences (100);
			} catch (Exception ex) {
				threadException = ex;
			}
		});
		thread.Start ();
		thread.Join ();

		if (threadException is not null) {
			throw new AssertFailedException ("Worker thread failed.", threadException);
		}

		ReferenceTestHelpers.CollectGarbage ();
		if (weakReferences is null) {
			throw new AssertFailedException ("The worker thread did not create the expected weak references.");
		}

		Assert.IsTrue (weakReferences.All (reference => !reference.TryGetTarget (out _)));

		int after = JniEnvironment.Runtime.ValueManager.GetSurfacedPeers ().Count;
		Assert.IsTrue (
			after - before <= 5,
			$"Surfaced peer count increased after collecting 100 weak peers. Before={before}, After={after}, Delta={after - before}");
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	static WeakReference<LeakRunnable> [] CreateWeakReferences (int count)
	{
		var instances = new LeakRunnable [count];
		var references = new WeakReference<LeakRunnable> [count];
		for (int i = 0; i < count; i++) {
			instances [i] = new LeakRunnable ();
			references [i] = new WeakReference<LeakRunnable> (instances [i]);
		}

		GC.KeepAlive (instances);
		return references;
	}

	static int CountSurfacedPeer (IJavaPeerable peer)
	{
		return JniRuntime.CurrentRuntime.ValueManager.GetSurfacedPeers ().Count (candidate => {
			return candidate.SurfacedPeer.TryGetTarget (out var target) && ReferenceEquals (target, peer);
		});
	}
}

sealed class LeakRunnable : Java.Lang.Object, Java.Lang.IRunnable
{
	public void Run ()
	{
	}
}
