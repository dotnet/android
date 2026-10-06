#nullable enable

using System;
using System.Threading;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("JniReferenceLeakMeasurement")]
public class JniReferenceLeakMeasurementTests
{
	[Test]
	public void SnapshotRejectsStrongCountChangeEvenIfWeakCountsAndGenerationMatch ()
	{
		int reads = 0;
		var reading = JniReferenceLeakMeasurement.ReadStableSample (
			() => 7, () => ++reads == 1 ? 0 : 105, () => 0);

		Assert.IsFalse (reading.Stable,
			"Weak references may be created and promoted between their two reads, before completion is notified.");
		Assert.AreEqual (105, reading.Sample.Global);
	}

	[TestCase (7, 8, 100, 100, 0, 0, false)]
	[TestCase (7, 7, 100, 100, 0, 1, false)]
	[TestCase (7, 7, 100, 100, 0, 0, true)]
	public void SnapshotChecksBothCountersAndGeneration (int start, int end, int globalBefore,
		int globalAfter, int weakBefore, int weakAfter, bool stable)
	{
		int generations = 0, globals = 0, weaks = 0;
		var reading = JniReferenceLeakMeasurement.ReadStableSample (
			() => ++generations == 1 ? start : end,
			() => ++globals == 1 ? globalBefore : globalAfter,
			() => ++weaks == 1 ? weakBefore : weakAfter);

		Assert.AreEqual (stable, reading.Stable);
	}

	[Test]
	public void WaitsForWitnessAndCompletedStrongWeakTransition ()
	{
		int round = 0;
		var sample = JniReferenceLeakMeasurement.WaitForCollection (7, () => round >= 2, () => {
			return round switch {
				1 => (new JniReferenceLeakMeasurement.Sample (8, 100, 0), true),
				2 => (new JniReferenceLeakMeasurement.Sample (8, 0, 100), true),
				_ => (new JniReferenceLeakMeasurement.Sample (9, 105, 0), true),
			};
		}, () => round++, () => {}, TimeSpan.FromSeconds (1));

		Assert.AreEqual (3, round);
		Assert.AreEqual (105, sample.Global, "Completed growth must not be discarded as a retry.");
	}

	[Test]
	public void WaitsForCompletionNotificationAndStableSample ()
	{
		int round = 0;
		var sample = JniReferenceLeakMeasurement.WaitForCollection (7, () => true,
			() => (new JniReferenceLeakMeasurement.Sample (round == 1 ? 7 : 8, 100, 0), round != 2),
			() => round++, () => {}, TimeSpan.FromSeconds (1));

		Assert.AreEqual (3, round);
		Assert.AreEqual (8, sample.Generation);
	}

	[Test]
	public void EmptyHostDoesNotRequireBridgeGeneration ()
	{
		int round = 0;
		var sample = JniReferenceLeakMeasurement.WaitForCollection (null, () => true,
			() => (new JniReferenceLeakMeasurement.Sample (null, 100, 0), true),
			() => round++, () => {}, TimeSpan.FromSeconds (1));

		Assert.AreEqual (1, round);
		Assert.AreEqual (100, sample.Global);
	}

	[Test]
	public void DrainsFinalizersAndPeersAfterCompletionWithoutAnotherCollection ()
	{
		int collections = 0;
		int global = 101;
		var sample = JniReferenceLeakMeasurement.WaitForCollection (7, () => true,
			() => (new JniReferenceLeakMeasurement.Sample (8, global, 0), true),
			() => collections++, () => global--, TimeSpan.FromSeconds (1));

		Assert.AreEqual (1, collections);
		Assert.AreEqual (100, sample.Global);
	}

	[Test]
	public void RejectsStrongWeakTransitionDuringFinalDrain ()
	{
		int round = 0;
		int weak = 0;
		var sample = JniReferenceLeakMeasurement.WaitForCollection (7, () => true,
			() => (new JniReferenceLeakMeasurement.Sample (7 + round, round == 1 ? 0 : 105, weak), true),
			() => {
				round++;
				weak = 0;
			}, () => {
				if (round == 1)
					weak = 100;
			}, TimeSpan.FromSeconds (1));

		Assert.AreEqual (2, round);
		Assert.AreEqual (105, sample.Global);
	}

	[Test]
	public void FinalizedWitnessDoesNotBypassPendingWorkerAndTimeoutPreventsReuse ()
	{
		var worker = new JniReferenceLeakMeasurement.CollectionWorker ();
		using var release = new ManualResetEventSlim ();
		try {
			worker.Start (() => release.Wait ());
			Assert.Throws<AssertionException> (() => JniReferenceLeakMeasurement.WaitForCollection (
				7, () => worker.HasCompletedOutcome (() => true),
				() => (new JniReferenceLeakMeasurement.Sample (8, 100, 0), true),
				worker.ObserveCompletion, () => {}, TimeSpan.Zero, worker.MarkTimedOut));
			Assert.Throws<InvalidOperationException> (() => worker.Start (() => {}));
		} finally {
			release.Set ();
			Assert.IsTrue (SpinWait.SpinUntil (() => worker.IsCompleted, TimeSpan.FromSeconds (5)));
			worker.ObserveCompletion ();
		}
		Assert.Throws<InvalidOperationException> (worker.EnsureAvailable,
			"Even if the timed-out worker subsequently completes, the test process must not be reused.");
	}

	[Test]
	public void WorkerExceptionIsNotReportedAsLeakAssertion ()
	{
		var worker = new JniReferenceLeakMeasurement.CollectionWorker ();
		worker.Start (() => throw new InvalidOperationException ("Collection worker failed."));
		Assert.IsTrue (SpinWait.SpinUntil (() => worker.IsCompleted, TimeSpan.FromSeconds (5)));

		var error = Assert.Throws<InvalidOperationException> (worker.ObserveCompletion);
		Assert.That (error?.Message, Is.EqualTo ("Collection worker failed."));
		Assert.Throws<InvalidOperationException> (worker.EnsureAvailable);
	}

	[TestCase (false, 8, 0)]
	[TestCase (true, 7, 0)]
	[TestCase (true, 8, 1)]
	public void IncompleteCollectionTimesOut (bool collected, int generation, int weak)
	{
		var error = Assert.Throws<AssertionException> (() => JniReferenceLeakMeasurement.WaitForCollection (
			7, () => collected, () => (new JniReferenceLeakMeasurement.Sample (generation, 100, weak), true),
			() => {}, () => {}, TimeSpan.Zero));

		Assert.IsNotNull (error);
		Assert.That (error?.Message, Does.Contain ("InitialGeneration=7"));
		Assert.That (error?.Message, Does.Contain ($"Weak={weak}"));
	}
}
