#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Android.Runtime;
using Java.Interop;
using Java.InteropTests;
using Microsoft.Android.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.RuntimeTests
{
	[TestClass]
	public class RuntimePeerEventSourceTests
	{
		readonly TestContext testContext;

		public RuntimePeerEventSourceTests (TestContext testContext)
		{
			this.testContext = testContext;
		}

		[TestMethod]
		public void ManagedConstructionAndDispose_EmitLifecycleEvents ()
		{
			using var listener = new PeerEventListener (RuntimeEventSource.PeerLifecycleKeyword);

			int managedHash;
			int jniHash;
			using (var instance = new Java.Lang.Integer (42)) {
				managedHash = RuntimeHelpers.GetHashCode (instance);
				jniHash = instance.JniIdentityHashCode;
			}

			var events = listener.GetEventsForManagedObject (managedHash);
			Assert.AreEqual (3, events.Count, "Expected JavaPeerCreated, ManagedPeerReleasedJavaPeer, and JavaPeerReleasedManagedPeer.");
			AssertLifecycleEvent (events [0], RuntimeEventSource.JavaPeerCreatedEventId, "JavaPeerCreated", "Java.Lang.Integer", "java/lang/Integer", jniHash, managedHash);
			AssertLifecycleEvent (events [1], RuntimeEventSource.ManagedPeerReleasedJavaPeerEventId, "ManagedPeerReleasedJavaPeer", "Java.Lang.Integer", "java/lang/Integer", jniHash, managedHash);
			AssertLifecycleEvent (events [2], RuntimeEventSource.JavaPeerReleasedManagedPeerEventId, "JavaPeerReleasedManagedPeer", "Java.Lang.Integer", "java/lang/Integer", jniHash, managedHash);
		}

		[TestMethod]
		public void WrappingRawJavaInstance_EmitsManagedPeerCreated ()
		{
			using var listener = new PeerEventListener (RuntimeEventSource.PeerLifecycleKeyword);

			int managedHash;
			int jniHash;
			using (var type = new JniType ("java/lang/Object")) {
				var ctor = type.GetConstructor ("()V");
				JniObjectReference handle;
				unsafe {
					// SAFETY: The live type's no-argument constructor is invoked without arguments.
					handle = JniEnvironment.Object.NewObject (type.PeerReference, ctor);
				}
				Assert.IsTrue (handle.IsValid, "Failed to create java/lang/Object instance.");

				using var wrapper = Java.Lang.Object.GetObject<Java.Lang.Object> (handle.Handle, JniHandleOwnership.TransferLocalRef);
				Assert.IsNotNull (wrapper);
				managedHash = RuntimeHelpers.GetHashCode (wrapper);
				jniHash = wrapper.JniIdentityHashCode;
			}

			var created = listener.GetEventsForManagedObject (managedHash)
				.FindAll (e => e.EventId == RuntimeEventSource.ManagedPeerCreatedEventId);
			Assert.AreEqual (1, created.Count, "Expected one ManagedPeerCreated event.");
			AssertLifecycleEvent (created [0], RuntimeEventSource.ManagedPeerCreatedEventId, "ManagedPeerCreated", "Java.Lang.Object", "java/lang/Object", jniHash, managedHash);
		}

		[TestMethod]
		[Timeout (60_000, CooperativeCancellation = true)]
		[TestCategory ("GCBridge")]
		public async Task GCBridgeRound_EmitsReachabilityEvents ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime) {
				Assert.Inconclusive ("This test exercises the managed CoreCLR GC bridge.");
			}

			using var listener = new PeerEventListener (RuntimeEventSource.ReachabilityKeyword);
			await GCBridgeTestHelpers.WaitForBridgeRoundsAsync (testContext.CancellationToken);

			var events = listener.GetEvents (RuntimeEventSource.JavaPeerOnlyReachableFromManagedPeerEventId);
			Assert.IsTrue (events.Count > 0, "Expected bridge rounds to report collected peers.");
			foreach (var e in events) {
				Assert.AreEqual ("JavaPeerOnlyReachableFromManagedPeer", e.EventName);
				Assert.AreEqual (8, e.Payload.Count);
				Assert.AreEqual (ExpectedRuntimeFlavor, e.Payload [4]);
				Assert.IsTrue (e.Payload [5] is int componentIndex && componentIndex >= 0);
				Assert.IsTrue (e.Payload [6] is int contextIndex && contextIndex >= 0);
				Assert.AreNotEqual (0L, e.Payload [7]);
			}
		}

		[TestMethod]
		[Timeout (60_000, CooperativeCancellation = true)]
		[TestCategory ("GCBridge")]
		public async Task GCBridgeRound_ReportsCounters ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime) {
				Assert.Inconclusive ("This test exercises the managed CoreCLR GC bridge.");
			}

			using var listener = new CounterEventListener ();
			Assert.IsTrue (SpinWait.SpinUntil (InteropCounterEventSource.AreBridgeProcessingCountersEnabled, TimeSpan.FromSeconds (5)),
				"Expected bridge processing counters to be enabled.");
			Assert.IsFalse (RuntimeEventSource.ReachabilityEventsEnabled,
				"Enabling counters should not enable per-peer reachability events.");

			while (!listener.HasCollectedPeers) {
				await GCBridgeTestHelpers.WaitForBridgeRoundsAsync (testContext.CancellationToken);
				await Task.Delay (100, testContext.CancellationToken);
			}
			Assert.IsTrue (listener.CounterNames.ContainsKey (InteropCounterEventSource.ManagedObjectsOnlyReachableFromJavaCounterName));
		}

		static string ExpectedRuntimeFlavor =>
			Microsoft.Android.Runtime.RuntimeFeature.IsNativeAotRuntime ? "NativeAOT" :
			Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime ? "CoreCLR" :
			"Unknown";

		static void AssertLifecycleEvent (CapturedEvent captured, int eventId, string eventName, string managedType, string javaType, int jniHash, int managedHash)
		{
			Assert.AreEqual (eventId, captured.EventId);
			Assert.AreEqual (eventName, captured.EventName);
			Assert.AreEqual (5, captured.Payload.Count);
			Assert.AreEqual (managedType, captured.Payload [0]);
			Assert.AreEqual (javaType, captured.Payload [1]);
			Assert.AreEqual (jniHash, captured.Payload [2]);
			Assert.AreEqual (managedHash, captured.Payload [3]);
			Assert.AreEqual (ExpectedRuntimeFlavor, captured.Payload [4]);
		}

		readonly struct CapturedEvent
		{
			public int EventId { get; }
			public string EventName { get; }
			public IReadOnlyList<object?> Payload { get; }

			public CapturedEvent (EventWrittenEventArgs eventData)
			{
				EventId = eventData.EventId;
				EventName = eventData.EventName ?? "";
				Payload = eventData.Payload ?? [];
			}
		}

		// Field initializers run before the EventListener constructor, which can call OnEventSourceCreated.
		sealed class PeerEventListener (EventKeywords keywords) : EventListener
		{
			readonly Lock eventsLock = new ();
			readonly List<CapturedEvent> events = new ();
			readonly EventKeywords keywords = keywords;

			public List<CapturedEvent> GetEvents (int eventId)
			{
				lock (eventsLock) {
					return events.FindAll (e => e.EventId == eventId);
				}
			}

			public List<CapturedEvent> GetEventsForManagedObject (int managedHash)
			{
				lock (eventsLock) {
					return events.FindAll (e => e.Payload.Count > 3 && e.Payload [3] is int hash && hash == managedHash);
				}
			}

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == RuntimeEventSource.ProviderName) {
					EnableEvents (eventSource, EventLevel.Informational, keywords);
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventId is < RuntimeEventSource.ManagedPeerCreatedEventId or > RuntimeEventSource.JavaPeerOnlyReachableFromManagedPeerEventId) {
					return;
				}
				lock (eventsLock) {
					events.Add (new CapturedEvent (eventData));
				}
			}
		}

		sealed class CounterEventListener : EventListener
		{
			public ConcurrentDictionary<string, byte> CounterNames { get; } = new (StringComparer.Ordinal);

			volatile bool hasCollectedPeers;

			public bool HasCollectedPeers => hasCollectedPeers;

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == InteropCounterEventSource.ProviderName) {
					EnableEvents (eventSource, EventLevel.Verbose, EventKeywords.None, new Dictionary<string, string?> {
						["EventCounterIntervalSec"] = "1",
					});
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventName != "EventCounters" || eventData.Payload is not { Count: > 0 } ||
						eventData.Payload [0] is not IDictionary<string, object?> payload) {
					return;
				}
				if (payload.TryGetValue ("Name", out var name) && name is string counterName) {
					CounterNames [counterName] = 0;
					if (counterName == InteropCounterEventSource.JavaObjectsOnlyReachableFromManagedCounterName &&
							payload.TryGetValue ("Max", out var max) && max is double value && value > 0) {
						hasCollectedPeers = true;
					}
				}
			}
		}
	}
}
