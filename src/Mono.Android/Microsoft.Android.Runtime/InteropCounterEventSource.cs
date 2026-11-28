#nullable enable

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Android.Runtime
{
	internal static class InteropCounterEventSource
	{
		internal const string ProviderName = "Microsoft.Android.Runtime.InteropMetrics";

		internal const string ManagedObjectsOnlyReachableFromJavaCounterName = "managed-objects-only-reachable-from-java";
		internal const string JavaObjectsOnlyReachableFromManagedCounterName = "java-objects-only-reachable-from-managed";

		static readonly InteropCounterEventSourceImplementation source = new ();

		internal static bool AreBridgeProcessingCountersEnabled ()
		{
			return RuntimeFeature.EventSourceSupport && source.BridgeProcessingCountersEnabled;
		}

		internal static void ReportBridgeProcessingMetrics (
				int managedObjectsOnlyReachableFromJavaCount,
				int javaObjectsOnlyReachableFromManagedCount)
		{
			source.ReportBridgeProcessingMetrics (
				managedObjectsOnlyReachableFromJavaCount,
				javaObjectsOnlyReachableFromManagedCount);
		}

		[EventSource (Name = ProviderName)]
		sealed class InteropCounterEventSourceImplementation : EventSource
		{
			const string EventCounterIntervalSecArgumentName = "EventCounterIntervalSec";

			readonly object countersLock = new ();
			volatile Counters? counters;

			// Counters are created once and never disposed: EventCommand.Disable is raised per
			// listener/session, so tearing them down would break other sessions still listening.
			internal bool BridgeProcessingCountersEnabled => counters != null && IsEnabled ();

			[NonEvent]
			protected override void OnEventCommand (EventCommandEventArgs command)
			{
				base.OnEventCommand (command);

				if (command.Command != EventCommand.Enable || command.Arguments == null || !command.Arguments.ContainsKey (EventCounterIntervalSecArgumentName)) {
					return;
				}

				lock (countersLock) {
					counters ??= new Counters (
						CreateCounter (ManagedObjectsOnlyReachableFromJavaCounterName, ".NET objects only reachable from Java"),
						CreateCounter (JavaObjectsOnlyReachableFromManagedCounterName, "Java objects only reachable from .NET"));
				}
			}

			[NonEvent]
			internal void ReportBridgeProcessingMetrics (
					int managedObjectsOnlyReachableFromJavaCount,
					int javaObjectsOnlyReachableFromManagedCount)
			{
				var current = counters;
				if (current == null || !IsEnabled ()) {
					return;
				}

				current.ManagedObjectsOnlyReachableFromJava.WriteMetric (managedObjectsOnlyReachableFromJavaCount);
				current.JavaObjectsOnlyReachableFromManaged.WriteMetric (javaObjectsOnlyReachableFromManagedCount);
			}

			[NonEvent]
			EventCounter CreateCounter (string name, string displayName)
			{
				return new EventCounter (name, this) {
					DisplayName = displayName,
					DisplayUnits = "count",
				};
			}

			sealed record Counters (EventCounter ManagedObjectsOnlyReachableFromJava, EventCounter JavaObjectsOnlyReachableFromManaged);
		}
	}
}
