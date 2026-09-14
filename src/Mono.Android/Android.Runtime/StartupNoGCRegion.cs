using System;
using System.Threading;

namespace Android.Runtime;

sealed class StartupNoGCRegion
{
	const long Budget = 24 * 1024 * 1024;
	// Bound the process-wide region when an app never reports that startup is fully drawn.
	static readonly TimeSpan DefaultFallbackTimeout = TimeSpan.FromSeconds (10);
	static readonly StartupNoGCRegion instance = new ();

	readonly Lock sync = new ();
	Timer? fallbackTimer;
	State state;

	enum State
	{
		NotStarted,
		Active,
		Ended,
	}

	internal static void Start () => instance.StartRegion ();

	internal static void End () => instance.Finish ();

	void StartRegion ()
	{
		if (Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime) {
			return;
		}

		lock (sync) {
			if (state != State.NotStarted) {
				return;
			}

			bool started;
			try {
				started = GC.TryStartNoGCRegion (Budget, disallowFullBlockingGC: true);
			} catch (InvalidOperationException) {
				state = State.Ended;
				return;
			}

			if (!started) {
				state = State.Ended;
				return;
			}

			fallbackTimer = new Timer (
				static value => {
					if (value is StartupNoGCRegion noGCRegion) {
						noGCRegion.Finish ();
					}
				},
				this,
				DefaultFallbackTimeout,
				Timeout.InfiniteTimeSpan
			);
			state = State.Active;
		}
	}

	internal void Finish ()
	{
		Timer? timer;
		lock (sync) {
			if (state != State.Active) {
				return;
			}

			state = State.Ended;
			timer = fallbackTimer;
			fallbackTimer = null;
		}

		timer?.Dispose ();

		try {
			GC.EndNoGCRegion ();
		} catch (InvalidOperationException) {
			// The runtime already left the region because its budget was exhausted
			// or a collection was induced.
		}
	}
}
