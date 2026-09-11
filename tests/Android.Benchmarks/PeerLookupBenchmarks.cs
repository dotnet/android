using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Java.Interop;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[InvocationCount (OperationsPerIteration)]
[WarmupCount (5)]
[IterationCount (15)]
public class PeerLookupBenchmarks
{
	const int OperationsPerIteration = 4096;

	readonly IntPtr [] uncachedReferences = new IntPtr [OperationsPerIteration];
	readonly IntPtr [] newGlobalReferences = new IntPtr [OperationsPerIteration];
	readonly Java.Lang.String? [] uncachedPeers = new Java.Lang.String? [OperationsPerIteration];
	IntPtr cachedReference;
	IntPtr cachedObjectReference;
	Java.Lang.String? cachedPeer;
	Java.Lang.String? cachedObjectPeer;
	int uncachedIndex;
	int newGlobalReferenceIndex;

	[GlobalSetup]
	public void Setup ()
	{
		cachedReference = CreateGlobalString ();
		cachedObjectReference = CreateGlobalString ();

		cachedPeer = Java.Lang.Object.GetObject<Java.Lang.String> (cachedReference, JniHandleOwnership.DoNotTransfer);
		cachedObjectPeer = Java.Lang.Object.GetObject<Java.Lang.String> (cachedObjectReference, JniHandleOwnership.DoNotTransfer);
		if (cachedPeer == null || cachedObjectPeer == null)
			throw new InvalidOperationException ("Could not create the cached Java string peers.");
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		CleanupUncachedPeer ();
		CleanupNewGlobalReference ();
		cachedPeer?.Dispose ();
		cachedObjectPeer?.Dispose ();
		DeleteGlobalReference (ref cachedReference);
		DeleteGlobalReference (ref cachedObjectReference);
	}

	[IterationSetup (Target = nameof (GetObjectUncached))]
	public void SetupUncachedPeer ()
	{
		uncachedIndex = 0;
		for (int i = 0; i < uncachedReferences.Length; i++)
			uncachedReferences [i] = CreateGlobalString ();
	}

	[IterationCleanup (Target = nameof (GetObjectUncached))]
	public void CleanupUncachedPeer ()
	{
		for (int i = 0; i < uncachedReferences.Length; i++) {
			uncachedPeers [i]?.Dispose ();
			uncachedPeers [i] = null;
			DeleteGlobalReference (ref uncachedReferences [i]);
		}
	}

	[Benchmark]
	public Java.Lang.String? GetObjectUncached ()
	{
		int index = uncachedIndex++;
		return uncachedPeers [index] = Java.Lang.Object.GetObject<Java.Lang.String> (
			uncachedReferences [index],
			JniHandleOwnership.DoNotTransfer);
	}

	[Benchmark]
	public Java.Lang.String? GetObjectCached ()
	{
		return Java.Lang.Object.GetObject<Java.Lang.String> (
			cachedReference,
			JniHandleOwnership.DoNotTransfer);
	}

	[IterationSetup (Target = nameof (GetObjectCachedWithNewGlobalReference))]
	public void SetupNewGlobalReference ()
	{
		newGlobalReferenceIndex = 0;
		for (int i = 0; i < newGlobalReferences.Length; i++)
			newGlobalReferences [i] = JNIEnv.NewGlobalRef (cachedObjectReference);
	}

	[IterationCleanup (Target = nameof (GetObjectCachedWithNewGlobalReference))]
	public void CleanupNewGlobalReference ()
	{
		for (int i = 0; i < newGlobalReferences.Length; i++)
			DeleteGlobalReference (ref newGlobalReferences [i]);
	}

	[Benchmark]
	public Java.Lang.String? GetObjectCachedWithNewGlobalReference ()
	{
		int index = newGlobalReferenceIndex++;
		return Java.Lang.Object.GetObject<Java.Lang.String> (
			newGlobalReferences [index],
			JniHandleOwnership.DoNotTransfer);
	}

	static IntPtr CreateGlobalString ()
	{
		IntPtr localReference = JNIEnv.NewString ("benchmark");
		try {
			return JNIEnv.NewGlobalRef (localReference);
		} finally {
			JNIEnv.DeleteLocalRef (localReference);
		}
	}

	static void DeleteGlobalReference (ref IntPtr reference)
	{
		if (reference == IntPtr.Zero)
			return;
		JNIEnv.DeleteGlobalRef (reference);
		reference = IntPtr.Zero;
	}
}

[MemoryDiagnoser]
[WarmupCount (5)]
[IterationCount (15)]
public class PeerLookupLocalityBenchmarks
{
	const int OperationsPerIteration = 4096;

	IntPtr [] references = [];
	Java.Lang.String? [] peers = [];
	WeakReference<Java.Lang.String>? recentPeer;
	int operationIndex;

	[Params (1, 2, 8, 64)]
	public int WorkingSetSize { get; set; }

	[Params (1, 8)]
	public int ConsecutiveLookups { get; set; }

	[GlobalSetup]
	public void Setup ()
	{
		references = new IntPtr [WorkingSetSize];
		peers = new Java.Lang.String? [WorkingSetSize];
		for (int i = 0; i < references.Length; i++) {
			references [i] = CreateGlobalString (i);
			var peer = Java.Lang.Object.GetObject<Java.Lang.String> (
				references [i],
				JniHandleOwnership.DoNotTransfer);
			if (peer == null)
				throw new InvalidOperationException ($"Could not create Java string peer {i}.");
			peers [i] = peer;
		}

		var firstPeer = peers [0];
		if (firstPeer == null)
			throw new InvalidOperationException ("The first Java string peer is unavailable.");
		recentPeer = new WeakReference<Java.Lang.String> (firstPeer);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		recentPeer = null;
		for (int i = 0; i < references.Length; i++) {
			peers [i]?.Dispose ();
			peers [i] = null;
			DeleteGlobalReference (ref references [i]);
		}
	}

	[Benchmark (Baseline = true, OperationsPerInvoke = OperationsPerIteration)]
	public Java.Lang.String? CurrentRegistryLookup ()
	{
		Java.Lang.String? peer = null;
		for (int i = 0; i < OperationsPerIteration; i++) {
			peer = Java.Lang.Object.GetObject<Java.Lang.String> (
				GetNextReference (),
				JniHandleOwnership.DoNotTransfer);
		}
		return peer;
	}

	[Benchmark (OperationsPerInvoke = OperationsPerIteration)]
	public Java.Lang.String? RecentPeerIsSameObjectFastPath ()
	{
		Java.Lang.String? peer = null;
		for (int i = 0; i < OperationsPerIteration; i++) {
			IntPtr reference = GetNextReference ();
			var recent = recentPeer;
			if (recent != null &&
					recent.TryGetTarget (out peer) &&
					JniEnvironment.Types.IsSameObject (
						new JniObjectReference (reference),
						peer.PeerReference)) {
				continue;
			}

			peer = Java.Lang.Object.GetObject<Java.Lang.String> (
				reference,
				JniHandleOwnership.DoNotTransfer);
			if (peer != null) {
				if (recent == null) {
					recentPeer = new WeakReference<Java.Lang.String> (peer);
				} else {
					recent.SetTarget (peer);
				}
			}
		}
		return peer;
	}

	IntPtr GetNextReference ()
	{
		int index = (operationIndex++ / ConsecutiveLookups) % WorkingSetSize;
		return references [index];
	}

	static IntPtr CreateGlobalString (int index)
	{
		IntPtr localReference = JNIEnv.NewString ($"benchmark-{index}");
		try {
			return JNIEnv.NewGlobalRef (localReference);
		} finally {
			JNIEnv.DeleteLocalRef (localReference);
		}
	}

	static void DeleteGlobalReference (ref IntPtr reference)
	{
		if (reference == IntPtr.Zero)
			return;
		JNIEnv.DeleteGlobalRef (reference);
		reference = IntPtr.Zero;
	}
}
