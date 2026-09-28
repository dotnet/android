#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Android.Runtime;
using Java.Interop;
using Microsoft.Android.Runtime;
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
			Assert.AreEqual ((EventKeywords) 0x4, GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword"));
			Assert.AreEqual ((EventKeywords) 0x8, GetConstant<EventKeywords> (eventSourceType, "GCBridgeKeyword"));
			Assert.AreEqual (7, GetConstant<int> (eventSourceType, "GCBridgeStartEventId"));
			Assert.AreEqual (8, GetConstant<int> (eventSourceType, "GCBridgeStopEventId"));
			Assert.AreEqual (9, GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId"));
			Assert.AreEqual (10, GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId"));

			var typeMapKeyword = GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword");
			var gcBridgeKeyword = GetConstant<EventKeywords> (eventSourceType, "GCBridgeKeyword");
			var gcBridgeTask = GetConstant<EventTask> (eventSourceType, "GCBridgeTask");
			var typeMapTask = GetConstant<EventTask> (eventSourceType, "TypeMapTask");
			var javaToManagedDirection = GetConstant<string> (eventSourceType, "JavaToManagedTypeMapDirection");
			var managedToJavaDirection = GetConstant<string> (eventSourceType, "ManagedToJavaTypeMapDirection");
			int gcBridgeStartEventId = GetConstant<int> (eventSourceType, "GCBridgeStartEventId");
			int gcBridgeStopEventId = GetConstant<int> (eventSourceType, "GCBridgeStopEventId");
			int typeMapStartEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId");
			int typeMapStopEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId");
			Assert.AreEqual ((EventTask) 2, typeMapTask);
			Assert.AreEqual ("JavaToManaged", javaToManagedDirection);
			Assert.AreEqual ("ManagedToJava", managedToJavaDirection);

			using var listener = new CapturingEventListener ();
			Assert.IsTrue (listener.ProviderCreated, "The managed GC bridge should initialize the provider during setup.");
			Assert.IsTrue (Invoke<bool> (eventSourceType, "GCBridgeStart"));
			Assert.IsTrue (listener.ProviderCreated, "The EventListener should observe the runtime EventSource.");
			Invoke (eventSourceType, "GCBridgeStop");
			Assert.IsTrue (Invoke<bool> (eventSourceType, "TypeMapLookupStart", javaToManagedDirection));
			Invoke (eventSourceType, "TypeMapLookupStop", javaToManagedDirection);

			var events = listener.GetEvents (gcBridgeStartEventId, gcBridgeStopEventId, typeMapStartEventId, typeMapStopEventId);
			Assert.AreEqual (4, events.Count);
			AssertEvent (
				events [0],
				gcBridgeStartEventId,
				"GCBridgeStart",
				gcBridgeKeyword,
				gcBridgeTask,
				EventOpcode.Start);
			AssertEvent (
				events [1],
				gcBridgeStopEventId,
				"GCBridgeStop",
				gcBridgeKeyword,
				gcBridgeTask,
				EventOpcode.Stop,
				expectedPayload: null);
			AssertEvent (
				events [2],
				typeMapStartEventId,
				"TypeMapLookupStart",
				typeMapKeyword,
				typeMapTask,
				EventOpcode.Start,
				javaToManagedDirection);
			AssertEvent (
				events [3],
				typeMapStopEventId,
				"TypeMapLookupStop",
				typeMapKeyword,
				typeMapTask,
				EventOpcode.Stop,
				javaToManagedDirection);
		}

		[Test]
		[Category ("TypeMap")]
		public void JavaToManagedTypeMapLookup_EmitsOnlyForCachePopulation ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string jniName = "net/dot/android/test/RuntimeEventSourceMappedPeer";
			var instance = TrimmableTypeMap.Instance;
			GetJniProxyCache (instance).TryRemove (jniName, out _);

			var eventSourceType = GetRuntimeEventSourceType ();
			int startEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId");
			int stopEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId");
			var keyword = GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword");
			var task = GetConstant<EventTask> (eventSourceType, "TypeMapTask");
			var direction = GetConstant<string> (eventSourceType, "JavaToManagedTypeMapDirection");

			using var listener = new CapturingEventListener ();
			Assert.IsTrue (instance.TryGetTargetType (jniName, out var first));
			Assert.AreEqual (typeof (RuntimeEventSourceMappedPeer), first);
			Assert.IsTrue (instance.TryGetTargetType (jniName, out var second));
			Assert.AreEqual (first, second);

			var events = listener.GetEvents (startEventId, stopEventId);
			Assert.AreEqual (2, events.Count, "A cache hit must not emit another type-map pair.");
			AssertTypeMapPair (events, 0, startEventId, stopEventId, keyword, task, direction);
		}

		[Test]
		[Category ("TypeMap")]
		public void ManagedToJavaTypeMapLookup_EmitsOnlyForCachePopulation ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var managedType = typeof (RuntimeEventSourceMappedPeer);
			var instance = TrimmableTypeMap.Instance;
			GetProxyCache (instance).TryRemove (managedType, out _);

			var eventSourceType = GetRuntimeEventSourceType ();
			int startEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId");
			int stopEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId");
			var keyword = GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword");
			var task = GetConstant<EventTask> (eventSourceType, "TypeMapTask");
			var direction = GetConstant<string> (eventSourceType, "ManagedToJavaTypeMapDirection");

			using var listener = new CapturingEventListener ();
			Assert.IsTrue (instance.TryGetJniNameForManagedType (managedType, out var first));
			Assert.AreEqual ("net/dot/android/test/RuntimeEventSourceMappedPeer", first);
			Assert.IsTrue (instance.TryGetJniNameForManagedType (managedType, out var second));
			Assert.AreEqual (first, second);

			var events = listener.GetEvents (startEventId, stopEventId);
			Assert.AreEqual (2, events.Count, "A cache hit must not emit another type-map pair.");
			AssertTypeMapPair (events, 0, startEventId, stopEventId, keyword, task, direction);
		}

		[Test]
		[Category ("TypeMap")]
		public void FailedTypeMapLookups_EmitMatchedPairs ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string missingJniName = "net/dot/android/test/RuntimeEventSourceMissingType";
			var missingManagedType = typeof (RuntimeEventSourceTests);
			var instance = TrimmableTypeMap.Instance;
			GetJniProxyCache (instance).TryRemove (missingJniName, out _);
			GetProxyCache (instance).TryRemove (missingManagedType, out _);

			var eventSourceType = GetRuntimeEventSourceType ();
			int startEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId");
			int stopEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId");
			var keyword = GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword");
			var task = GetConstant<EventTask> (eventSourceType, "TypeMapTask");
			var javaToManaged = GetConstant<string> (eventSourceType, "JavaToManagedTypeMapDirection");
			var managedToJava = GetConstant<string> (eventSourceType, "ManagedToJavaTypeMapDirection");

			using var listener = new CapturingEventListener ();
			Assert.IsFalse (instance.TryGetTargetType (missingJniName, out var targetType));
			Assert.IsNull (targetType);
			Assert.IsFalse (instance.TryGetJniNameForManagedType (missingManagedType, out var jniName));
			Assert.IsNull (jniName);

			var events = listener.GetEvents (startEventId, stopEventId);
			Assert.AreEqual (4, events.Count);
			AssertTypeMapPair (events, 0, startEventId, stopEventId, keyword, task, javaToManaged);
			AssertTypeMapPair (events, 2, startEventId, stopEventId, keyword, task, managedToJava);
		}

		[Test]
		[Category ("TypeMap")]
		public void ThrowingTypeMapLookups_EmitMatchedPairsAndPropagate ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var instance = CreateTrimmableTypeMap (new ThrowingTypeMap ());
			var eventSourceType = GetRuntimeEventSourceType ();
			int startEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStartEventId");
			int stopEventId = GetConstant<int> (eventSourceType, "TypeMapLookupStopEventId");
			var keyword = GetConstant<EventKeywords> (eventSourceType, "TypeMapKeyword");
			var task = GetConstant<EventTask> (eventSourceType, "TypeMapTask");
			var javaToManaged = GetConstant<string> (eventSourceType, "JavaToManagedTypeMapDirection");
			var managedToJava = GetConstant<string> (eventSourceType, "ManagedToJavaTypeMapDirection");

			using var listener = new CapturingEventListener ();
			var javaException = Assert.Throws<InvalidOperationException> (() =>
				instance.TryGetTargetType ("net/dot/android/test/ThrowingType", out _));
			Assert.AreEqual (ThrowingTypeMap.ExceptionMessage, javaException?.Message);
			var managedException = Assert.Throws<InvalidOperationException> (() =>
				instance.TryGetJniNameForManagedType (typeof (RuntimeEventSourceTests), out _));
			Assert.AreEqual (ThrowingTypeMap.ExceptionMessage, managedException?.Message);

			var events = listener.GetEvents (startEventId, stopEventId);
			Assert.AreEqual (4, events.Count);
			AssertTypeMapPair (events, 0, startEventId, stopEventId, keyword, task, javaToManaged);
			AssertTypeMapPair (events, 2, startEventId, stopEventId, keyword, task, managedToJava);
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

			CapturedEvent startEvent = default;
			CapturedEvent stopEvent = default;
			Assert.IsTrue (
				SpinWait.SpinUntil (
					() => listener.TryGetEventPair (startEventId, stopEventId, out startEvent, out stopEvent),
					timeout),
				"GC bridge Start and Stop events should be emitted as matched pairs.");
			Assert.AreEqual (startEventId, startEvent.Id);
			Assert.AreEqual (0, startEvent.Payload.Count);
			Assert.AreEqual (stopEventId, stopEvent.Id);
			Assert.AreEqual (0, stopEvent.Payload.Count);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void CreateCollectibleJavaPeer ()
		{
			_ = new Java.Lang.String ("bridge");
		}

		static Type GetRuntimeEventSourceType ()
		{
			return Type.GetType ("Microsoft.Android.Runtime.RuntimeEventSource, Mono.Android", throwOnError: true)
				?? throw new InvalidOperationException ("Could not find the runtime EventSource.");
		}

		static ConcurrentDictionary<Type, JavaPeerProxy> GetProxyCache (TrimmableTypeMap instance)
		{
			var field = typeof (TrimmableTypeMap).GetField ("_proxyCache", BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new InvalidOperationException ("Could not find the managed proxy cache.");
			return field.GetValue (instance) as ConcurrentDictionary<Type, JavaPeerProxy>
				?? throw new InvalidOperationException ("Could not read the managed proxy cache.");
		}

		static ConcurrentDictionary<string, object> GetJniProxyCache (TrimmableTypeMap instance)
		{
			var field = typeof (TrimmableTypeMap).GetField ("_jniProxyCache", BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new InvalidOperationException ("Could not find the JNI proxy cache.");
			return field.GetValue (instance) as ConcurrentDictionary<string, object>
				?? throw new InvalidOperationException ("Could not read the JNI proxy cache.");
		}

		static TrimmableTypeMap CreateTrimmableTypeMap (ITypeMap typeMap)
		{
			var constructor = typeof (TrimmableTypeMap).GetConstructor (
				BindingFlags.Instance | BindingFlags.NonPublic,
				binder: null,
				[typeof (ITypeMap)],
				modifiers: null)
				?? throw new InvalidOperationException ("Could not find the TrimmableTypeMap constructor.");
			return (TrimmableTypeMap) constructor.Invoke ([typeMap]);
		}

		static void AssumeTrimmableTypeMapEnabled ()
		{
			if (!Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}
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

		static void AssertTypeMapPair (
			IReadOnlyList<CapturedEvent> events,
			int offset,
			int startEventId,
			int stopEventId,
			EventKeywords keyword,
			EventTask task,
			string direction)
		{
			AssertEvent (events [offset], startEventId, "TypeMapLookupStart", keyword, task, EventOpcode.Start, direction);
			AssertEvent (events [offset + 1], stopEventId, "TypeMapLookupStop", keyword, task, EventOpcode.Stop, direction);
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
			string? expectedPayload = null)
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
			public int ManagedThreadId { get; }

			public CapturedEvent (EventWrittenEventArgs eventData)
			{
				Id = eventData.EventId;
				Name = eventData.EventName ?? "";
				Keywords = eventData.Keywords;
				Task = eventData.Task;
				Opcode = eventData.Opcode;
				Payload = eventData.Payload ?? [];
				ManagedThreadId = Environment.CurrentManagedThreadId;
			}
		}

		sealed class CapturingEventListener : EventListener
		{
			readonly List<CapturedEvent> events = new ();

			public bool ProviderCreated { get; private set; }

			public IReadOnlyList<CapturedEvent> GetEvents (params int [] eventIds)
			{
				int managedThreadId = Environment.CurrentManagedThreadId;
				lock (events) {
					if (eventIds.Length == 0) {
						return events.FindAll (captured => captured.ManagedThreadId == managedThreadId);
					}

					var matchingEvents = new List<CapturedEvent> ();
					foreach (var captured in events) {
						if (captured.ManagedThreadId == managedThreadId && Array.IndexOf (eventIds, captured.Id) >= 0) {
							matchingEvents.Add (captured);
						}
					}
					return matchingEvents;
				}
			}

			public bool TryGetEventPair (
				int startEventId,
				int stopEventId,
				out CapturedEvent startEvent,
				out CapturedEvent stopEvent)
			{
				lock (events) {
					for (int i = 0; i + 1 < events.Count; i++) {
						if (events [i].Id == startEventId && events [i + 1].Id == stopEventId) {
							startEvent = events [i];
							stopEvent = events [i + 1];
							return true;
						}
					}
				}

				startEvent = default;
				stopEvent = default;
				return false;
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

		sealed class ThrowingTypeMap : ITypeMap
		{
			internal const string ExceptionMessage = "Test typemap lookup failure.";

			public void CollectProxyTypes (string jniName, ref JniProxyCacheBuilder builder)
			{
				throw new InvalidOperationException (ExceptionMessage);
			}

			public bool TryGetProxyType (Type managedType, out Type? proxyType)
			{
				proxyType = null;
				throw new InvalidOperationException (ExceptionMessage);
			}
		}
	}

	[Register ("net/dot/android/test/RuntimeEventSourceMappedPeer")]
	class RuntimeEventSourceMappedPeer : Java.Lang.Object
	{
	}
}
