#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests;

static class JniReferenceLeakMeasurement
{
	const int Iterations = 100;
	static readonly CollectionWorker collector = new ();

	internal sealed class CollectionWorker
	{
		Task? task;
		bool timedOut;

		internal bool IsCompleted => task is { IsCompleted: true };
		internal string Status => timedOut ? $"timed out (task completed={IsCompleted})" : task?.Status.ToString () ?? "not started";

		internal bool HasCompletedOutcome (Func<bool> outcome)
			=> IsCompleted && outcome ();

		internal void EnsureAvailable ()
		{
			if (timedOut)
				throw new InvalidOperationException ("JNI leak collection previously timed out. Start a fresh test process; no further action batches or GC workers are allowed.");
			if (task != null && !task.IsCompleted)
				throw new InvalidOperationException ("A JNI leak collection worker is still running.");
			ObserveCompletion ();
		}

		internal void Start (Action collect)
		{
			EnsureAvailable ();
			task = Task.Run (collect);
		}

		internal void ObserveCompletion ()
		{
			if (task is { IsCompleted: true })
				task.GetAwaiter ().GetResult ();
		}

		internal void MarkTimedOut ()
			=> timedOut = true;
	}

	internal readonly record struct Sample (int? Generation, int Global, int Weak)
	{
		public override string ToString ()
			=> $"Generation={Generation?.ToString () ?? "none"}, Global={Global}, Weak={Weak}";
	}

	internal static void AssertNoSustainedGlobalReferenceGrowth (Action action)
	{
		collector.EnsureAvailable ();
		RunBatch (action);
		var before = CollectAndSample ();
		RunBatch (action);
		var after = CollectAndSample ();

		var message = $"JNI leak measurement: Runtime={RuntimeInformation.FrameworkDescription}, " +
			$"Architecture={RuntimeInformation.ProcessArchitecture}, Before=({before}), After=({after})";
		TestContext.WriteLine (message);
#if __ANDROID__
		Android.Util.Log.Info ("JniReferenceLeak", message);
#endif
		Assert.LessOrEqual (after.Global, before.Global,
			$"Operation should not leak global references after {Iterations} iterations. " +
			$"Before=({before}), After=({after}), Delta={after.Global - before.Global}");
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	static void RunBatch (Action action)
	{
		for (int i = 0; i < Iterations; i++)
			action ();
	}

	static Sample CollectAndSample ()
	{
#if __ANDROID__
		bool asynchronousBridge =
			(AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime", out bool coreClr) && coreClr) ||
			(AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.IsNativeAotRuntime", out bool nativeAot) && nativeAot);
		Func<int?> generation = () => asynchronousBridge ? Android.Runtime.JNIEnv.BridgeProcessingGeneration : null;
		int? initialGeneration = generation ();
		// A cycle makes bridge work necessary even when the measured operation leaves no peers.
		var witness = CreateCollectionWitness ();
		Func<bool> finalized = () => witness.IsFinalized;
#else
		Func<int?> generation = () => null;
		Func<bool> finalized = () => true;
		int? initialGeneration = null;
#endif
		bool started = false;
		return WaitForCollection (initialGeneration,
			() => started && collector.HasCompletedOutcome (finalized),
			() => ReadStableSample (generation,
				() => JniEnvironment.Runtime.GlobalReferenceCount,
				() => JniEnvironment.Runtime.WeakGlobalReferenceCount), () => {
			collector.ObserveCompletion ();
			if (!started || (collector.IsCompleted && !finalized ())) {
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				// GC/finalizer waits can block on the bridge. Keep them off the timeout thread.
				collector.Start (() => {
					GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
					GC.WaitForPendingFinalizers ();
				});
				started = true;
			}
		}, () => {
			if (!collector.IsCompleted)
				throw new InvalidOperationException ("Collection must finish before draining peers.");
			collector.ObserveCompletion ();
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
		}, TimeSpan.FromSeconds (10), collector.MarkTimedOut,
			() => $"CollectionWorker={collector.Status}, WitnessFinalized={finalized ()}");
	}

	internal static (Sample Sample, bool Stable) ReadStableSample (Func<int?> generation,
		Func<int> globalCount, Func<int> weakCount)
	{
		int? start = generation ();
		int weakBefore = weakCount ();
		int globalBefore = globalCount ();
		int weakAfter = weakCount ();
		int globalAfter = globalCount ();
		int? end = generation ();
		return (new Sample (end, globalAfter, weakAfter),
			start == end && weakBefore == weakAfter && globalBefore == globalAfter);
	}

	internal static Sample WaitForCollection (int? initialGeneration, Func<bool> collected,
		Func<(Sample Sample, bool Stable)> readSample, Action collect, Action drain, TimeSpan timeout,
		Action? onTimeout = null, Func<string>? diagnostics = null)
	{
		var stopwatch = Stopwatch.StartNew ();
		Sample last = default;
		do {
			collect ();
			bool witnessCollected = collected ();
			var reading = readSample ();
			last = reading.Sample;
			// Do not poll for a count below the baseline: a completed round with growth must fail.
			// Weak globals are transient during bridge processing; never sample that phase.
			if (witnessCollected && reading.Stable && last.Weak == 0 &&
				(initialGeneration == null || last.Generation != initialGeneration)) {
				// Drain after the completion notification, without starting another collection.
				drain ();
				reading = readSample ();
				last = reading.Sample;
				if (reading.Stable && last.Weak == 0)
					return last;
			}
			Thread.Sleep (1);
		} while (stopwatch.Elapsed < timeout);

		onTimeout?.Invoke ();
		Assert.Fail ($"JNI leak measurement did not complete collection within {timeout}. " +
			$"InitialGeneration={initialGeneration?.ToString () ?? "none"}, Last=({last}), OutcomeReached={collected ()}. {diagnostics?.Invoke ()}");
		throw new InvalidOperationException ("Unreachable after Assert.Fail.");
	}

#if __ANDROID__
	[MethodImpl (MethodImplOptions.NoInlining)]
	static CollectionState CreateCollectionWitness ()
	{
		var state = new CollectionState ();
		var peer = new CollectionWitness { State = state };
		peer.Next = peer;
		return state;
	}

	sealed class CollectionState
	{
		int finalized;

		public bool IsFinalized => Volatile.Read (ref finalized) != 0;

		public void MarkFinalized ()
			=> Volatile.Write (ref finalized, 1);
	}

	sealed class CollectionWitness : Java.Lang.Object
	{
		public CollectionWitness ()
		{
		}

		public CollectionWitness? Next { get; set; }
		public CollectionState? State { get; set; }

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			if (!disposing)
				State?.MarkFinalized ();
		}
	}
#endif
}
