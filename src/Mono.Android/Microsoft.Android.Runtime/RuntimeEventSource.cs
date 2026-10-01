#nullable enable

using System.Diagnostics.Tracing;

namespace Microsoft.Android.Runtime;

internal static class RuntimeEventSource
{
	internal const string ProviderName = "Microsoft.Android.Runtime";

	internal const int GCBridgeStartEventId = 7;
	internal const int GCBridgeStopEventId = 8;
	internal const int TypeMapLookupStartEventId = 9;
	internal const int TypeMapLookupStopEventId = 10;

	internal const EventKeywords GCBridgeKeyword = (EventKeywords) 0x8;
	internal const EventKeywords TypeMapKeyword = (EventKeywords) 0x4;
	internal const EventTask GCBridgeTask = (EventTask) 1;
	internal const EventTask TypeMapTask = (EventTask) 2;

	internal const string JavaToManagedTypeMapDirection = "JavaToManaged";
	internal const string ManagedToJavaTypeMapDirection = "ManagedToJava";

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

	static class RuntimeEventSourceHolder
	{
		internal static readonly RuntimeEventSourceImplementation Instance = new ();
	}

	[EventSource (Name = ProviderName)]
	sealed class RuntimeEventSourceImplementation : EventSource
	{
		internal void Initialize ()
		{
		}

		public static class Keywords
		{
			public const EventKeywords GCBridge = GCBridgeKeyword;
			public const EventKeywords TypeMap = TypeMapKeyword;
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
