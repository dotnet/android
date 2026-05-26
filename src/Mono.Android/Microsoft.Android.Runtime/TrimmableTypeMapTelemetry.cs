#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Android.Runtime;

static class TrimmableTypeMapTelemetry
{
	const string InstrumentationName = "Microsoft.Android.Runtime.TrimmableTypeMap";
	const int MaxBufferedOperations = 256;

	static readonly ActivitySource s_activitySource = new (InstrumentationName);
	static readonly Meter s_meter = new (InstrumentationName);
	static readonly Histogram<double> s_operationDuration = s_meter.CreateHistogram<double> (
		"android.typemap.operation.duration",
		unit: "ms",
		description: "Duration of trimmable typemap startup and lookup operations.");
	static readonly Counter<long> s_operationCount = s_meter.CreateCounter<long> (
		"android.typemap.operation.count",
		unit: "{operation}",
		description: "Count of trimmable typemap startup and lookup operations.");
	static readonly Counter<long> s_entryCount = s_meter.CreateCounter<long> (
		"android.typemap.entries",
		unit: "{entry}",
		description: "Number of generated trimmable typemap entries loaded by kind.");
	static readonly object s_bufferedOperationsLock = new ();
	static readonly BufferedOperation[] s_bufferedOperations = new BufferedOperation [MaxBufferedOperations];
	static int s_bufferedOperationCount;
	static bool s_bufferedOperationsFlushed;

	public static bool IsEnabled =>
		s_activitySource.HasListeners () ||
		s_operationDuration.Enabled ||
		s_operationCount.Enabled ||
		s_entryCount.Enabled;

	public static OperationScope StartOperation (string operation)
	{
		var hasActivityListeners = s_activitySource.HasListeners ();
		if (!hasActivityListeners && !ShouldBufferOperations () && !s_operationDuration.Enabled && !s_operationCount.Enabled) {
			return default;
		}

		if (hasActivityListeners) {
			FlushBufferedEvents ();
		}

		var activity = hasActivityListeners ? s_activitySource.StartActivity (operation) : null;
		var startTimestamp = activity is not null || s_operationDuration.Enabled ? Stopwatch.GetTimestamp () : 0;
		return new OperationScope (operation, activity, startTimestamp, bufferOperation: !hasActivityListeners && ShouldBufferOperations ());
	}

	public static void RecordEntries (string kind, long count)
	{
		if (!s_entryCount.Enabled) {
			return;
		}

		var tags = new TagList {
			{ "kind", kind },
		};
		s_entryCount.Add (count, tags);
	}

	static bool ShouldBufferOperations ()
	{
		if (s_bufferedOperationsFlushed) {
			return false;
		}

		lock (s_bufferedOperationsLock) {
			return !s_bufferedOperationsFlushed && s_bufferedOperationCount < s_bufferedOperations.Length;
		}
	}

	static void BufferOperation (string operation, DateTimeOffset startTimestamp, DateTimeOffset endTimestamp)
	{
		lock (s_bufferedOperationsLock) {
			if (s_bufferedOperationsFlushed || s_bufferedOperationCount >= s_bufferedOperations.Length) {
				return;
			}

			s_bufferedOperations [s_bufferedOperationCount++] = new BufferedOperation (operation, startTimestamp, endTimestamp);
		}
	}

	static void FlushBufferedEvents ()
	{
		BufferedOperation[]? bufferedOperations = null;
		int bufferedOperationCount;

		lock (s_bufferedOperationsLock) {
			if (s_bufferedOperationsFlushed) {
				return;
			}

			bufferedOperationCount = s_bufferedOperationCount;
			if (bufferedOperationCount > 0) {
				bufferedOperations = new BufferedOperation [bufferedOperationCount];
				Array.Copy (s_bufferedOperations, bufferedOperations, bufferedOperationCount);
			}
			s_bufferedOperationsFlushed = true;
		}

		if (bufferedOperations is null) {
			return;
		}

		using (var activity = s_activitySource.StartActivity ("typemap.buffered_events")) {
			if (activity is not null) {
				activity.SetTag ("operation.count", bufferedOperationCount);
			}
		}

		foreach (var bufferedOperation in bufferedOperations) {
			var activity = s_activitySource.StartActivity (
				bufferedOperation.Name,
				ActivityKind.Internal,
				default (ActivityContext),
				tags: null,
				links: null,
				startTime: bufferedOperation.StartTimestamp);
			if (activity is null) {
				continue;
			}

			activity.SetTag ("buffered", true);
			activity.SetTag ("duration.us", (bufferedOperation.EndTimestamp - bufferedOperation.StartTimestamp).TotalMicroseconds);
			activity.AddEvent (new ActivityEvent ($"{bufferedOperation.Name}.start", bufferedOperation.StartTimestamp));
			activity.AddEvent (new ActivityEvent ($"{bufferedOperation.Name}.end", bufferedOperation.EndTimestamp));
			activity.SetEndTime (bufferedOperation.EndTimestamp.UtcDateTime);
			activity.Dispose ();

			if (s_operationDuration.Enabled) {
				var tags = new TagList {
					{ "operation", bufferedOperation.Name },
					{ "buffered", true },
				};
				s_operationDuration.Record ((bufferedOperation.EndTimestamp - bufferedOperation.StartTimestamp).TotalMilliseconds, tags);
			}
			if (s_operationCount.Enabled) {
				var tags = new TagList {
					{ "operation", bufferedOperation.Name },
					{ "buffered", true },
				};
				s_operationCount.Add (1, tags);
			}
		}
	}

	readonly struct BufferedOperation
	{
		public readonly string Name;
		public readonly DateTimeOffset StartTimestamp;
		public readonly DateTimeOffset EndTimestamp;

		public BufferedOperation (string name, DateTimeOffset startTimestamp, DateTimeOffset endTimestamp)
		{
			Name = name;
			StartTimestamp = startTimestamp;
			EndTimestamp = endTimestamp;
		}
	}

	public readonly struct OperationScope : IDisposable
	{
		readonly string? operation;
		readonly Activity? activity;
		readonly long startTimestamp;
		readonly bool bufferOperation;
		readonly DateTimeOffset startEventTimestamp;

		public bool IsActive => activity is not null;

		public OperationScope (string operation, Activity? activity, long startTimestamp, bool bufferOperation)
		{
			this.operation = operation;
			this.activity = activity;
			this.startTimestamp = startTimestamp;
			this.bufferOperation = bufferOperation;
			startEventTimestamp = DateTimeOffset.UtcNow;
			activity?.AddEvent (new ActivityEvent ($"{operation}.start"));
		}

		public void SetTag (string key, object? value)
		{
			activity?.SetTag (key, value);
		}

		public void Dispose ()
		{
			if (operation is not null) {
				activity?.AddEvent (new ActivityEvent ($"{operation}.end"));
				if (bufferOperation) {
					BufferOperation (operation, startEventTimestamp, DateTimeOffset.UtcNow);
				}

				if (startTimestamp != 0) {
					var elapsed = Stopwatch.GetElapsedTime (startTimestamp).TotalMilliseconds;
					activity?.SetTag ("duration.us", elapsed * 1000);
					if (s_operationDuration.Enabled) {
						var tags = new TagList {
							{ "operation", operation },
						};
						s_operationDuration.Record (elapsed, tags);
					}
				}

				if (s_operationCount.Enabled) {
					var tags = new TagList {
						{ "operation", operation },
					};
					s_operationCount.Add (1, tags);
				}
			}

			activity?.Dispose ();
		}
	}
}
