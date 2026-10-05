#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.CompilerServices;

using Android.Runtime;
using Java.Interop;
using Microsoft.Android.Runtime;
using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	[Category ("ReferenceTracing")]
	[NonParallelizable]
	public class ManagedObjectReferenceManagerTests {
		[TestCase (0x10, EventLevel.Verbose, true, false)]
		[TestCase (0x20, EventLevel.Verbose, false, true)]
		[TestCase (0x30, EventLevel.Informational, false, false)]
		[TestCase (0x40, EventLevel.Verbose, false, false)]
		public void ReferenceLifecycleAndEvents (long keyword, EventLevel level, bool globalEvents, bool localEvents)
		{
			using var peer = new Java.Lang.String ("reference lifecycle");
			using var listener = new ReferenceListener ((EventKeywords)keyword, level);
			var manager = new ManagedObjectReferenceManager ();
			JniObjectReference global = default, weak = default, local = default;
			int localCount = 0;
			IntPtr globalHandle = IntPtr.Zero, weakHandle = IntPtr.Zero, localHandle = IntPtr.Zero;
			try {
				global = manager.CreateGlobalReference (peer.PeerReference);
				globalHandle = global.Handle;
				Assert.IsTrue (global.IsValid);
				Assert.AreEqual (1, manager.GlobalReferenceCount);
				Assert.AreEqual (0, manager.WeakGlobalReferenceCount);
				weak = manager.CreateWeakGlobalReference (global);
				weakHandle = weak.Handle;
				Assert.IsTrue (weak.IsValid);
				Assert.AreEqual (1, manager.GlobalReferenceCount);
				Assert.AreEqual (1, manager.WeakGlobalReferenceCount);
				local = manager.CreateLocalReference (global, ref localCount);
				localHandle = local.Handle;
				Assert.IsTrue (local.IsValid);
				Assert.AreEqual (1, localCount);
			} finally {
				manager.DeleteLocalReference (ref local, ref localCount);
				manager.DeleteWeakGlobalReference (ref weak);
				manager.DeleteGlobalReference (ref global);
			}

			var events = listener.GetEvents ().Where (e => e.ThreadId == Environment.CurrentManagedThreadId).ToArray ();
			manager.DeleteLocalReference (ref local, ref localCount);
			manager.DeleteWeakGlobalReference (ref weak);
			manager.DeleteGlobalReference (ref global);
			Assert.IsFalse (manager.CreateGlobalReference (default).IsValid);
			Assert.IsFalse (manager.CreateWeakGlobalReference (default).IsValid);
			Assert.IsFalse (manager.CreateLocalReference (default, ref localCount).IsValid);
			Assert.AreEqual (IntPtr.Zero, manager.ReleaseLocalReference (ref local, ref localCount));
			Assert.AreEqual (0, manager.GlobalReferenceCount);
			Assert.AreEqual (0, manager.WeakGlobalReferenceCount);
			Assert.AreEqual (0, localCount);
			Assert.AreEqual (events.Length, listener.GetEvents ().Count (e => e.ThreadId == Environment.CurrentManagedThreadId),
				"Invalid references must not generate operations or change accounting.");

			int [] expectedIds = [11, 13, 15, 16, 14, 12];
			expectedIds = expectedIds.Where (id => id is 15 or 16 ? localEvents : globalEvents).ToArray ();
			CollectionAssert.AreEqual (expectedIds, events.Select (e => e.Id));
			string [] globalNames = ["sourceHandle", "handle", "sourceType", "referenceType", "globalCount", "weakCount", "managedThreadId", "bridgeOperation"];
			string [] localNames = ["sourceHandle", "handle", "localCount", "managedThreadId", "bridgeOperation"];
			int threadId = Environment.CurrentManagedThreadId;
			foreach (var captured in events) {
				object [] payload = captured.Id switch {
					11 => [Handle (peer.PeerReference.Handle), Handle (globalHandle), 2, 2, 1, 0, threadId, 0],
					13 => [Handle (globalHandle), Handle (weakHandle), 2, 3, 1, 1, threadId, 0],
					15 => [Handle (globalHandle), Handle (localHandle), 1, threadId, 0],
					16 => [0ul, Handle (localHandle), 0, threadId, 0],
					14 => [Handle (weakHandle), 0ul, 3, 0, 1, 0, threadId, 0],
					12 => [Handle (globalHandle), 0ul, 2, 0, 0, 0, threadId, 0],
					_ => throw new InvalidOperationException ($"Unexpected reference event {captured.Id}."),
				};
				CollectionAssert.AreEqual (captured.Id is 15 or 16 ? localNames : globalNames, captured.Names);
				CollectionAssert.AreEqual (payload, captured.Payload);
			}
			// Exercise the collected-reference wire contract without relying on a nondeterministic Java GC outcome.
			RuntimeEventSource.WeakGlobalReferenceCollected (weakHandle);
			var collectedEvents = listener.GetEvents ()
				.Where (e => e.ThreadId == threadId && e.Id == 20).ToArray ();
			Assert.AreEqual (globalEvents ? 1 : 0, collectedEvents.Length);
			if (globalEvents) {
				Assert.AreEqual (EventLevel.Verbose, collectedEvents [0].Level);
				Assert.AreEqual (0x10, (long)collectedEvents [0].Keywords & 0x0FFFFFFFFFFF);
				CollectionAssert.AreEqual (new [] { "handle" }, collectedEvents [0].Names);
				Assert.IsInstanceOf<ulong> (collectedEvents [0].Payload [0]);
				CollectionAssert.AreEqual (new object [] { Handle (weakHandle) }, collectedEvents [0].Payload);
			}
		}

		[Test]
		public void InitializesGrefLimitsFromHost ()
		{
			int previousThreshold = JNIEnvInit.gref_gc_threshold;
			int previousMaximum = JNIEnvInit.max_gref_count;
			try {
				JNIEnvInit.InitializeMaxGrefCounts (new JNIEnvInit.JnienvInitializeArgs {
					grefGcThreshold = 1800,
					maxGrefCount = 2000,
				});
				Assert.AreEqual (1800, JNIEnvInit.gref_gc_threshold);
				Assert.AreEqual (2000, JNIEnvInit.max_gref_count);
			} finally {
				JNIEnvInit.gref_gc_threshold = previousThreshold;
				JNIEnvInit.max_gref_count = previousMaximum;
			}
		}

		[Test]
		public void ReleasedLocalReferenceRemainsUsableUntilDeleted ()
		{
			using var peer = new Java.Lang.String ("transferred reference");
			using var listener = new ReferenceListener (RuntimeEventSource.LocalReferenceKeyword);
			var manager = new ManagedObjectReferenceManager ();
			int count = 0;
			var local = manager.CreateLocalReference (peer.PeerReference, ref count);
			IntPtr handle = manager.ReleaseLocalReference (ref local, ref count);
			Assert.IsFalse (local.IsValid);
			Assert.AreEqual (0, count);
			local = new JniObjectReference (handle, JniObjectReferenceType.Local);
			manager.CreatedLocalReference (local, ref count);
			try {
				CollectionAssert.AreEqual (new [] { 15, 17, 15 },
					listener.GetEvents ().Where (e => e.ThreadId == Environment.CurrentManagedThreadId).Select (e => e.Id),
					"Releasing ownership must not report JNI deletion.");
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (peer.PeerReference, local),
					"The transferred JNI handle must still refer to the original object.");
			} finally {
				manager.DeleteLocalReference (ref local, ref count);
			}
			Assert.AreEqual (0, count);
			Assert.IsFalse (local.IsValid);
			Assert.AreEqual (16, listener.GetEvents ().Last (e => e.ThreadId == Environment.CurrentManagedThreadId).Id);
		}

		[TestCase (false)]
		[TestCase (true)]
		[MethodImpl (MethodImplOptions.NoInlining)]
		public void NativeAotStacksRequireExplicitOptIn (bool collectStacks)
		{
			using var peer = new Java.Lang.String ("stack correlation");
			var keywords = RuntimeEventSource.GlobalReferenceKeyword;
			if (collectStacks) {
				keywords |= RuntimeEventSource.ReferenceStackTraceKeyword;
			}
			using var listener = new ReferenceListener (keywords);
			var manager = new ManagedObjectReferenceManager ();
			var global = manager.CreateGlobalReference (peer.PeerReference);
			IntPtr referenceHandle = global.Handle;
			ulong handle = Handle (referenceHandle);
			manager.DeleteGlobalReference (ref global);
			RuntimeEventSource.WeakGlobalReferenceCollected (referenceHandle);
			var events = listener.GetEvents ().Where (e => e.ThreadId == Environment.CurrentManagedThreadId).ToArray ();
			bool expectStacks = collectStacks && Microsoft.Android.Runtime.RuntimeFeature.IsNativeAotRuntime;
			CollectionAssert.AreEqual (expectStacks ? new [] { 11, 21, 12, 21, 20, 21 } : new [] { 11, 12, 20 }, events.Select (e => e.Id));
			foreach (var captured in events.Where (e => e.Id == 21)) {
				CollectionAssert.AreEqual (new [] { "referenceEventId", "handle", "managedThreadId", "stackTrace" }, captured.Names);
				Assert.AreEqual (4, captured.Payload.Length);
				int index = Array.IndexOf (events, captured);
				Assert.AreEqual (events [index - 1].Id, captured.Payload [0],
					"The stack must identify the immediately preceding reference operation, including collection.");
				Assert.AreEqual (handle, captured.Payload [1]);
				Assert.AreEqual (Environment.CurrentManagedThreadId, captured.Payload [2]);
				var stack = captured.Payload [3] as string ?? throw new InvalidOperationException ("The stack payload should be a string.");
				StringAssert.Contains (nameof (NativeAotStacksRequireExplicitOptIn), stack);
			}
		}

		[Test]
		public void DisabledTracingDoesNotAllocateDuringReferenceOperations ()
		{
			using var peer = new Java.Lang.String ("disabled tracing");
			using var listener = new ReferenceListener (RuntimeEventSource.GCBridgeKeyword);
			var manager = new ManagedObjectReferenceManager ();
			var reference = peer.PeerReference;
			for (int i = 0; i < 100; i++) {
				ExerciseReferences ();
			}
			long before = GC.GetAllocatedBytesForCurrentThread ();
			for (int i = 0; i < 1000; i++) {
				ExerciseReferences ();
			}
			long after = GC.GetAllocatedBytesForCurrentThread ();
			Assert.AreEqual (before, after);
			Assert.IsEmpty (listener.GetEvents ());

			void ExerciseReferences ()
			{
				var global = manager.CreateGlobalReference (reference);
				var weak = manager.CreateWeakGlobalReference (global);
				int count = 0;
				var local = manager.CreateLocalReference (global, ref count);
				manager.DeleteLocalReference (ref local, ref count);
				manager.DeleteWeakGlobalReference (ref weak);
				manager.DeleteGlobalReference (ref global);
				manager.WriteGlobalReferenceLine ("{");
				manager.WriteLocalReferenceLine ("{");
			}
		}

		static ulong Handle (IntPtr handle) => unchecked((ulong)(nuint)handle);

		internal sealed class ReferenceListener : EventListener {
			readonly List<(int Id, int ThreadId, object?[] Payload, string [] Names, EventLevel Level, EventKeywords Keywords)> events = [];

			public ReferenceListener (EventKeywords keyword, EventLevel level = EventLevel.Verbose)
			{
				RuntimeEventSource.Initialize ();
				foreach (var source in EventSource.GetSources ()) {
					if (source.Name == RuntimeEventSource.ProviderName) {
						EnableEvents (source, level, keyword);
					}
				}
			}

			public (int Id, int ThreadId, object?[] Payload, string [] Names, EventLevel Level, EventKeywords Keywords) [] GetEvents ()
			{
				lock (events) {
					return events.ToArray ();
				}
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (eventData.EventId != 0 && (eventData.EventId < 11 || eventData.EventId > 21)) {
					return;
				}
				lock (events) {
					events.Add ((eventData.EventId, Environment.CurrentManagedThreadId,
						eventData.Payload?.ToArray () ?? [], eventData.PayloadNames?.ToArray () ?? [],
						eventData.Level, eventData.Keywords));
				}
			}
		}
	}
}
