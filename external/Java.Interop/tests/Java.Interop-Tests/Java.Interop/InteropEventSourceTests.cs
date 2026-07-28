#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class InteropEventSourceTests
	{
		[Test]
		public void WrapperLifecycleEvents_HaveExpectedPayload ()
		{
			using (var listener = new CapturingEventListener ()) {
				InteropEventSource.DotNetWrapperCreated ("Managed.Type", "java/type", 1, 2, "CoreCLR");
				InteropEventSource.JavaWrapperCreated ("Managed.Type", "java/type", 3, 4, "CoreCLR");
				InteropEventSource.DotNetWrapperReleasedJavaReference ("Managed.Type", "java/type", 5, 6, "CoreCLR");
				InteropEventSource.JavaWrapperReleasedDotNetReference ("Managed.Type", "java/type", 7, 8, "CoreCLR");

				var lifecycleEvents = listener.Events.Where (e => e.EventId is >= 1 and <= 4).ToArray ();
				Assert.AreEqual (4, lifecycleEvents.Length, "Expected all lifecycle events to be emitted.");

				AssertEventPayload (lifecycleEvents [0], "DotNetWrapperCreated", "Managed.Type", "java/type", 1, 2, "CoreCLR");
				AssertEventPayload (lifecycleEvents [1], "JavaWrapperCreated", "Managed.Type", "java/type", 3, 4, "CoreCLR");
				AssertEventPayload (lifecycleEvents [2], "DotNetWrapperReleasedJavaReference", "Managed.Type", "java/type", 5, 6, "CoreCLR");
				AssertEventPayload (lifecycleEvents [3], "JavaWrapperReleasedDotNetReference", "Managed.Type", "java/type", 7, 8, "CoreCLR");
			}
		}

		[Test]
		public void ReachabilityEvents_HaveExpectedPayload ()
		{
			using (var listener = new CapturingEventListener ()) {
				InteropEventSource.DotNetObjectOnlyReachableFromJava ("Managed.Type", "java/type", 11, 12, "NativeAOT", 2, 3, 16);
				InteropEventSource.JavaObjectOnlyReachableFromDotNet ("Managed.Type", "java/type", 21, 22, "NativeAOT", 4, 5, 32);

				var reachabilityEvents = listener.Events.Where (e => e.EventId is >= 5 and <= 6).ToArray ();
				Assert.AreEqual (2, reachabilityEvents.Length, "Expected both reachability events to be emitted.");

				AssertReachabilityPayload (reachabilityEvents [0], "DotNetObjectOnlyReachableFromJava", "Managed.Type", "java/type", 11, 12, "NativeAOT", 2, 3, 16L);
				AssertReachabilityPayload (reachabilityEvents [1], "JavaObjectOnlyReachableFromDotNet", "Managed.Type", "java/type", 21, 22, "NativeAOT", 4, 5, 32L);
			}
		}

		[Test]
		public void CallsWithoutListener_DoNotThrow ()
		{
			Assert.DoesNotThrow (() => InteropEventSource.DotNetWrapperCreated ("Managed.Type", "java/type", 1, 2, "MonoVM"));
			Assert.DoesNotThrow (() => InteropEventSource.JavaWrapperCreated ("Managed.Type", "java/type", 1, 2, "MonoVM"));
			Assert.DoesNotThrow (() => InteropEventSource.DotNetWrapperReleasedJavaReference ("Managed.Type", "java/type", 1, 2, "MonoVM"));
			Assert.DoesNotThrow (() => InteropEventSource.JavaWrapperReleasedDotNetReference ("Managed.Type", "java/type", 1, 2, "MonoVM"));
			Assert.DoesNotThrow (() => InteropEventSource.DotNetObjectOnlyReachableFromJava ("Managed.Type", "java/type", 1, 2, "MonoVM", 1, 1, 1));
			Assert.DoesNotThrow (() => InteropEventSource.JavaObjectOnlyReachableFromDotNet ("Managed.Type", "java/type", 1, 2, "MonoVM", 1, 1, 1));
		}

		static void AssertEventPayload (CapturedEvent captured, string eventName, string managedType, string javaType, int jniHash, int managedHash, string runtimeMode)
		{
			Assert.AreEqual (eventName, captured.EventName);
			Assert.AreEqual (managedType, captured.Payload [0]);
			Assert.AreEqual (javaType, captured.Payload [1]);
			Assert.AreEqual (jniHash, captured.Payload [2]);
			Assert.AreEqual (managedHash, captured.Payload [3]);
			Assert.AreEqual (runtimeMode, captured.Payload [4]);
		}

		static void AssertReachabilityPayload (CapturedEvent captured, string eventName, string managedType, string javaType, int jniHash, int managedHash, string runtimeMode, int componentIndex, int contextIndex, long contextPointer)
		{
			Assert.AreEqual (eventName, captured.EventName);
			Assert.AreEqual (managedType, captured.Payload [0]);
			Assert.AreEqual (javaType, captured.Payload [1]);
			Assert.AreEqual (jniHash, captured.Payload [2]);
			Assert.AreEqual (managedHash, captured.Payload [3]);
			Assert.AreEqual (runtimeMode, captured.Payload [4]);
			Assert.AreEqual (componentIndex, captured.Payload [5]);
			Assert.AreEqual (contextIndex, captured.Payload [6]);
			Assert.AreEqual (contextPointer, captured.Payload [7]);
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
	}
}
