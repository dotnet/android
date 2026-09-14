using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Runtime;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests;

[TestFixture]
public class StartupNoGCRegionTest
{
	static readonly TimeSpan LongFallbackTimeout = TimeSpan.FromMinutes (1);

	[Test]
	public void ReportFullyDrawnPreservesRegistration ()
	{
		var method = typeof (Activity).GetMethod (nameof (Activity.ReportFullyDrawn));
		if (method == null) {
			Assert.Fail ("Could not find Activity.ReportFullyDrawn.");
			return;
		}

		var register = method.GetCustomAttribute<RegisterAttribute> ();
		if (register == null) {
			Assert.Fail ("Activity.ReportFullyDrawn does not have RegisterAttribute.");
			return;
		}

		Assert.IsTrue (method.IsPublic);
		Assert.IsTrue (method.IsVirtual);
		Assert.AreEqual ("reportFullyDrawn", register.Name);
		Assert.AreEqual ("()V", register.Signature);
		Assert.AreEqual ("GetReportFullyDrawnHandler", register.Connector);
	}

	[Test]
	public void OnlyStartsForCoreClr ()
	{
		int startCount = 0;
		var noGCRegion = Create (
			(_, _) => {
				startCount++;
				return true;
			}
		);

		noGCRegion.Start (isCoreClrRuntime: false);
		noGCRegion.Finish ();

		Assert.AreEqual (0, startCount);
	}

	[Test]
	public void StartFailureDoesNotEndRegion ()
	{
		int endCount = 0;
		var noGCRegion = Create (
			(_, _) => false,
			() => endCount++
		);

		noGCRegion.Start (isCoreClrRuntime: true);
		noGCRegion.Finish ();

		Assert.AreEqual (0, endCount);
	}

	[Test]
	public void ExistingNoGCRegionDoesNotEndRegion ()
	{
		int endCount = 0;
		var noGCRegion = Create (
			(_, _) => throw new InvalidOperationException (),
			() => endCount++
		);

		Assert.DoesNotThrow (() => noGCRegion.Start (isCoreClrRuntime: true));
		noGCRegion.Finish ();

		Assert.AreEqual (0, endCount);
	}

	[Test]
	public void StartUsesExpectedBudget ()
	{
		long requestedBudget = 0;
		bool disallowFullBlockingGC = false;
		var noGCRegion = Create (
			(budget, disallowBlockingGC) => {
				requestedBudget = budget;
				disallowFullBlockingGC = disallowBlockingGC;
				return true;
			}
		);

		noGCRegion.Start (isCoreClrRuntime: true);
		noGCRegion.Finish ();

		Assert.AreEqual (24 * 1024 * 1024, requestedBudget);
		Assert.IsTrue (disallowFullBlockingGC);
	}

	[Test]
	public void RepeatedReportFullyDrawnEndsRegionOnce ()
	{
		int endCount = 0;
		var noGCRegion = Create (
			(_, _) => true,
			() => endCount++
		);

		noGCRegion.Start (isCoreClrRuntime: true);
		noGCRegion.Finish ();
		noGCRegion.Finish ();

		Assert.AreEqual (1, endCount);
	}

	[Test]
	public void ConcurrentReportFullyDrawnEndsRegionOnce ()
	{
		int endCount = 0;
		var noGCRegion = Create (
			(_, _) => true,
			() => Interlocked.Increment (ref endCount)
		);

		noGCRegion.Start (isCoreClrRuntime: true);
		Parallel.For (0, 16, _ => noGCRegion.Finish ());

		Assert.AreEqual (1, endCount);
	}

	[Test]
	public void RegionExhaustionIsHandledOnce ()
	{
		int endCount = 0;
		var noGCRegion = Create (
			(_, _) => true,
			() => {
				endCount++;
				throw new InvalidOperationException ();
			}
		);

		noGCRegion.Start (isCoreClrRuntime: true);

		Assert.DoesNotThrow (noGCRegion.Finish);
		Assert.DoesNotThrow (noGCRegion.Finish);
		Assert.AreEqual (1, endCount);
	}

	[Test]
	public void ReplacementRegionIsNotEndedAfterCollection ()
	{
		int collectionCount = 0;
		int endCount = 0;
		var noGCRegion = new StartupNoGCRegion (
			(_, _) => true,
			() => endCount++,
			_ => collectionCount,
			LongFallbackTimeout
		);

		noGCRegion.Start (isCoreClrRuntime: true);
		collectionCount++;
		noGCRegion.Finish ();

		Assert.AreEqual (0, endCount);
	}

	[Test]
	public void FallbackEndsRegionOnce ()
	{
		using var ended = new ManualResetEventSlim ();
		int endCount = 0;
		var noGCRegion = new StartupNoGCRegion (
			(_, _) => true,
			() => {
				Interlocked.Increment (ref endCount);
				ended.Set ();
			},
			_ => 0,
			TimeSpan.Zero
		);

		noGCRegion.Start (isCoreClrRuntime: true);

		Assert.IsTrue (ended.Wait (TimeSpan.FromSeconds (5)));
		noGCRegion.Finish ();
		Assert.AreEqual (1, endCount);
	}

	static StartupNoGCRegion Create (
		Func<long, bool, bool> tryStartNoGCRegion,
		Action? endNoGCRegion = null
	) => new (
		tryStartNoGCRegion,
		endNoGCRegion ?? (() => { }),
		_ => 0,
		LongFallbackTimeout
	);
}
