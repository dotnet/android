#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.AppTests;
using Android.Content;
using Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.RuntimeTests
{
	[TestClass]
	[TestCategory ("AndroidTelemetry")]
	[DoNotParallelize]
	public class AndroidTelemetryTests
	{
		static ActivityListener CreateListener (ConcurrentQueue<System.Diagnostics.Activity> activities, ActivitySamplingResult sampling, string sourceName = AndroidTelemetry.ActivitySourceName) => new () {
			ShouldListenTo = source => source.Name == sourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> options) => sampling,
			SampleUsingParentId = (ref ActivityCreationOptions<string> options) => sampling,
			ActivityStopped = activities.Enqueue,
		};

		[TestMethod]
		public void StartupReplayHonorsOptInAndPreservesTrace ()
		{
			bool enabled = AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.StartupTracing", out bool value) && value;
			Assert.AreEqual (0, AndroidTelemetry.ReplayStartupActivities (), "Replay without a listener must not consume startup data.");

			var ignored = new ConcurrentQueue<System.Diagnostics.Activity> ();
			using (var listener = CreateListener (ignored, ActivitySamplingResult.None)) {
				ActivitySource.AddActivityListener (listener);
				Assert.AreEqual (0, AndroidTelemetry.ReplayStartupActivities (), "An unsampled replay must remain available.");
			}

			var activities = new ConcurrentQueue<System.Diagnostics.Activity> ();
			using var recording = CreateListener (activities, ActivitySamplingResult.AllDataAndRecorded);
			ActivitySource.AddActivityListener (recording);
			using var ambient = new System.Diagnostics.Activity ("ambient").SetIdFormat (ActivityIdFormat.W3C).Start ();
			var replays = new int [8];
			Parallel.For (0, replays.Length, i => {
				var previous = System.Diagnostics.Activity.Current;
				replays [i] = AndroidTelemetry.ReplayStartupActivities ();
				Assert.AreSame (previous, System.Diagnostics.Activity.Current);
			});
			int emitted = replays.Sum ();
			Assert.AreEqual (enabled ? 1 : 0, replays.Count (count => count > 0), "Only one replay may emit startup data.");
			Assert.AreSame (ambient, System.Diagnostics.Activity.Current);

			if (!enabled) {
				Assert.AreEqual (0, emitted);
				Assert.AreEqual (0, activities.Count);
				return;
			}

			var spans = activities.ToArray ();
			Assert.AreEqual (spans.Length, emitted);
			Assert.IsTrue (emitted >= 5, "Expected managed initialization and its startup phases.");
			var root = spans.Single (activity => activity.OperationName == "android.runtime.initialize");
			Assert.AreEqual (default (ActivitySpanId), root.ParentSpanId);
			Assert.AreNotEqual (ambient.TraceId, root.TraceId);
			Assert.AreEqual (true, root.GetTagItem ("android.runtime.startup.buffered"));
			Assert.AreEqual (0, root.GetTagItem ("android.runtime.startup.dropped"));
			Assert.IsTrue (root.StartTimeUtc < DateTime.UtcNow);
			Assert.IsTrue (root.Duration >= TimeSpan.Zero);
			foreach (var child in spans.Where (activity => activity != root)) {
				Assert.AreEqual (root.TraceId, child.TraceId);
				Assert.AreEqual (root.SpanId, child.ParentSpanId);
				Assert.AreEqual (true, child.GetTagItem ("android.runtime.startup.buffered"));
				Assert.IsTrue (child.StartTimeUtc >= root.StartTimeUtc);
				Assert.IsTrue (child.StartTimeUtc + child.Duration <= root.StartTimeUtc + root.Duration);
			}
			CollectionAssert.IsSubsetOf (
				new [] { "android.runtime.type_map.initialize", "android.runtime.jni.create", "android.runtime.gc_bridge.initialize", "android.runtime.jni.register_natives" },
				spans.Select (activity => activity.OperationName).ToArray ());
			Assert.AreEqual (0, AndroidTelemetry.ReplayStartupActivities (), "Startup data must be replayed only once.");
		}

		[TestMethod]
		public void LifecycleTracingEmitsOneActivityPerCallback ()
		{
			var application = Application.Context as Application ?? throw new InvalidOperationException ("The application context is not an Application.");
			var activities = new ConcurrentQueue<System.Diagnostics.Activity> ();
			using var listener = CreateListener (activities, ActivitySamplingResult.AllDataAndRecorded);
			ActivitySource.AddActivityListener (listener);
			AndroidTelemetry.EnableActivityLifecycleTracing (application);
			AndroidTelemetry.EnableActivityLifecycleTracing (application);

			using var intent = new Intent (application, typeof (RenamedActivity));
			intent.AddFlags (ActivityFlags.NewTask);
			application.StartActivity (intent);

			string? activityName = typeof (RenamedActivity).FullName;
			Assert.IsTrue (SpinWait.SpinUntil (() => activities.Any (activity =>
				activity.OperationName == "android.activity.destroyed" &&
				Equals (activity.GetTagItem ("android.activity.name"), activityName)), TimeSpan.FromSeconds (15)),
				"The activity lifecycle callbacks did not complete.");
			Assert.AreEqual (1, activities.Count (activity =>
				activity.OperationName == "android.activity.created" &&
				Equals (activity.GetTagItem ("android.activity.name"), activityName)));
		}

		[TestMethod]
		public void InteropTracingEmitsPeerAndTypeMapActivities ()
		{
			var activities = new ConcurrentQueue<System.Diagnostics.Activity> ();
			using var listener = CreateListener (activities, ActivitySamplingResult.AllDataAndRecorded, AndroidTelemetry.InteropActivitySourceName);
			ActivitySource.AddActivityListener (listener);

			using (var uuid = Java.Util.UUID.RandomUUID ()) {
				Assert.IsNotNull (uuid);
			}
			using (var peer = new InteropTracingPeer ()) {
				Assert.IsNotNull (peer);
			}

			var spans = activities.ToArray ();
			var createPeer = spans.FirstOrDefault (activity =>
				activity.OperationName == "android.runtime.interop.create_peer" &&
				Equals (activity.GetTagItem ("android.java.class"), "java/util/UUID"));
			Assert.IsNotNull (createPeer, "Expected a create_peer activity for java/util/UUID.");
			Assert.AreEqual (typeof (Java.Util.UUID).FullName, createPeer.GetTagItem ("android.managed.type"));

			var managedToJava = spans.FirstOrDefault (activity =>
				activity.OperationName == "android.runtime.type_map.managed_to_java" &&
				Equals (activity.GetTagItem ("android.managed.type"), typeof (InteropTracingPeer).FullName));
			Assert.IsNotNull (managedToJava, "Expected a managed_to_java type map miss for a type not used before.");
			Assert.AreEqual (true, managedToJava.GetTagItem ("android.type_map.found"));
		}

		[TestMethod]
		public void GCBridgeProcessingEmitsActivity ()
		{
			var activities = new ConcurrentQueue<System.Diagnostics.Activity> ();
			using var listener = CreateListener (activities, ActivitySamplingResult.AllDataAndRecorded);
			ActivitySource.AddActivityListener (listener);

			bool observed = SpinWait.SpinUntil (() => {
				AllocateJavaPeers ();
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				return activities.Any (activity => activity.OperationName == "android.runtime.gc_bridge.process");
			}, TimeSpan.FromSeconds (15));
			Assert.IsTrue (observed, "Expected a gc_bridge.process activity.");

			var span = activities.First (activity => activity.OperationName == "android.runtime.gc_bridge.process");
			Assert.AreEqual (default (ActivitySpanId), span.ParentSpanId);
			Assert.IsTrue (span.Duration >= TimeSpan.Zero);
			Assert.IsInstanceOfType (span.GetTagItem ("android.gc_bridge.components"), typeof (long));
			Assert.IsInstanceOfType (span.GetTagItem ("android.gc_bridge.collected"), typeof (int));
			Assert.IsTrue ((int) (span.GetTagItem ("android.gc_bridge.generation") ?? 0) > 0);
		}

		[System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		static void AllocateJavaPeers ()
		{
			for (int i = 0; i < 100; i++) {
				_ = new InteropTracingPeer ();
			}
		}

		class InteropTracingPeer : Java.Lang.Object
		{
		}
	}
}
