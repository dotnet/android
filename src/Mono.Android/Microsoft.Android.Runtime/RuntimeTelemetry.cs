using System;
using System.Diagnostics;

namespace Microsoft.Android.Runtime;

static class RuntimeTelemetry
{
	internal const string ActivitySourceName = "Microsoft.Android.Runtime";
	internal const string StartupInitialization = "android.runtime.initialize";
	internal const string StartupTypeMap = "android.runtime.type_map.initialize";
	internal const string StartupJniRuntime = "android.runtime.jni.create";
	internal const string StartupGCBridge = "android.runtime.gc_bridge.initialize";
	internal const string StartupNativeMethods = "android.runtime.jni.register_natives";
	internal const string StartupHooks = "android.runtime.startup_hooks";
	internal const string GCBridgeProcess = "android.runtime.gc_bridge.process";

	// Fine-grained, potentially frequent operations use a separate source so consumers opt in to them independently.
	internal const string InteropActivitySourceName = "Microsoft.Android.Runtime.Interop";
	internal const string InteropRegisterNatives = "android.runtime.interop.register_natives";
	internal const string InteropCreatePeer = "android.runtime.interop.create_peer";
	internal const string TypeMapJavaToManaged = "android.runtime.type_map.java_to_managed";
	internal const string TypeMapManagedToJava = "android.runtime.type_map.managed_to_java";

	internal const string JavaClassTag = "android.java.class";
	internal const string ManagedTypeTag = "android.managed.type";
	internal const string FoundTag = "android.type_map.found";
	internal const string TargetTypeTag = "android.interop.target_type";

	internal static ActivitySource Source => ActivitySourceHolder.Source;

	internal static ActivitySource InteropSource => InteropActivitySourceHolder.Source;

	static class ActivitySourceHolder
	{
		internal static readonly ActivitySource Source = new (ActivitySourceName);
	}

	static class InteropActivitySourceHolder
	{
		internal static readonly ActivitySource Source = new (InteropActivitySourceName);
	}

	// Emitted after the GC bridge completes, so listeners never run while bridge processing is in progress.
	internal static void RecordGCBridgeProcessing (long startTimestamp, long components, long crossReferences, int collected, int generation)
	{
		var startTime = DateTimeOffset.UtcNow - Stopwatch.GetElapsedTime (startTimestamp);
		using var activity = Source.StartActivity (GCBridgeProcess, ActivityKind.Internal, default (ActivityContext), startTime: startTime);
		if (activity == null)
			return;
		activity.SetTag ("android.gc_bridge.components", components);
		activity.SetTag ("android.gc_bridge.cross_references", crossReferences);
		activity.SetTag ("android.gc_bridge.collected", collected);
		activity.SetTag ("android.gc_bridge.generation", generation);
	}

	internal static StartupOperation StartStartupOperation (string name) =>
		RuntimeFeature.StartupTracing ? StartupBuffer.Start (name) : default;

	internal readonly struct StartupOperation : IDisposable
	{
		readonly string? name;
		readonly long startTimestamp;

		internal StartupOperation (string name, long startTimestamp)
		{
			this.name = name;
			this.startTimestamp = startTimestamp;
		}

		public void Dispose ()
		{
			if (name != null)
				StartupBuffer.Record (name, startTimestamp, Stopwatch.GetTimestamp ());
		}
	}

	readonly struct StartupRecord
	{
		internal readonly string Name;
		internal readonly long Start;
		internal readonly long End;

		internal StartupRecord (string name, long start, long end)
		{
			Name = name;
			Start = start;
			End = end;
		}
	}

	static class StartupBuffer
	{
		const int Capacity = 16;
		static readonly object gate = new ();
		static readonly long originTimestamp = Stopwatch.GetTimestamp ();
		static readonly DateTimeOffset originTime = DateTimeOffset.UtcNow;
		static readonly StartupRecord [] records = new StartupRecord [Capacity];
		static StartupRecord root;
		static int count;
		static int dropped;
		static bool complete;
		static bool replaying;
		static bool replayed;

		// Allocate the buffer before the optional no-GC startup region begins.
		static StartupBuffer ()
		{
		}

		internal static StartupOperation Start (string name) => new (name, Stopwatch.GetTimestamp ());

		internal static void Record (string name, long start, long end)
		{
			lock (gate) {
				var record = new StartupRecord (name, start, end);
				if (name == StartupInitialization) {
					root = record;
					complete = true;
				} else if (count < records.Length) {
					records [count++] = record;
				} else {
					dropped++;
				}
			}
		}

		internal static bool TryBeginReplay (out StartupRecord startup, out StartupRecord [] operations, out int droppedCount)
		{
			lock (gate) {
				if (!complete || replaying || replayed) {
					startup = default;
					operations = [];
					droppedCount = 0;
					return false;
				}

				startup = root;
				operations = new StartupRecord [count];
				Array.Copy (records, operations, count);
				droppedCount = dropped;
				replaying = true;
				return true;
			}
		}

		internal static void EndReplay (bool emitted)
		{
			lock (gate) {
				replaying = false;
				if (emitted) {
					replayed = true;
					Array.Clear (records, 0, count);
					count = 0;
					root = default;
				}
			}
		}

		internal static DateTimeOffset GetTime (long timestamp) =>
			originTime + Stopwatch.GetElapsedTime (originTimestamp, timestamp);
	}

	internal static int ReplayStartupActivities ()
	{
		if (!RuntimeFeature.StartupTracing || !Source.HasListeners ())
			return 0;
		if (!StartupBuffer.TryBeginReplay (out var startup, out var operations, out var dropped))
			return 0;

		bool emitted = false;
		var previous = Activity.Current;
		try {
			Activity.Current = null;
			using var root = Source.StartActivity (startup.Name, ActivityKind.Internal, default (ActivityContext), startTime: StartupBuffer.GetTime (startup.Start));
			if (root == null)
				return 0;

			emitted = true;
			root.SetTag ("android.runtime.startup.buffered", true);
			root.SetTag ("android.runtime.startup.dropped", dropped);
			int emittedCount = 1;
			foreach (var operation in operations) {
				using var activity = Source.StartActivity (operation.Name, ActivityKind.Internal, root.Context, startTime: StartupBuffer.GetTime (operation.Start));
				if (activity == null)
					continue;
				activity.SetTag ("android.runtime.startup.buffered", true);
				activity.SetEndTime (StartupBuffer.GetTime (operation.End).UtcDateTime);
				emittedCount++;
			}
			root.SetEndTime (StartupBuffer.GetTime (startup.End).UtcDateTime);
			return emittedCount;
		} finally {
			Activity.Current = previous;
			StartupBuffer.EndReplay (emitted);
		}
	}
}
