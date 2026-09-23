using System;
using System.Threading;

namespace Android.Runtime;

sealed class StartupNoGCRegion
{
	const long Budget32Bit = 12 * 1024 * 1024;
	const long Budget64Bit = 24 * 1024 * 1024;
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
		lock (sync) {
			if (state != State.NotStarted) {
				return;
			}

			bool started;
			try {
				long budget = IntPtr.Size == 4 ? Budget32Bit : Budget64Bit;
				started = GC.TryStartNoGCRegion (budget, disallowFullBlockingGC: true);
			} catch (Exception) {
				// This startup optimization must never prevent the application from starting.
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

	void Finish ()
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
		} catch (Exception) {
			// Ending this startup optimization must never fail the application.
		}
	}
}
