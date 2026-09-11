using System.Diagnostics;
using Android.Runtime;
using BenchmarkDotNet.Attributes;
using Perfolizer.Mathematics.OutlierDetection;

namespace Xamarin.Android.Benchmarks;

[WarmupCount (3)]
[IterationCount (30)]
[Outliers (OutlierMode.DontRemove)]
public class PeerLookupGcBridgeBenchmarks
{
	const int OperationsPerInvoke = 65_536;

	Java.Lang.String? [] peers = [];
	IntPtr reference;
	Thread? gcThread;
	Exception? gcThreadException;
	ManualResetEventSlim? gcThreadReady;
	bool stopGcThread;
	int completedCollections;

	[Params (false, true)]
	public bool BridgePressure { get; set; }

	[Params (64, 512)]
	public int RegisteredPeerCount { get; set; }

	[GlobalSetup]
	public void Setup ()
	{
		peers = new Java.Lang.String? [RegisteredPeerCount];
		for (int i = 0; i < peers.Length; i++)
			peers [i] = new Java.Lang.String ($"peer-{i}");

		var firstPeer = peers [0];
		if (firstPeer == null)
			throw new InvalidOperationException ("The first Java string peer is unavailable.");
		reference = JNIEnv.NewGlobalRef (firstPeer.Handle);
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		StopGcThread ();
		DeleteGlobalReference (ref reference);
		for (int i = 0; i < peers.Length; i++) {
			peers [i]?.Dispose ();
			peers [i] = null;
		}
	}

	[IterationSetup]
	public void StartGcThread ()
	{
		if (!BridgePressure)
			return;

		stopGcThread = false;
		gcThreadException = null;
		var ready = new ManualResetEventSlim ();
		gcThreadReady = ready;
		completedCollections = 0;
		var thread = new Thread (() => {
			try {
				while (!Volatile.Read (ref stopGcThread)) {
					GC.Collect (GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
					Interlocked.Increment (ref completedCollections);
					ready.Set ();
					Thread.Sleep (1);
				}
			} catch (Exception e) {
				gcThreadException = e;
			}
		}) {
			IsBackground = true,
			Name = "Peer lookup GC pressure",
		};
		gcThread = thread;
		thread.Start ();
		if (!ready.Wait (TimeSpan.FromSeconds (30))) {
			Volatile.Write (ref stopGcThread, true);
			thread.Join (TimeSpan.FromSeconds (30));
			gcThread = null;
			gcThreadReady = null;
			ready.Dispose ();
			throw new TimeoutException ("The GC pressure thread did not complete a collection within 30 seconds.");
		}
	}

	[IterationCleanup]
	public void StopGcThread ()
	{
		var thread = gcThread;
		if (thread == null)
			return;

		Volatile.Write (ref stopGcThread, true);
		if (!thread.Join (TimeSpan.FromSeconds (30)))
			throw new TimeoutException ("The GC pressure thread did not stop within 30 seconds.");
		gcThread = null;
		gcThreadReady?.Dispose ();
		gcThreadReady = null;

		var exception = gcThreadException;
		gcThreadException = null;
		if (exception != null)
			throw new InvalidOperationException ("The GC pressure thread failed.", exception);
	}

	[Benchmark (OperationsPerInvoke = OperationsPerInvoke)]
	public Java.Lang.String? GetObjectCached ()
	{
		int collectionsBefore = Volatile.Read (ref completedCollections);
		Java.Lang.String? peer = null;
		for (int i = 0; i < OperationsPerInvoke; i++) {
			peer = Java.Lang.Object.GetObject<Java.Lang.String> (
				reference,
				JniHandleOwnership.DoNotTransfer);
		}
		WaitForCollection (collectionsBefore);
		return peer;
	}

	void WaitForCollection (int collectionsBefore)
	{
		if (!BridgePressure)
			return;

		long timeout = Stopwatch.GetTimestamp () + (30 * Stopwatch.Frequency);
		while (Volatile.Read (ref completedCollections) <= collectionsBefore) {
			if (Volatile.Read (ref gcThreadException) != null) {
				StopGcThread ();
				throw new InvalidOperationException ("The GC pressure thread failed.");
			}
			if (Stopwatch.GetTimestamp () >= timeout) {
				StopGcThread ();
				throw new TimeoutException ("No forced collection completed during the measured lookup batch.");
			}
			Thread.SpinWait (64);
		}
	}

	static void DeleteGlobalReference (ref IntPtr value)
	{
		if (value == IntPtr.Zero)
			return;
		JNIEnv.DeleteGlobalRef (value);
		value = IntPtr.Zero;
	}
}
