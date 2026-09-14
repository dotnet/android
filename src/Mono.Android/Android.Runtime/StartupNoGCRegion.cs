using System;
using System.Threading;

namespace Android.Runtime;

sealed class StartupNoGCRegion
{
	const long Budget = 24 * 1024 * 1024;
	// Bound the process-wide region when an app never reports that startup is fully drawn.
	static readonly TimeSpan DefaultFallbackTimeout = TimeSpan.FromSeconds (10);
	static readonly StartupNoGCRegion instance = new (
		static (totalSize, disallowFullBlockingGC) => GC.TryStartNoGCRegion (totalSize, disallowFullBlockingGC),
		GC.EndNoGCRegion,
		GC.CollectionCount,
		DefaultFallbackTimeout
	);

	readonly object sync = new ();
	readonly Func<long, bool, bool> tryStartNoGCRegion;
	readonly Action endNoGCRegion;
	readonly Func<int, int> collectionCount;
	readonly TimeSpan fallbackTimeout;
	Timer? fallbackTimer;
	State state;
	int gen0CollectionCount;
	int gen1CollectionCount;
	int gen2CollectionCount;

	enum State
	{
		NotStarted,
		Active,
		Ended,
	}

	internal StartupNoGCRegion (
		Func<long, bool, bool> tryStartNoGCRegion,
		Action endNoGCRegion,
		Func<int, int> collectionCount,
		TimeSpan fallbackTimeout
	)
	{
		ArgumentNullException.ThrowIfNull (tryStartNoGCRegion);
		ArgumentNullException.ThrowIfNull (endNoGCRegion);
		ArgumentNullException.ThrowIfNull (collectionCount);

		this.tryStartNoGCRegion = tryStartNoGCRegion;
		this.endNoGCRegion = endNoGCRegion;
		this.collectionCount = collectionCount;
		this.fallbackTimeout = fallbackTimeout;
	}

	internal static void Start () => instance.Start (isCoreClrRuntime: true);

	internal static void End () => instance.Finish ();

	internal void Start (bool isCoreClrRuntime)
	{
		if (!isCoreClrRuntime) {
			return;
		}

		lock (sync) {
			if (state != State.NotStarted) {
				return;
			}

			bool started;
			try {
				started = tryStartNoGCRegion (Budget, true);
			} catch (InvalidOperationException) {
				state = State.Ended;
				return;
			}

			if (!started) {
				state = State.Ended;
				return;
			}

			gen0CollectionCount = collectionCount (0);
			gen1CollectionCount = collectionCount (1);
			gen2CollectionCount = collectionCount (2);
			fallbackTimer = new Timer (
				static value => {
					if (value is StartupNoGCRegion noGCRegion) {
						noGCRegion.Finish ();
					}
				},
				this,
				fallbackTimeout,
				Timeout.InfiniteTimeSpan
			);
			state = State.Active;
		}
	}

	internal void Finish ()
	{
		Timer? timer;
		bool collectionOccurred;
		lock (sync) {
			if (state != State.Active) {
				return;
			}

			state = State.Ended;
			timer = fallbackTimer;
			fallbackTimer = null;
			collectionOccurred =
				collectionCount (0) != gen0CollectionCount ||
				collectionCount (1) != gen1CollectionCount ||
				collectionCount (2) != gen2CollectionCount;
		}

		timer?.Dispose ();

		if (collectionOccurred) {
			return;
		}

		try {
			endNoGCRegion ();
		} catch (InvalidOperationException) {
			// The runtime already left the region because its budget was exhausted
			// or a collection was induced.
		}
	}
}
