#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Reflection;

using NUnit.Framework;

namespace Android.RuntimeTests
{
	[TestFixture]
	public class RuntimeEventSourceTests
	{
		[Test]
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "Microsoft.Android.Runtime.RuntimeEventSource", "Mono.Android")]
		public void ProviderContractAndEmission ()
		{
			Assert.IsTrue (AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.InteropEventSource", out bool enabled) && enabled,
				"The runtime EventSource feature switch should be enabled for this test application.");

			var eventSourceType = Type.GetType ("Microsoft.Android.Runtime.RuntimeEventSource, Mono.Android", throwOnError: true)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource.");
			Assert.AreEqual ("Microsoft.Android.Runtime", GetConstant<string> (eventSourceType, "ProviderName"));
			Assert.AreEqual ((EventKeywords) 0x1, GetConstant<EventKeywords> (eventSourceType, "PeerLifecycleKeyword"));
			Assert.AreEqual ((EventKeywords) 0x2, GetConstant<EventKeywords> (eventSourceType, "ReachabilityKeyword"));
			Assert.AreEqual ((EventKeywords) 0x4, GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword"));
			Assert.AreEqual ((EventKeywords) 0x8, GetConstant<EventKeywords> (eventSourceType, "GCBridgeKeyword"));
			Assert.AreEqual (1, GetConstant<int> (eventSourceType, "ManagedPeerCreatedEventId"));
			Assert.AreEqual (2, GetConstant<int> (eventSourceType, "JavaPeerCreatedEventId"));
			Assert.AreEqual (3, GetConstant<int> (eventSourceType, "ManagedPeerReleasedJavaPeerEventId"));
			Assert.AreEqual (4, GetConstant<int> (eventSourceType, "JavaPeerReleasedManagedPeerEventId"));
			Assert.AreEqual (5, GetConstant<int> (eventSourceType, "ManagedPeerOnlyReachableFromJavaPeerEventId"));
			Assert.AreEqual (6, GetConstant<int> (eventSourceType, "JavaPeerOnlyReachableFromManagedPeerEventId"));

			var typeMapKeyword = GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword");
			var gcBridgeKeyword = GetConstant<EventKeywords> (eventSourceType, "GCBridgeKeyword");
			var gcBridgeTask = GetConstant<EventTask> (eventSourceType, "GCBridgeTask");
			var typeMapTask = GetConstant<EventTask> (eventSourceType, "TypeMapTask");
			var javaToManagedDirection = GetConstant<string> (eventSourceType, "JavaToManagedTypeMapDirection");

			using var listener = new CapturingEventListener ();
			Assert.IsTrue (Invoke<bool> (eventSourceType, "IsEnabled", typeMapKeyword));
			Assert.IsTrue (Invoke<bool> (eventSourceType, "IsEnabled", gcBridgeKeyword));

			Invoke (eventSourceType, "GCBridgeStart");
			Invoke (eventSourceType, "GCBridgeStop");
			Invoke (eventSourceType, "TypeMapLookupStart", javaToManagedDirection);
			Invoke (eventSourceType, "TypeMapLookupStop", javaToManagedDirection);

			Assert.AreEqual (4, listener.Events.Count);
			AssertEvent (
				listener.Events [0],
				GetConstant<int> (eventSourceType, "GCBridgeStartEventId"),
				"GCBridgeStart",
				gcBridgeKeyword,
				gcBridgeTask,
				EventOpcode.Start,
				expectedPayload: null);
			AssertEvent (
				listener.Events [1],
				GetConstant<int> (eventSourceType, "GCBridgeStopEventId"),
				"GCBridgeStop",
				gcBridgeKeyword,
				gcBridgeTask,
				EventOpcode.Stop,
				expectedPayload: null);
			AssertEvent (
				listener.Events [2],
				GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId"),
				"TypeMapLookupStart",
				typeMapKeyword,
				typeMapTask,
				EventOpcode.Start,
				javaToManagedDirection);
			AssertEvent (
				listener.Events [3],
				GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId"),
				"TypeMapLookupStop",
				typeMapKeyword,
				typeMapTask,
				EventOpcode.Stop,
				javaToManagedDirection);
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
			EventOpcode expectedOpcode,
			string? expectedPayload)
		{
			Assert.AreEqual (expectedId, captured.Id);
			Assert.AreEqual (expectedName, captured.Name);
			Assert.AreEqual ((long) expectedKeywords, (long) captured.Keywords & 0x0FFFFFFFFFFF);
			Assert.AreEqual (expectedTask, captured.Task);
			Assert.AreEqual (expectedOpcode, captured.Opcode);
			if (expectedPayload is null) {
				Assert.AreEqual (0, captured.Payload.Count);
			} else {
				Assert.AreEqual (1, captured.Payload.Count);
				Assert.AreEqual (expectedPayload, captured.Payload [0]);
			}
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
			public List<CapturedEvent> Events { get; } = new ();

			protected override void OnEventSourceCreated (EventSource eventSource)
			{
				if (eventSource.Name == "Microsoft.Android.Runtime") {
					EnableEvents (eventSource, EventLevel.Informational, EventKeywords.All);
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventName is not null) {
					Events.Add (new CapturedEvent (eventData));
				}
			}
		}
	}
}
