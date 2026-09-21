#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Android.Runtime;
using NUnit.Framework;

namespace Android.RuntimeTests
{
	[TestFixture]
	public class RuntimeEventSourceTests
	{
		[Test]
		[Category ("GCBridge")]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "Microsoft.Android.Runtime.RuntimeEventSource", "Mono.Android")]
		public void ProviderContractAndEmission ()
		{
			Assert.IsTrue (AppContext.TryGetSwitch ("System.Diagnostics.Tracing.EventSource.IsSupported", out bool enabled) && enabled,
				"The runtime EventSource feature switch should be enabled for this test application.");
			Assert.IsTrue (Microsoft.Android.Runtime.RuntimeFeature.EventSourceSupport,
				"The runtime EventSource feature wrapper should report support as enabled.");

			var eventSourceType = Type.GetType ("Microsoft.Android.Runtime.RuntimeEventSource, Mono.Android", throwOnError: true)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource.");
			Assert.AreEqual ("Microsoft.Android.Runtime", GetConstant<string> (eventSourceType, "ProviderName"));
			Assert.AreEqual ((EventKeywords) 0x8, GetConstant<EventKeywords> (eventSourceType, "GCBridgeKeyword"));
			Assert.AreEqual (7, GetConstant<int> (eventSourceType, "GCBridgeStartEventId"));
			Assert.AreEqual (8, GetConstant<int> (eventSourceType, "GCBridgeStopEventId"));

			var gcBridgeKeyword = GetConstant<EventKeywords> (eventSourceType, "GCBridgeKeyword");
			var gcBridgeTask = GetConstant<EventTask> (eventSourceType, "GCBridgeTask");

			using var listener = new CapturingEventListener ();
			Assert.IsFalse (listener.ProviderCreated, "The provider should not be created before its lazy holder is accessed.");
			Assert.IsTrue (Invoke<bool> (eventSourceType, "GCBridgeStart"));
			Assert.IsTrue (listener.ProviderCreated, "The EventListener should observe provider creation.");
			Invoke (eventSourceType, "GCBridgeStop");

			var events = listener.GetEvents ();
			Assert.AreEqual (2, events.Count);
			AssertEvent (
				events [0],
				GetConstant<int> (eventSourceType, "GCBridgeStartEventId"),
				"GCBridgeStart",
				gcBridgeKeyword,
				gcBridgeTask,
				EventOpcode.Start);
			AssertEvent (
				events [1],
				GetConstant<int> (eventSourceType, "GCBridgeStopEventId"),
				"GCBridgeStop",
				gcBridgeKeyword,
				gcBridgeTask,
				EventOpcode.Stop);
		}

		[Test]
		[Category ("GCBridge")]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "Microsoft.Android.Runtime.RuntimeEventSource", "Mono.Android")]
		public void GCBridgeRoundEmission ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime) {
				Assert.Ignore ("This test exercises the managed CoreCLR GC bridge.");
			}

			var eventSourceType = Type.GetType ("Microsoft.Android.Runtime.RuntimeEventSource, Mono.Android", throwOnError: true)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource.");
			int startEventId = GetConstant<int> (eventSourceType, "GCBridgeStartEventId");
			int stopEventId = GetConstant<int> (eventSourceType, "GCBridgeStopEventId");

			using var listener = new CapturingEventListener ();
			int initialBridgeGeneration = JNIEnv.BridgeProcessingGeneration;
			var timeout = TimeSpan.FromSeconds (5);
			var stopwatch = Stopwatch.StartNew ();
			do {
				CreateCollectibleJavaPeer ();
				GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
				GC.WaitForPendingFinalizers ();
				Thread.Sleep (10);
			} while (JNIEnv.BridgeProcessingGeneration == initialBridgeGeneration && stopwatch.Elapsed < timeout);

			Assert.Greater (JNIEnv.BridgeProcessingGeneration, initialBridgeGeneration,
				"A GC bridge round should complete after forcing a full collection.");

			Assert.IsTrue (
				SpinWait.SpinUntil (() => {
					var bridgeEvents = listener.GetEvents (startEventId, stopEventId);
					int startCount = 0;
					int stopCount = 0;
					foreach (var captured in bridgeEvents) {
						if (captured.Id == startEventId) {
							startCount++;
						} else if (captured.Id == stopEventId) {
							stopCount++;
						}
					}
					return startCount > 0 && startCount == stopCount;
				}, timeout),
				"GC bridge Start and Stop events should be emitted as matched pairs.");

			var events = listener.GetEvents (startEventId, stopEventId);
			Assert.Greater (events.Count, 0);
			Assert.AreEqual (0, events.Count % 2);
			for (int i = 0; i < events.Count; i += 2) {
				Assert.AreEqual (startEventId, events [i].Id);
				Assert.AreEqual (0, events [i].Payload.Count);
				Assert.AreEqual (stopEventId, events [i + 1].Id);
				Assert.AreEqual (0, events [i + 1].Payload.Count);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void CreateCollectibleJavaPeer ()
		{
			_ = new Java.Lang.String ("bridge");
		}

		static T GetConstant<T> (Type type, string name)
		{
			var field = type.GetField (name, BindingFlags.NonPublic | BindingFlags.Static)
				?? throw new InvalidOperationException ($"Could not find {type.FullName}.{name}.");
			var value = field.GetRawConstantValue ()
				?? throw new InvalidOperationException ($"{type.FullName}.{name} did not have a constant value.");
			if (typeof (T).IsEnum) {
				return (T) Enum.ToObject (typeof (T), value);
			}
			return (T) value;
		}

		static void Invoke (Type type, string name, params object?[] arguments)
		{
			var method = type.GetMethod (name, BindingFlags.NonPublic | BindingFlags.Static)
				?? throw new InvalidOperationException ($"Could not find {type.FullName}.{name}.");
			method.Invoke (null, arguments);
		}

		static T Invoke<T> (Type type, string name, params object?[] arguments)
		{
			var method = type.GetMethod (name, BindingFlags.NonPublic | BindingFlags.Static)
				?? throw new InvalidOperationException ($"Could not find {type.FullName}.{name}.");
			var value = method.Invoke (null, arguments)
				?? throw new InvalidOperationException ($"{type.FullName}.{name} returned null.");
			return (T) value;
		}

		static void AssertEvent (
			CapturedEvent captured,
			int expectedId,
			string expectedName,
			EventKeywords expectedKeywords,
			EventTask expectedTask,
			EventOpcode expectedOpcode)
		{
			Assert.AreEqual (expectedId, captured.Id);
			Assert.AreEqual (expectedName, captured.Name);
			Assert.AreEqual ((long) expectedKeywords, (long) captured.Keywords & 0x0FFFFFFFFFFF);
			Assert.AreEqual (expectedTask, captured.Task);
			Assert.AreEqual (expectedOpcode, captured.Opcode);
			Assert.AreEqual (0, captured.Payload.Count);
		}

		readonly struct CapturedEvent
		{
			public int Id { get; }
			public string Name { get; }
			public EventKeywords Keywords { get; }
			public EventTask Task { get; }
			public EventOpcode Opcode { get; }
			public IReadOnlyList<object?> Payload { get; }

			public CapturedEvent (EventWrittenEventArgs eventData)
			{
				Id = eventData.EventId;
				Name = eventData.EventName ?? "";
				Keywords = eventData.Keywords;
				Task = eventData.Task;
				Opcode = eventData.Opcode;
				Payload = eventData.Payload ?? [];
			}
		}

		sealed class CapturingEventListener : EventListener
		{
			readonly List<CapturedEvent> events = new ();

			public bool ProviderCreated { get; private set; }

			public IReadOnlyList<CapturedEvent> GetEvents (params int [] eventIds)
			{
				lock (events) {
					if (eventIds.Length == 0) {
						return events.ToArray ();
					}

					var matchingEvents = new List<CapturedEvent> ();
					foreach (var captured in events) {
						if (Array.IndexOf (eventIds, captured.Id) >= 0) {
							matchingEvents.Add (captured);
						}
					}
					return matchingEvents;
				}
			}

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == "Microsoft.Android.Runtime") {
					ProviderCreated = true;
					EnableEvents (eventSource, EventLevel.Informational, EventKeywords.All);
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventName is not null) {
					lock (events) {
						events.Add (new CapturedEvent (eventData));
					}
				}
			}
		}
	}
}
