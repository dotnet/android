#nullable enable

using System;
using System.Diagnostics.Tracing;

#if INSIDE_MONO_ANDROID_RUNTIME
namespace Microsoft.Android.Runtime
#else
namespace Java.Interop
#endif
{
	internal static class InteropCounterEventSource
	{
#if INSIDE_MONO_ANDROID_RUNTIME
		internal const string ProviderName = "Microsoft.Android.Runtime.InteropMetrics";
#else
		internal const string ProviderName = "Java.Interop.InteropMetrics";
#endif

		internal const string ManagedObjectsOnlyReachableFromJavaCounterName = "managed-objects-only-reachable-from-java";
		internal const string JavaObjectsOnlyReachableFromManagedCounterName = "java-objects-only-reachable-from-managed";
		internal const string BridgeObjectsAliveAfterProcessingCounterName = "bridge-objects-alive-after-processing";
		internal const string BridgeObjectsUnreachableAfterProcessingCounterName = "bridge-objects-unreachable-after-processing";

		static readonly InteropCounterEventSourceImplementation source = new ();

		internal static bool AreBridgeProcessingCountersEnabled ()
		{
			return source.BridgeProcessingCountersEnabled;
		}

		internal static void ReportBridgeProcessingMetrics (
				int managedObjectsOnlyReachableFromJavaCount,
				int javaObjectsOnlyReachableFromManagedCount,
				int bridgeObjectsAliveAfterProcessingCount,
				int bridgeObjectsUnreachableAfterProcessingCount)
		{
			source.ReportBridgeProcessingMetrics (
				managedObjectsOnlyReachableFromJavaCount,
				javaObjectsOnlyReachableFromManagedCount,
				bridgeObjectsAliveAfterProcessingCount,
				bridgeObjectsUnreachableAfterProcessingCount);
		}

		[EventSource (Name = ProviderName)]
		sealed class InteropCounterEventSourceImplementation : EventSource
		{
			const string EventCounterIntervalSecArgumentName = "EventCounterIntervalSec";

			EventCounter? managedObjectsOnlyReachableFromJavaCounter;
			EventCounter? javaObjectsOnlyReachableFromManagedCounter;
			EventCounter? bridgeObjectsAliveAfterProcessingCounter;
			EventCounter? bridgeObjectsUnreachableAfterProcessingCounter;
			readonly object countersLock = new ();

			volatile bool bridgeProcessingCountersEnabled;

			internal bool BridgeProcessingCountersEnabled => bridgeProcessingCountersEnabled;

			[NonEvent]
			protected override void OnEventCommand (EventCommandEventArgs command)
			{
				base.OnEventCommand (command);

				if (command.Command == EventCommand.Disable) {
					lock (countersLock) {
						bridgeProcessingCountersEnabled = false;
						DisposeBridgeProcessingCounters ();
					}
					return;
				}

				if (command.Command != EventCommand.Enable || command.Arguments == null || !command.Arguments.ContainsKey (EventCounterIntervalSecArgumentName)) {
					return;
				}

				lock (countersLock) {
					EnsureBridgeProcessingCountersInitialized ();
					bridgeProcessingCountersEnabled = true;
				}
			}

			[NonEvent]
			internal void ReportBridgeProcessingMetrics (
					int managedObjectsOnlyReachableFromJavaCount,
					int javaObjectsOnlyReachableFromManagedCount,
					int bridgeObjectsAliveAfterProcessingCount,
					int bridgeObjectsUnreachableAfterProcessingCount)
			{
				lock (countersLock) {
					if (!bridgeProcessingCountersEnabled) {
						return;
					}

					GetManagedObjectsOnlyReachableFromJavaCounter ().WriteMetric (managedObjectsOnlyReachableFromJavaCount);
					GetJavaObjectsOnlyReachableFromManagedCounter ().WriteMetric (javaObjectsOnlyReachableFromManagedCount);
					GetBridgeObjectsAliveAfterProcessingCounter ().WriteMetric (bridgeObjectsAliveAfterProcessingCount);
					GetBridgeObjectsUnreachableAfterProcessingCounter ().WriteMetric (bridgeObjectsUnreachableAfterProcessingCount);
				}
			}

			[NonEvent]
			void EnsureBridgeProcessingCountersInitialized ()
			{
				managedObjectsOnlyReachableFromJavaCounter ??= CreateCounter (
					ManagedObjectsOnlyReachableFromJavaCounterName,
					".NET objects only reachable from Java");
				javaObjectsOnlyReachableFromManagedCounter ??= CreateCounter (
					JavaObjectsOnlyReachableFromManagedCounterName,
					"Java objects only reachable from .NET");
				bridgeObjectsAliveAfterProcessingCounter ??= CreateCounter (
					BridgeObjectsAliveAfterProcessingCounterName,
					"Bridge objects still alive after bridge processing");
				bridgeObjectsUnreachableAfterProcessingCounter ??= CreateCounter (
					BridgeObjectsUnreachableAfterProcessingCounterName,
					"Bridge objects unreachable after bridge processing");
			}

			[NonEvent]
			EventCounter CreateCounter (string name, string displayName)
			{
				return new EventCounter (name, this) {
					DisplayName = displayName,
					DisplayUnits = "count",
				};
			}

			[NonEvent]
			EventCounter GetManagedObjectsOnlyReachableFromJavaCounter ()
			{
				if (managedObjectsOnlyReachableFromJavaCounter == null) {
					throw new InvalidOperationException ($"{ManagedObjectsOnlyReachableFromJavaCounterName} counter was not initialized.");
				}

				return managedObjectsOnlyReachableFromJavaCounter;
			}

			[NonEvent]
			EventCounter GetJavaObjectsOnlyReachableFromManagedCounter ()
			{
				if (javaObjectsOnlyReachableFromManagedCounter == null) {
					throw new InvalidOperationException ($"{JavaObjectsOnlyReachableFromManagedCounterName} counter was not initialized.");
				}

				return javaObjectsOnlyReachableFromManagedCounter;
			}

			[NonEvent]
			EventCounter GetBridgeObjectsAliveAfterProcessingCounter ()
			{
				if (bridgeObjectsAliveAfterProcessingCounter == null) {
					throw new InvalidOperationException ($"{BridgeObjectsAliveAfterProcessingCounterName} counter was not initialized.");
				}

				return bridgeObjectsAliveAfterProcessingCounter;
			}

			[NonEvent]
			EventCounter GetBridgeObjectsUnreachableAfterProcessingCounter ()
			{
				if (bridgeObjectsUnreachableAfterProcessingCounter == null) {
					throw new InvalidOperationException ($"{BridgeObjectsUnreachableAfterProcessingCounterName} counter was not initialized.");
				}

				return bridgeObjectsUnreachableAfterProcessingCounter;
			}

			[NonEvent]
			void DisposeBridgeProcessingCounters ()
			{
				managedObjectsOnlyReachableFromJavaCounter?.Dispose ();
				managedObjectsOnlyReachableFromJavaCounter = null;
				javaObjectsOnlyReachableFromManagedCounter?.Dispose ();
				javaObjectsOnlyReachableFromManagedCounter = null;
				bridgeObjectsAliveAfterProcessingCounter?.Dispose ();
				bridgeObjectsAliveAfterProcessingCounter = null;
				bridgeObjectsUnreachableAfterProcessingCounter?.Dispose ();
				bridgeObjectsUnreachableAfterProcessingCounter = null;
			}
		}
	}
}
