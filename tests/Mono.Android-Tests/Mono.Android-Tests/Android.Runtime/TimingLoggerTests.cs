#nullable enable

using System;
using System.Reflection;

using Android.Runtime;
using NUnit.Framework;

namespace Android.RuntimeTests
{
	[TestFixture]
	[Category ("TimingLogger")]
	[NonParallelizable]
	public class TimingLoggerTests
	{
		LogCategories categories;

		[SetUp]
		public void SetUp ()
		{
			categories = Logger.Categories;
		}

		[TearDown]
		public void TearDown ()
		{
			Logger.SetLogCategories (categories);
		}

		[Test]
		public void ObsoleteGuidance ()
		{
			var attribute = typeof (TimingLogger).GetCustomAttribute<ObsoleteAttribute> ()
				?? throw new InvalidOperationException ("TimingLogger is missing ObsoleteAttribute.");
			Assert.AreEqual (
				"Android.Runtime.TimingLogger is obsolete. Use System.Diagnostics.Stopwatch for local duration measurement or System.Diagnostics.Tracing.EventSource for trace integration.",
				attribute.Message);
			Assert.IsFalse (attribute.IsError);
		}

		[Test]
		public void ActivationFollowsTimingLogCategory ()
		{
			Logger.SetLogCategories (LogCategories.None);
			using var logger = new TimingLogger ();
			Assert.IsFalse (logger.IsActive);

			Logger.SetLogCategories (LogCategories.Timing);
			logger.Start ();
			Assert.IsTrue (logger.IsActive);
		}

		[Test]
		public void StartAndStopStateTransitions ()
		{
			Logger.SetLogCategories (LogCategories.Timing);
			using var logger = new TimingLogger (startImmediately: false);
			Assert.IsFalse (logger.IsActive);

			logger.Stop ("inactive");
			Assert.IsFalse (logger.IsActive);

			logger.Start ();
			Assert.IsTrue (logger.IsActive);
			long startTimestamp = logger.StartTimestamp;

			logger.Start ();
			Assert.IsTrue (logger.IsActive);
			Assert.AreEqual (startTimestamp, logger.StartTimestamp);

			logger.Stop ("active");
			Assert.IsFalse (logger.IsActive);
			Assert.AreEqual (0, logger.StartTimestamp);
		}

		[Test]
		public void ElapsedMessageFormat ()
		{
			var elapsed = TimeSpan.FromTicks (15_000_001);

			Assert.AreEqual ("custom; elapsed: 1:1500::100", TimingLogger.FormatMessage ("custom", elapsed));
			Assert.AreEqual ("Managed Timing; elapsed: 1:1500::100", TimingLogger.FormatMessage (null, elapsed));
		}

		[Test]
		public void DisposeIsIdempotent ()
		{
			Logger.SetLogCategories (LogCategories.Timing);
			var logger = new TimingLogger ();
			Assert.IsTrue (logger.IsActive);

			logger.Dispose ();
			Assert.IsFalse (logger.IsActive);

			logger.Dispose ();
			Assert.IsFalse (logger.IsActive);

			logger.Start ();
			Assert.IsFalse (logger.IsActive);
		}
	}
}
