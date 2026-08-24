#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class InteropEventSourceTests
	{
		[Test]
		public void PeerLifecycleEvents_HaveExpectedPayload ()
		{
			using (var listener = new CapturingEventListener ()) {
				InteropEventSource.ManagedPeerCreated ("Managed.Type", "java/type", 1, 2);
				InteropEventSource.JavaPeerCreated ("Managed.Type", "java/type", 3, 4);
				InteropEventSource.ManagedPeerReleasedJavaPeer ("Managed.Type", "java/type", 5, 6);
				InteropEventSource.JavaPeerReleasedManagedPeer ("Managed.Type", "java/type", 7, 8);

				var lifecycleEvents = listener.Events.Where (e => e.EventId is >= 1 and <= 4).ToArray ();
				Assert.AreEqual (4, lifecycleEvents.Length, "Expected all lifecycle events to be emitted.");

				AssertEventPayload (lifecycleEvents [0], "ManagedPeerCreated", "Managed.Type", "java/type", 1, 2, "Unknown");
				AssertEventPayload (lifecycleEvents [1], "JavaPeerCreated", "Managed.Type", "java/type", 3, 4, "Unknown");
				AssertEventPayload (lifecycleEvents [2], "ManagedPeerReleasedJavaPeer", "Managed.Type", "java/type", 5, 6, "Unknown");
				AssertEventPayload (lifecycleEvents [3], "JavaPeerReleasedManagedPeer", "Managed.Type", "java/type", 7, 8, "Unknown");
			}
		}

		[Test]
		public void ReachabilityEvents_HaveExpectedPayload ()
		{
			using (var listener = new CapturingEventListener ()) {
				InteropEventSource.ManagedPeerOnlyReachableFromJavaPeer ("Managed.Type", "java/type", 11, 12, 2, 3, 16);
				InteropEventSource.JavaPeerOnlyReachableFromManagedPeer ("Managed.Type", "java/type", 21, 22, 4, 5, 32);

				var reachabilityEvents = listener.Events.Where (e => e.EventId is >= 5 and <= 6).ToArray ();
				Assert.AreEqual (2, reachabilityEvents.Length, "Expected both reachability events to be emitted.");

				AssertReachabilityPayload (reachabilityEvents [0], "ManagedPeerOnlyReachableFromJavaPeer", "Managed.Type", "java/type", 11, 12, "Unknown", 2, 3, 16L);
				AssertReachabilityPayload (reachabilityEvents [1], "JavaPeerOnlyReachableFromManagedPeer", "Managed.Type", "java/type", 21, 22, "Unknown", 4, 5, 32L);
			}
		}

		[Test]
		public void CallsWithoutListener_DoNotThrow ()
		{
			Assert.DoesNotThrow (() => InteropEventSource.ManagedPeerCreated ("Managed.Type", "java/type", 1, 2));
			Assert.DoesNotThrow (() => InteropEventSource.JavaPeerCreated ("Managed.Type", "java/type", 1, 2));
			Assert.DoesNotThrow (() => InteropEventSource.ManagedPeerReleasedJavaPeer ("Managed.Type", "java/type", 1, 2));
			Assert.DoesNotThrow (() => InteropEventSource.JavaPeerReleasedManagedPeer ("Managed.Type", "java/type", 1, 2));
			Assert.DoesNotThrow (() => InteropEventSource.ManagedPeerOnlyReachableFromJavaPeer ("Managed.Type", "java/type", 1, 2, 1, 1, 1));
			Assert.DoesNotThrow (() => InteropEventSource.JavaPeerOnlyReachableFromManagedPeer ("Managed.Type", "java/type", 1, 2, 1, 1, 1));
			Assert.DoesNotThrow (() => InteropCounterEventSource.ReportBridgeProcessingMetrics (1, 2));
		}

		[Test]
		public void BridgeProcessingCounters_CanBeEnabledSeparately ()
		{
			using (var listener = new CounterCapturingEventListener ()) {
				bool countersEnabled = SpinWait.SpinUntil (() => InteropCounterEventSource.AreBridgeProcessingCountersEnabled (), TimeSpan.FromSeconds (5));
				Assert.IsTrue (countersEnabled, "Expected bridge processing counters to be enabled.");
				Assert.IsFalse (InteropEventSource.IsEnabled (InteropEventSource.PeerLifecycleKeyword), "Did not expect the fine-grained interop event source to be enabled.");

				InteropCounterEventSource.ReportBridgeProcessingMetrics (11, 12);

				bool receivedCounters = SpinWait.SpinUntil (() => listener.CounterValues.Count == 2, TimeSpan.FromSeconds (5));
				Assert.IsTrue (receivedCounters, "Expected all bridge processing counters to be emitted.");
				AssertCounterValue (listener, InteropCounterEventSource.ManagedObjectsOnlyReachableFromJavaCounterName, 11);
				AssertCounterValue (listener, InteropCounterEventSource.JavaObjectsOnlyReachableFromManagedCounterName, 12);
			}
		}

		static void AssertEventPayload (CapturedEvent captured, string eventName, string managedType, string javaType, int jniHash, int managedHash, string runtimeFlavor)
		{
			Assert.AreEqual (eventName, captured.EventName);
			Assert.AreEqual (managedType, captured.Payload [0]);
			Assert.AreEqual (javaType, captured.Payload [1]);
			Assert.AreEqual (jniHash, captured.Payload [2]);
			Assert.AreEqual (managedHash, captured.Payload [3]);
			Assert.AreEqual (runtimeFlavor, captured.Payload [4]);
		}

		static void AssertReachabilityPayload (CapturedEvent captured, string eventName, string managedType, string javaType, int jniHash, int managedHash, string runtimeFlavor, int componentIndex, int contextIndex, long contextPointer)
		{
			Assert.AreEqual (eventName, captured.EventName);
			Assert.AreEqual (managedType, captured.Payload [0]);
			Assert.AreEqual (javaType, captured.Payload [1]);
			Assert.AreEqual (jniHash, captured.Payload [2]);
			Assert.AreEqual (managedHash, captured.Payload [3]);
			Assert.AreEqual (runtimeFlavor, captured.Payload [4]);
			Assert.AreEqual (componentIndex, captured.Payload [5]);
			Assert.AreEqual (contextIndex, captured.Payload [6]);
			Assert.AreEqual (contextPointer, captured.Payload [7]);
		}

		static void AssertCounterValue (CounterCapturingEventListener listener, string counterName, double expectedValue)
		{
			Assert.IsTrue (listener.CounterValues.TryGetValue (counterName, out double actualValue), $"Expected counter '{counterName}' to be present.");
			Assert.AreEqual (expectedValue, actualValue, 0.001d);
		}

		readonly struct CapturedEvent
		{
			public string EventName { get; }
			public int EventId { get; }
			public object?[] Payload { get; }

			public CapturedEvent (string eventName, int eventId, object?[] payload)
			{
				EventName = eventName;
				EventId = eventId;
				Payload = payload;
			}
		}

		sealed class CapturingEventListener : EventListener
		{
			public List<CapturedEvent> Events { get; } = new List<CapturedEvent> ();

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == InteropEventSource.ProviderName) {
					EnableEvents (eventSource, EventLevel.Verbose, EventKeywords.All);
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventName == null) {
					return;
				}

				var payload = eventData.Payload?.ToArray () ?? Array.Empty<object?> ();
				Events.Add (new CapturedEvent (eventData.EventName, eventData.EventId, payload));
			}
		}

		sealed class CounterCapturingEventListener : EventListener
		{
			public ConcurrentDictionary<string, double> CounterValues { get; } = new ConcurrentDictionary<string, double> (StringComparer.Ordinal);

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == InteropCounterEventSource.ProviderName) {
					EnableEvents (
						eventSource,
						EventLevel.Verbose,
						EventKeywords.None,
						new Dictionary<string, string?> {
							["EventCounterIntervalSec"] = "1",
						});
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventName != "EventCounters") {
					return;
				}

				if (eventData.Payload == null || eventData.Payload.Count == 0 || eventData.Payload [0] is not IEnumerable<KeyValuePair<string, object?>> payload) {
					return;
				}

				string? counterName = null;
				double? mean = null;
				foreach (var entry in payload) {
					if (entry.Key == "Name") {
						counterName = entry.Value as string;
					} else if (entry.Key == "Mean" && entry.Value is double value) {
						mean = value;
					}
				}

				if (counterName != null && mean.HasValue) {
					CounterValues [counterName] = mean.Value;
				}
			}
		}
	}
}
