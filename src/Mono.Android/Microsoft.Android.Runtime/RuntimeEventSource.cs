#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

internal static class RuntimeEventSource
{
	internal const string ProviderName = "Microsoft.Android.Runtime";

	internal const int ManagedPeerCreatedEventId = 1;
	internal const int JavaPeerCreatedEventId = 2;
	internal const int ManagedPeerReleasedJavaPeerEventId = 3;
	internal const int JavaPeerReleasedManagedPeerEventId = 4;
	internal const int ManagedPeerOnlyReachableFromJavaPeerEventId = 5;
	internal const int JavaPeerOnlyReachableFromManagedPeerEventId = 6;
	internal const int GCBridgeStartEventId = 7;
	internal const int GCBridgeStopEventId = 8;
	internal const int TypeMapLookupStartEventId = 9;
	internal const int TypeMapLookupStopEventId = 10;
	internal const int GlobalReferenceCreatedEventId = 11;
	internal const int GlobalReferenceDeletedEventId = 12;
	internal const int WeakGlobalReferenceCreatedEventId = 13;
	internal const int WeakGlobalReferenceDeletedEventId = 14;
	internal const int LocalReferenceCreatedEventId = 15;
	internal const int LocalReferenceDeletedEventId = 16;
	internal const int LocalReferenceReleasedEventId = 17;
	internal const int GlobalReferenceDiagnosticEventId = 18;
	internal const int LocalReferenceDiagnosticEventId = 19;
	internal const int WeakGlobalReferenceCollectedEventId = 20;
	internal const int ReferenceStackTraceEventId = 21;

	internal const EventKeywords PeerLifecycleKeyword = (EventKeywords) 0x1;
	internal const EventKeywords ReachabilityKeyword = (EventKeywords) 0x2;
	internal const EventKeywords GCBridgeKeyword = (EventKeywords) 0x8;
	internal const EventKeywords TypeMapKeyword = (EventKeywords) 0x4;
	internal const EventKeywords GlobalReferenceKeyword = (EventKeywords) 0x10;
	internal const EventKeywords LocalReferenceKeyword = (EventKeywords) 0x20;
	internal const EventKeywords ReferenceStackTraceKeyword = (EventKeywords) 0x40;
	internal const EventTask GCBridgeTask = (EventTask) 1;
	internal const EventTask TypeMapTask = (EventTask) 2;

	internal const string JavaToManagedTypeMapDirection = "JavaToManaged";
	internal const string ManagedToJavaTypeMapDirection = "ManagedToJava";

	const string UnknownValue = "Unknown";

	internal static void Initialize ()
	{
		RuntimeEventSourceHolder.Instance.Initialize ();
	}

	internal static bool GCBridgeStart ()
	{
		if (!RuntimeFeature.EventSourceSupport) {
			return false;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, GCBridgeKeyword)) {
			RuntimeEventSourceHolder.Instance.GCBridgeStart ();
			return true;
		}
		return false;
	}

	internal static void GCBridgeStop ()
	{
		RuntimeEventSourceHolder.Instance.GCBridgeStop ();
	}

	internal static bool TypeMapLookupStart (string direction)
	{
		if (!RuntimeFeature.EventSourceSupport) {
			return false;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, TypeMapKeyword)) {
			RuntimeEventSourceHolder.Instance.TypeMapLookupStart (direction);
			return true;
		}
		return false;
	}

	internal static void TypeMapLookupStop (string direction)
	{
		RuntimeEventSourceHolder.Instance.TypeMapLookupStop (direction);
	}

	internal static bool PeerLifecycleEventsEnabled =>
		RuntimeFeature.EventSourceSupport && RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, PeerLifecycleKeyword);

	internal static bool ReachabilityEventsEnabled =>
		RuntimeFeature.EventSourceSupport && RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, ReachabilityKeyword);

	internal static void ManagedPeerCreated (string? managedType, string? javaType, int jniIdentityHashCode, int managedObjectHashCode)
	{
		RuntimeEventSourceHolder.Instance.ManagedPeerCreated (managedType ?? UnknownValue, javaType ?? UnknownValue, jniIdentityHashCode, managedObjectHashCode, GetRuntimeFlavor ());
	}

	internal static void JavaPeerCreated (string? managedType, string? javaType, int jniIdentityHashCode, int managedObjectHashCode)
	{
		RuntimeEventSourceHolder.Instance.JavaPeerCreated (managedType ?? UnknownValue, javaType ?? UnknownValue, jniIdentityHashCode, managedObjectHashCode, GetRuntimeFlavor ());
	}

	internal static void ManagedPeerReleasedJavaPeer (string? managedType, string? javaType, int jniIdentityHashCode, int managedObjectHashCode)
	{
		RuntimeEventSourceHolder.Instance.ManagedPeerReleasedJavaPeer (managedType ?? UnknownValue, javaType ?? UnknownValue, jniIdentityHashCode, managedObjectHashCode, GetRuntimeFlavor ());
	}

	internal static void JavaPeerReleasedManagedPeer (string? managedType, string? javaType, int jniIdentityHashCode, int managedObjectHashCode)
	{
		RuntimeEventSourceHolder.Instance.JavaPeerReleasedManagedPeer (managedType ?? UnknownValue, javaType ?? UnknownValue, jniIdentityHashCode, managedObjectHashCode, GetRuntimeFlavor ());
	}

	internal static void ManagedPeerOnlyReachableFromJavaPeer (string? managedType, string? javaType, int jniIdentityHashCode, int managedObjectHashCode, int componentIndex, int contextIndex, long contextPointer)
	{
		RuntimeEventSourceHolder.Instance.ManagedPeerOnlyReachableFromJavaPeer (managedType ?? UnknownValue, javaType ?? UnknownValue, jniIdentityHashCode, managedObjectHashCode, GetRuntimeFlavor (), componentIndex, contextIndex, contextPointer);
	}

	internal static void JavaPeerOnlyReachableFromManagedPeer (string? managedType, string? javaType, int jniIdentityHashCode, int managedObjectHashCode, int componentIndex, int contextIndex, long contextPointer)
	{
		RuntimeEventSourceHolder.Instance.JavaPeerOnlyReachableFromManagedPeer (managedType ?? UnknownValue, javaType ?? UnknownValue, jniIdentityHashCode, managedObjectHashCode, GetRuntimeFlavor (), componentIndex, contextIndex, contextPointer);
	}

	static string GetRuntimeFlavor ()
	{
		if (RuntimeFeature.IsNativeAotRuntime) {
			return "NativeAOT";
		}
		if (RuntimeFeature.IsCoreClrRuntime) {
			return "CoreCLR";
		}
		return UnknownValue;
	}

	internal static bool GlobalReferenceEventsEnabled =>
		RuntimeFeature.EventSourceSupport && RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Verbose, GlobalReferenceKeyword);

	internal static bool LocalReferenceEventsEnabled =>
		RuntimeFeature.EventSourceSupport && RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Verbose, LocalReferenceKeyword);

	internal static void GlobalReference (
		ReferenceLogEvent kind,
		JniObjectReference source,
		JniObjectReference reference,
		int globalCount,
		int weakCount,
		GCBridgeReferenceOperation bridgeOperation)
	{
		if (!GlobalReferenceEventsEnabled) {
			return;
		}

		var provider = RuntimeEventSourceHolder.Instance;
		ulong sourceHandle = unchecked((ulong)(nuint)source.Handle);
		ulong handle = unchecked((ulong)(nuint)reference.Handle);
		int threadId = Environment.CurrentManagedThreadId;
		int eventId;
		switch (kind) {
			case ReferenceLogEvent.GlobalCreated:
				eventId = GlobalReferenceCreatedEventId;
				provider.GlobalReferenceCreated (sourceHandle, handle, (int)source.Type, (int)reference.Type, globalCount, weakCount, threadId, (int)bridgeOperation);
				break;
			case ReferenceLogEvent.GlobalDeleted:
				eventId = GlobalReferenceDeletedEventId;
				provider.GlobalReferenceDeleted (sourceHandle, handle, (int)source.Type, (int)reference.Type, globalCount, weakCount, threadId, (int)bridgeOperation);
				break;
			case ReferenceLogEvent.WeakGlobalCreated:
				eventId = WeakGlobalReferenceCreatedEventId;
				provider.WeakGlobalReferenceCreated (sourceHandle, handle, (int)source.Type, (int)reference.Type, globalCount, weakCount, threadId, (int)bridgeOperation);
				break;
			case ReferenceLogEvent.WeakGlobalDeleted:
				eventId = WeakGlobalReferenceDeletedEventId;
				provider.WeakGlobalReferenceDeleted (sourceHandle, handle, (int)source.Type, (int)reference.Type, globalCount, weakCount, threadId, (int)bridgeOperation);
				break;
			default:
				throw new ArgumentOutOfRangeException (nameof (kind));
		}
		WriteReferenceStackTrace (eventId, reference.IsValid ? handle : sourceHandle, threadId);
	}

	internal static void LocalReferenceCreated (IntPtr source, IntPtr handle, int count, GCBridgeReferenceOperation bridgeOperation)
	{
		if (LocalReferenceEventsEnabled) {
			int threadId = Environment.CurrentManagedThreadId;
			ulong referenceHandle = unchecked((ulong)(nuint)handle);
			RuntimeEventSourceHolder.Instance.LocalReferenceCreated (unchecked((ulong)(nuint)source), referenceHandle, count, threadId, (int)bridgeOperation);
			WriteReferenceStackTrace (LocalReferenceCreatedEventId, referenceHandle, threadId);
		}
	}

	internal static void LocalReferenceDeleted (IntPtr handle, int count, GCBridgeReferenceOperation bridgeOperation)
	{
		if (LocalReferenceEventsEnabled) {
			int threadId = Environment.CurrentManagedThreadId;
			ulong referenceHandle = unchecked((ulong)(nuint)handle);
			RuntimeEventSourceHolder.Instance.LocalReferenceDeleted (0, referenceHandle, count, threadId, (int)bridgeOperation);
			WriteReferenceStackTrace (LocalReferenceDeletedEventId, referenceHandle, threadId);
		}
	}

	internal static void LocalReferenceReleased (IntPtr handle, int count, GCBridgeReferenceOperation bridgeOperation)
	{
		if (LocalReferenceEventsEnabled) {
			int threadId = Environment.CurrentManagedThreadId;
			ulong referenceHandle = unchecked((ulong)(nuint)handle);
			RuntimeEventSourceHolder.Instance.LocalReferenceReleased (0, referenceHandle, count, threadId, (int)bridgeOperation);
			WriteReferenceStackTrace (LocalReferenceReleasedEventId, referenceHandle, threadId);
		}
	}

	internal static void GlobalReferenceDiagnostic (string message)
	{
		if (GlobalReferenceEventsEnabled) {
			RuntimeEventSourceHolder.Instance.GlobalReferenceDiagnostic (message);
		}
	}

	internal static void LocalReferenceDiagnostic (string message)
	{
		if (LocalReferenceEventsEnabled) {
			RuntimeEventSourceHolder.Instance.LocalReferenceDiagnostic (message);
		}
	}

	internal static void WeakGlobalReferenceCollected (IntPtr handle)
	{
		if (GlobalReferenceEventsEnabled) {
			ulong referenceHandle = unchecked((ulong)(nuint)handle);
			RuntimeEventSourceHolder.Instance.WeakGlobalReferenceCollected (referenceHandle);
			WriteReferenceStackTrace (WeakGlobalReferenceCollectedEventId, referenceHandle, Environment.CurrentManagedThreadId);
		}
	}

	static void WriteReferenceStackTrace (int referenceEventId, ulong handle, int managedThreadId)
	{
		if (!RuntimeFeature.IsNativeAotRuntime) {
			return;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Verbose, ReferenceStackTraceKeyword)) {
			RuntimeEventSourceHolder.Instance.ReferenceStackTrace (referenceEventId, handle, managedThreadId, GetReferenceStackTrace ());
		}
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	static string GetReferenceStackTrace ()
	{
		// Only skip this guaranteed frame; NativeAOT may inline the callers.
		return new StackTrace (skipFrames: 1, fNeedFileInfo: false).ToString ();
	}

	static class RuntimeEventSourceHolder
	{
		internal static readonly RuntimeEventSourceImplementation Instance = new ();
	}

	[EventSource (Name = ProviderName)]
	sealed class RuntimeEventSourceImplementation : EventSource
	{
		[NonEvent]
		internal void Initialize ()
		{
		}

		public static class Keywords
		{
			public const EventKeywords PeerLifecycle = PeerLifecycleKeyword;
			public const EventKeywords Reachability = ReachabilityKeyword;
			public const EventKeywords GCBridge = GCBridgeKeyword;
			public const EventKeywords TypeMap = TypeMapKeyword;
			public const EventKeywords GlobalReference = GlobalReferenceKeyword;
			public const EventKeywords LocalReference = LocalReferenceKeyword;
			public const EventKeywords ReferenceStackTrace = ReferenceStackTraceKeyword;
		}

		public static class Tasks
		{
			public const EventTask GCBridge = GCBridgeTask;
			public const EventTask TypeMap = TypeMapTask;
		}

		[Event (ManagedPeerCreatedEventId, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
		public void ManagedPeerCreated (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
		{
			WriteEvent (ManagedPeerCreatedEventId, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
		}

		[Event (JavaPeerCreatedEventId, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
		public void JavaPeerCreated (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
		{
			WriteEvent (JavaPeerCreatedEventId, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
		}

		[Event (ManagedPeerReleasedJavaPeerEventId, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
		public void ManagedPeerReleasedJavaPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
		{
			WriteEvent (ManagedPeerReleasedJavaPeerEventId, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
		}

		[Event (JavaPeerReleasedManagedPeerEventId, Level = EventLevel.Informational, Keywords = Keywords.PeerLifecycle)]
		public void JavaPeerReleasedManagedPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor)
		{
			WriteEvent (JavaPeerReleasedManagedPeerEventId, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor);
		}

		[Event (ManagedPeerOnlyReachableFromJavaPeerEventId, Level = EventLevel.Informational, Keywords = Keywords.Reachability)]
		public void ManagedPeerOnlyReachableFromJavaPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor, int componentIndex, int contextIndex, long contextPointer)
		{
			WriteEvent (ManagedPeerOnlyReachableFromJavaPeerEventId, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor, componentIndex, contextIndex, contextPointer);
		}

		[Event (JavaPeerOnlyReachableFromManagedPeerEventId, Level = EventLevel.Informational, Keywords = Keywords.Reachability)]
		public void JavaPeerOnlyReachableFromManagedPeer (string managedType, string javaType, int jniIdentityHashCode, int managedObjectHashCode, string runtimeFlavor, int componentIndex, int contextIndex, long contextPointer)
		{
			WriteEvent (JavaPeerOnlyReachableFromManagedPeerEventId, managedType, javaType, jniIdentityHashCode, managedObjectHashCode, runtimeFlavor, componentIndex, contextIndex, contextPointer);
		}

		[Event (GCBridgeStartEventId, Level = EventLevel.Informational, Keywords = Keywords.GCBridge, Task = Tasks.GCBridge, Opcode = EventOpcode.Start)]
		public void GCBridgeStart ()
		{
			WriteEvent (GCBridgeStartEventId);
		}

		[Event (GCBridgeStopEventId, Level = EventLevel.Informational, Keywords = Keywords.GCBridge, Task = Tasks.GCBridge, Opcode = EventOpcode.Stop)]
		public void GCBridgeStop ()
		{
			WriteEvent (GCBridgeStopEventId);
		}

		[Event (TypeMapLookupStartEventId, Level = EventLevel.Informational, Keywords = Keywords.TypeMap, Task = Tasks.TypeMap, Opcode = EventOpcode.Start)]
		public void TypeMapLookupStart (string direction)
		{
			WriteEvent (TypeMapLookupStartEventId, direction);
		}

		[Event (TypeMapLookupStopEventId, Level = EventLevel.Informational, Keywords = Keywords.TypeMap, Task = Tasks.TypeMap, Opcode = EventOpcode.Stop)]
		public void TypeMapLookupStop (string direction)
		{
			WriteEvent (TypeMapLookupStopEventId, direction);
		}

		[Event (GlobalReferenceCreatedEventId, Level = EventLevel.Verbose, Keywords = Keywords.GlobalReference)]
		public void GlobalReferenceCreated (ulong sourceHandle, ulong handle, int sourceType, int referenceType, int globalCount, int weakCount, int managedThreadId, int bridgeOperation)
		{
			WriteGlobalReference (GlobalReferenceCreatedEventId, sourceHandle, handle, sourceType, referenceType, globalCount, weakCount, managedThreadId, bridgeOperation);
		}

		[Event (GlobalReferenceDeletedEventId, Level = EventLevel.Verbose, Keywords = Keywords.GlobalReference)]
		public void GlobalReferenceDeleted (ulong sourceHandle, ulong handle, int sourceType, int referenceType, int globalCount, int weakCount, int managedThreadId, int bridgeOperation)
		{
			WriteGlobalReference (GlobalReferenceDeletedEventId, sourceHandle, handle, sourceType, referenceType, globalCount, weakCount, managedThreadId, bridgeOperation);
		}

		[Event (WeakGlobalReferenceCreatedEventId, Level = EventLevel.Verbose, Keywords = Keywords.GlobalReference)]
		public void WeakGlobalReferenceCreated (ulong sourceHandle, ulong handle, int sourceType, int referenceType, int globalCount, int weakCount, int managedThreadId, int bridgeOperation)
		{
			WriteGlobalReference (WeakGlobalReferenceCreatedEventId, sourceHandle, handle, sourceType, referenceType, globalCount, weakCount, managedThreadId, bridgeOperation);
		}

		[Event (WeakGlobalReferenceDeletedEventId, Level = EventLevel.Verbose, Keywords = Keywords.GlobalReference)]
		public void WeakGlobalReferenceDeleted (ulong sourceHandle, ulong handle, int sourceType, int referenceType, int globalCount, int weakCount, int managedThreadId, int bridgeOperation)
		{
			WriteGlobalReference (WeakGlobalReferenceDeletedEventId, sourceHandle, handle, sourceType, referenceType, globalCount, weakCount, managedThreadId, bridgeOperation);
		}

		[NonEvent]
		void WriteGlobalReference (int eventId, ulong sourceHandle, ulong handle, int sourceType, int referenceType, int globalCount, int weakCount, int managedThreadId, int bridgeOperation)
		{
			WriteEvent (eventId, sourceHandle, handle, sourceType, referenceType, globalCount, weakCount, managedThreadId, bridgeOperation);
		}

		[Event (LocalReferenceCreatedEventId, Level = EventLevel.Verbose, Keywords = Keywords.LocalReference)]
		public void LocalReferenceCreated (ulong sourceHandle, ulong handle, int localCount, int managedThreadId, int bridgeOperation)
		{
			WriteLocalReference (LocalReferenceCreatedEventId, sourceHandle, handle, localCount, managedThreadId, bridgeOperation);
		}

		[Event (LocalReferenceDeletedEventId, Level = EventLevel.Verbose, Keywords = Keywords.LocalReference)]
		public void LocalReferenceDeleted (ulong sourceHandle, ulong handle, int localCount, int managedThreadId, int bridgeOperation)
		{
			WriteLocalReference (LocalReferenceDeletedEventId, sourceHandle, handle, localCount, managedThreadId, bridgeOperation);
		}

		[Event (LocalReferenceReleasedEventId, Level = EventLevel.Verbose, Keywords = Keywords.LocalReference)]
		public void LocalReferenceReleased (ulong sourceHandle, ulong handle, int localCount, int managedThreadId, int bridgeOperation)
		{
			WriteLocalReference (LocalReferenceReleasedEventId, sourceHandle, handle, localCount, managedThreadId, bridgeOperation);
		}

		[NonEvent]
		void WriteLocalReference (int eventId, ulong sourceHandle, ulong handle, int localCount, int managedThreadId, int bridgeOperation)
		{
			WriteEvent (eventId, sourceHandle, handle, localCount, managedThreadId, bridgeOperation);
		}

		[Event (GlobalReferenceDiagnosticEventId, Level = EventLevel.Verbose, Keywords = Keywords.GlobalReference)]
		public void GlobalReferenceDiagnostic (string message)
		{
			WriteEvent (GlobalReferenceDiagnosticEventId, message);
		}

		[Event (LocalReferenceDiagnosticEventId, Level = EventLevel.Verbose, Keywords = Keywords.LocalReference)]
		public void LocalReferenceDiagnostic (string message)
		{
			WriteEvent (LocalReferenceDiagnosticEventId, message);
		}

		[Event (WeakGlobalReferenceCollectedEventId, Level = EventLevel.Verbose, Keywords = Keywords.GlobalReference)]
		public void WeakGlobalReferenceCollected (ulong handle)
		{
			WriteEvent (WeakGlobalReferenceCollectedEventId, handle);
		}

		[Event (ReferenceStackTraceEventId, Level = EventLevel.Verbose, Keywords = Keywords.ReferenceStackTrace)]
		public void ReferenceStackTrace (int referenceEventId, ulong handle, int managedThreadId, string stackTrace)
		{
			WriteEvent (ReferenceStackTraceEventId, referenceEventId, handle, managedThreadId, stackTrace);
		}
	}
}
