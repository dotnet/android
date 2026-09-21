#nullable enable

using System.Diagnostics.Tracing;

namespace Microsoft.Android.Runtime;

internal static class RuntimeEventSource
{
	internal const string ProviderName = "Microsoft.Android.Runtime";

	internal const int GCBridgeStartEventId = 7;
	internal const int GCBridgeStopEventId = 8;

	internal const EventKeywords GCBridgeKeyword = (EventKeywords) 0x8;
	internal const EventTask GCBridgeTask = (EventTask) 1;

	internal static void Initialize ()
	{
		_ = RuntimeEventSourceHolder.Instance;
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

	static class RuntimeEventSourceHolder
	{
		internal static readonly RuntimeEventSourceImplementation Instance = new ();
	}

	[EventSource (Name = ProviderName)]
	sealed class RuntimeEventSourceImplementation : EventSource
	{
		public static class Keywords
		{
			public const EventKeywords GCBridge = GCBridgeKeyword;
		}

		public static class Tasks
		{
			public const EventTask GCBridge = GCBridgeTask;
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
	}
}
