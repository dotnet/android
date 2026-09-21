#nullable enable

using System.Diagnostics.Tracing;

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

	internal const EventKeywords PeerLifecycleKeyword = (EventKeywords) 0x1;
	internal const EventKeywords ReachabilityKeyword = (EventKeywords) 0x2;
	internal const EventKeywords TypeMapKeyword = (EventKeywords) 0x4;
	internal const EventKeywords GCBridgeKeyword = (EventKeywords) 0x8;

	internal const EventTask GCBridgeTask = (EventTask) 1;
	internal const EventTask TypeMapTask = (EventTask) 2;

	internal const string JavaToManagedTypeMapDirection = "JavaToManaged";
	internal const string ManagedToJavaTypeMapDirection = "ManagedToJava";

	internal static bool IsEnabled (EventKeywords keywords)
	{
		return RuntimeFeature.InteropEventSource &&
			RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, keywords);
	}

	internal static void GCBridgeStart ()
	{
		if (!RuntimeFeature.InteropEventSource) {
			return;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, GCBridgeKeyword)) {
			RuntimeEventSourceHolder.Instance.GCBridgeStart ();
		}
	}

	internal static void GCBridgeStop ()
	{
		if (!RuntimeFeature.InteropEventSource) {
			return;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, GCBridgeKeyword)) {
			RuntimeEventSourceHolder.Instance.GCBridgeStop ();
		}
	}

	internal static void TypeMapLookupStart (string direction)
	{
		if (!RuntimeFeature.InteropEventSource) {
			return;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, TypeMapKeyword)) {
			RuntimeEventSourceHolder.Instance.TypeMapLookupStart (direction);
		}
	}

	internal static void TypeMapLookupStop (string direction)
	{
		if (!RuntimeFeature.InteropEventSource) {
			return;
		}
		if (RuntimeEventSourceHolder.Instance.IsEnabled (EventLevel.Informational, TypeMapKeyword)) {
			RuntimeEventSourceHolder.Instance.TypeMapLookupStop (direction);
		}
	}

	static class RuntimeEventSourceHolder
	{
		internal static readonly RuntimeEventSourceImplementation Instance = new ();
	}

	[EventSource (Name = ProviderName)]
	sealed class RuntimeEventSourceImplementation : EventSource
	{
		public static class Keywords
		{
			public const EventKeywords PeerLifecycle = PeerLifecycleKeyword;
			public const EventKeywords Reachability = ReachabilityKeyword;
			public const EventKeywords TypeMap = TypeMapKeyword;
			public const EventKeywords GCBridge = GCBridgeKeyword;
		}

		public static class Tasks
		{
			public const EventTask GCBridge = GCBridgeTask;
			public const EventTask TypeMap = TypeMapTask;
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
	}
}
