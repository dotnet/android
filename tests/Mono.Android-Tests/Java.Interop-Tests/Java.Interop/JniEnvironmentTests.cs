using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

using Java.Interop;

using Microsoft.Android.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniEnvironmentTests
	{
		[TestMethod]
		[TestCategory ("ManagedConstruction")]
		public void Transition_RejectsNullEnvironmentPointer ()
		{
			var environmentPointer = JniEnvironment.EnvironmentPointer;
			// SAFETY: Zero is rejected before JNI dispatch; do not substitute a fabricated nonzero address.
			Assert.ThrowsExactly<ArgumentNullException> (() => new JniTransition (IntPtr.Zero));
			Assert.AreEqual (environmentPointer, JniEnvironment.EnvironmentPointer);
		}

		[TestMethod]
		public unsafe void Types_IsSameObject ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				try {
					using (var ot = JniEnvironment.Types.GetTypeFromInstance (o)) {
						Assert.IsTrue (JniEnvironment.Types.IsSameObject (t.PeerReference, ot.PeerReference));
					}
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}

		}

		[TestMethod]
		public void Types_GetJniTypeNameFromInstance ()
		{
			using (var o = new JavaObject ())
				Assert.AreEqual ("java/lang/Object", JniEnvironment.Types.GetJniTypeNameFromInstance (o.PeerReference));
			using (var o = new JavaInt32Array (0))
				Assert.AreEqual ("[I", JniEnvironment.Types.GetJniTypeNameFromInstance (o.PeerReference));
		}

		[TestMethod]
		public unsafe void Handles_NewReturnToJniRef ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				try {
					var n = o.NewLocalRef ();
					JniObjectReference.Dispose (ref n);
					// warning: lref 'leak'
					var r = JniEnvironment.References.NewReturnToJniRef (o);
					var h = new JniObjectReference (r);
					Assert.AreEqual (JniEnvironment.References.GetIdentityHashCode (o), JniEnvironment.References.GetIdentityHashCode (h));
				} finally {
					JniObjectReference.Dispose (ref o);
				}
			}
		}

		[TestMethod]
		public void References_CreatedReference_InvalidRef ()
		{
			var c = JniEnvironment.LocalReferenceCount;
			var r = new JniObjectReference ();
			JniEnvironment.References.CreatedReference (r);
			Assert.AreEqual (c, JniEnvironment.LocalReferenceCount);
		}

		[TestMethod]
		public void References_CreatedReference_LocalRef ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = JniEnvironment.LocalReferenceCount;
				var r = JniEnvironment.References.NewLocalRef (t.PeerReference);
				bool accounted = false;
				try {
					Assert.IsTrue (r.IsValid);
					Assert.AreEqual (JniObjectReferenceType.Local, r.Type);
					Assert.AreEqual (c, JniEnvironment.LocalReferenceCount);

					JniEnvironment.References.CreatedReference (r);
					accounted = true;
					Assert.AreEqual (c + 1, JniEnvironment.LocalReferenceCount);
				} finally {
					if (accounted)
						JniObjectReference.Dispose (ref r);
					else if (r.IsValid)
						JniEnvironment.References.DeleteLocalRef (r.Handle);
				}
				Assert.AreEqual (c, JniEnvironment.LocalReferenceCount);
			}
		}

		[TestMethod]
		public void References_CreatedReference_GlobalRef ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				using var events = new GlobalReferenceEvents ();
				var raw = JniEnvironment.References.NewGlobalRef (t.PeerReference);
				var rawHandle = raw.Handle;
				try {
					Assert.IsTrue (raw.IsValid);
					Assert.AreEqual (JniObjectReferenceType.Global, raw.Type);
					Assert.IsFalse (events.Contains (RuntimeEventSource.GlobalReferenceCreatedEventId, rawHandle),
						"A direct JNI reference must not be accounted for.");
					Assert.ThrowsExactly<ArgumentException> (() => JniEnvironment.References.CreatedReference (raw));
					Assert.IsFalse (events.Contains (RuntimeEventSource.GlobalReferenceCreatedEventId, rawHandle),
						"Rejecting a global reference must not account for it.");
					Assert.IsFalse (events.Contains (RuntimeEventSource.GlobalReferenceDeletedEventId, rawHandle));
					Assert.IsTrue (JniEnvironment.Types.IsSameObject (t.PeerReference, raw));
				} finally {
					JniEnvironment.References.DeleteGlobalRef (rawHandle);
				}
				Assert.IsFalse (events.Contains (RuntimeEventSource.GlobalReferenceDeletedEventId, rawHandle),
					"Deleting a direct JNI reference must not change accounting.");

				var tracked = t.PeerReference.NewGlobalRef ();
				var trackedHandle = tracked.Handle;
				try {
					Assert.IsTrue (tracked.IsValid);
					Assert.IsTrue (events.Contains (RuntimeEventSource.GlobalReferenceCreatedEventId, trackedHandle),
						"A tracked reference must produce an accounting event.");
				} finally {
					JniObjectReference.Dispose (ref tracked);
				}
				Assert.IsTrue (events.Contains (RuntimeEventSource.GlobalReferenceDeletedEventId, trackedHandle));
			}
		}

		sealed class GlobalReferenceEvents : EventListener
		{
			readonly int threadId = Environment.CurrentManagedThreadId;
			readonly List<(int Id, ulong Handle)> events = new ();

			public GlobalReferenceEvents ()
			{
				RuntimeEventSource.Initialize ();
				foreach (var source in EventSource.GetSources ()) {
					if (source.Name == RuntimeEventSource.ProviderName)
						EnableEvents (source, EventLevel.Verbose, RuntimeEventSource.GlobalReferenceKeyword);
				}
				events.Clear ();
			}

			public bool Contains (int id, IntPtr handle)
			{
				var value = unchecked((ulong)(nuint)handle);
				foreach (var entry in events) {
					if (entry.Id == id && entry.Handle == value)
						return true;
				}
				return false;
			}

			protected override void OnEventWritten (EventWrittenEventArgs eventData)
			{
				if (Environment.CurrentManagedThreadId != threadId || eventData.Payload == null)
					return;
				int index = eventData.EventId switch {
					RuntimeEventSource.GlobalReferenceCreatedEventId => 1,
					RuntimeEventSource.GlobalReferenceDeletedEventId => 0,
					_ => -1,
				};
				if (index >= 0 && eventData.Payload.Count > index && eventData.Payload [index] is ulong handle)
					events.Add ((eventData.EventId, handle));
			}
		}
	}
}
