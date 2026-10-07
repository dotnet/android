#if JAVA_INTEROP
using System;
using System.Globalization;
using System.Threading;

using Java.Interop;
using Microsoft.Android.Runtime;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

namespace Android.Runtime {

	enum ReferenceLogEvent {
		GlobalCreated,
		GlobalDeleted,
		WeakGlobalCreated,
		WeakGlobalDeleted,
	}

	enum GCBridgeReferenceOperation {
		None,
		Initialize,
		GlobalToWeak,
		WeakToGlobal,
	}

	internal sealed class ManagedObjectReferenceManager : JniRuntime.JniObjectReferenceManager {
		[ThreadStatic]
		static GCBridgeReferenceOperation gcBridgeReferenceOperation;

		int grefCount;
		int weakGrefCount;

		public override int GlobalReferenceCount => Volatile.Read (ref grefCount);
		public override int WeakGlobalReferenceCount => Volatile.Read (ref weakGrefCount);

		public override bool LogGlobalReferenceMessages => RuntimeFeature.EventSourceSupport && RuntimeEventSource.GlobalReferenceEventsEnabled;
		public override bool LogLocalReferenceMessages => RuntimeFeature.EventSourceSupport && RuntimeEventSource.LocalReferenceEventsEnabled;

		internal static void BeginGCBridgeReferenceOperation (GCBridgeReferenceOperation operation)
		{
			gcBridgeReferenceOperation = operation;
		}

		internal static void EndGCBridgeReferenceOperation ()
		{
			gcBridgeReferenceOperation = GCBridgeReferenceOperation.None;
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			var reference = base.CreateLocalReference (value, ref localReferenceCount);
			// Keep the feature switch outermost so ILLink removes the entire instrumentation block.
			if (RuntimeFeature.EventSourceSupport) {
				if (reference.IsValid) {
					RuntimeEventSource.LocalReferenceCreated (value.Handle, reference.Handle, localReferenceCount, gcBridgeReferenceOperation);
				}
			}
			return reference;
		}

		public override void DeleteLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (!value.IsValid) {
				return;
			}

			var reference = value;
			base.DeleteLocalReference (ref value, ref localReferenceCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.LocalReferenceDeleted (reference.Handle, localReferenceCount, gcBridgeReferenceOperation);
			}
		}

		public override void CreatedLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			if (!value.IsValid) {
				return;
			}

			base.CreatedLocalReference (value, ref localReferenceCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.LocalReferenceCreated (IntPtr.Zero, value.Handle, localReferenceCount, gcBridgeReferenceOperation);
			}
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (!value.IsValid) {
				return IntPtr.Zero;
			}

			var handle = base.ReleaseLocalReference (ref value, ref localReferenceCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.LocalReferenceReleased (handle, localReferenceCount, gcBridgeReferenceOperation);
			}
			return handle;
		}

		public override void WriteLocalReferenceLine (string format, params object?[] args)
		{
			if (RuntimeFeature.EventSourceSupport) {
				if (RuntimeEventSource.LocalReferenceEventsEnabled) {
					RuntimeEventSource.LocalReferenceDiagnostic (string.Format (CultureInfo.InvariantCulture, format, args));
				}
			}
		}

		public override void WriteGlobalReferenceLine (string format, params object?[] args)
		{
			if (RuntimeFeature.EventSourceSupport) {
				if (RuntimeEventSource.GlobalReferenceEventsEnabled) {
					RuntimeEventSource.GlobalReferenceDiagnostic (string.Format (CultureInfo.InvariantCulture, format, args));
				}
			}
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference value)
		{
			var reference = base.CreateGlobalReference (value);
			if (!reference.IsValid) {
				return reference;
			}

			int count = UpdateReferenceCount (ReferenceLogEvent.GlobalCreated, out int weakCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.GlobalReference (ReferenceLogEvent.GlobalCreated, value, reference, count, weakCount, gcBridgeReferenceOperation);
			}

			if (gcBridgeReferenceOperation == GCBridgeReferenceOperation.None && count >= JNIEnvInit.gref_gc_threshold) {
				Logger.Log (LogLevel.Warn, "monodroid-gc", count + " outstanding GREFs. Performing a full GC!");
				GC.WaitForPendingFinalizers ();
				GC.Collect ();
			}

			return reference;
		}

		public override void DeleteGlobalReference (ref JniObjectReference value)
		{
			if (!value.IsValid) {
				return;
			}

			int count = UpdateReferenceCount (ReferenceLogEvent.GlobalDeleted, out int weakCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.GlobalReference (ReferenceLogEvent.GlobalDeleted, value, default, count, weakCount, gcBridgeReferenceOperation);
			}
			base.DeleteGlobalReference (ref value);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference value)
		{
			var reference = base.CreateWeakGlobalReference (value);
			if (!reference.IsValid) {
				return reference;
			}

			int count = UpdateReferenceCount (ReferenceLogEvent.WeakGlobalCreated, out int weakCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.GlobalReference (ReferenceLogEvent.WeakGlobalCreated, value, reference, count, weakCount, gcBridgeReferenceOperation);
			}
			return reference;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference value)
		{
			if (!value.IsValid) {
				return;
			}

			int count = UpdateReferenceCount (ReferenceLogEvent.WeakGlobalDeleted, out int weakCount);
			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.GlobalReference (ReferenceLogEvent.WeakGlobalDeleted, value, default, count, weakCount, gcBridgeReferenceOperation);
			}
			base.DeleteWeakGlobalReference (ref value);
		}

		internal int UpdateReferenceCount (ReferenceLogEvent kind, out int weakCount)
		{
			int globalCount;

			switch (kind) {
				case ReferenceLogEvent.GlobalCreated:
					globalCount = Interlocked.Increment (ref grefCount);
					weakCount = Volatile.Read (ref weakGrefCount);
					break;
				case ReferenceLogEvent.GlobalDeleted:
					globalCount = Interlocked.Decrement (ref grefCount);
					weakCount = Volatile.Read (ref weakGrefCount);
					break;
				case ReferenceLogEvent.WeakGlobalCreated:
					weakCount = Interlocked.Increment (ref weakGrefCount);
					globalCount = Volatile.Read (ref grefCount);
					break;
				case ReferenceLogEvent.WeakGlobalDeleted:
					weakCount = Interlocked.Decrement (ref weakGrefCount);
					globalCount = Volatile.Read (ref grefCount);
					break;
				default:
					throw new ArgumentOutOfRangeException (nameof (kind));
			}

			return globalCount;
		}
	}
}
#endif // JAVA_INTEROP
