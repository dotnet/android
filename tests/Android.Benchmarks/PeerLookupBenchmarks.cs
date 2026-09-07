using Android.Runtime;
using BenchmarkDotNet.Attributes;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
[InvocationCount (OperationsPerIteration)]
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
