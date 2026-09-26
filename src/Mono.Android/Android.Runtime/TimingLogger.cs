using System;
using System.Diagnostics;

namespace Android.Runtime
{
	/// <summary>
	///   A class which uses a monotonic managed timer to measure time spent executing a portion of code bracketed
	///   with calls to <see cref="Start"/> (or the constructor, by default) and <see cref="Stop"/>.
	///   Timing messages are logged with the <c>Info</c> priority and the <c>monodroid-timing</c> tag in the
	///   device's logcat buffer when the <c>timing</c> managed log category is enabled.
	/// </summary>
	/// <remarks>
	///   This API is obsolete. Use <see cref="Stopwatch"/> for local duration measurement or
	///   <see cref="System.Diagnostics.Tracing.EventSource"/> for trace integration.
	/// </remarks>
	[Obsolete ("Android.Runtime.TimingLogger is obsolete. Use System.Diagnostics.Stopwatch for local duration measurement or System.Diagnostics.Tracing.EventSource for trace integration.")]
	public class TimingLogger : IDisposable
	{
		const string DefaultMessage = "Managed Timing";
		const string LogTag = "monodroid-timing";

		bool disposed = false;
		bool active;
		long startTimestamp;
		string? initStartMessage;

		internal bool IsActive => active;
		internal long StartTimestamp => startTimestamp;

		/// <summary>
		///   Construct a TimeLogger instance and start measuring time immediately, if the <paramref
		///   name="startImmediately"/> parameter is left out or set to <c>true</c>. If the <paramref
		///   name="startMessage"/> is not <c>null</c> then the message is logged at the start.
		/// </summary>
		public TimingLogger (string? startMessage = null, bool startImmediately = true)
		{
			if (startImmediately)
				Start (startMessage);
			else {
				initStartMessage = startMessage;
			}
		}

		~TimingLogger ()
		{
			Dispose (false);
		}

		/// <summary>
		///   Start measuring time. If <paramref name="startMessage"/> is provided (or if the constructor was
		///   passed a message to use when starting) it will be output to the log, otherwise the measurement
		///   start is silent. The method does anything only if no measurement is active.
		/// </summary>
		public void Start (string? startMessage = null)
		{
			if (active || disposed || !Logger.LogTiming)
				return;

			string? message = startMessage ?? initStartMessage;
			if (message != null)
				Logger.Log (LogLevel.Info, LogTag, message);

			startTimestamp = Stopwatch.GetTimestamp ();
			active = true;
		}

		/// <summary>
		///   Stop measuring time and log message specified in the <paramref name="stopMessage"/> parameter. If
		///   message is not specified, the .NET for Android runtime will use the default message, <c>"Managed
		///   Timing"</c>. Time is reported in the following format:
		///
		/// <para>
		///   <c>stopMessage; elapsed: seconds:milliseconds::nanoseconds</c>
		/// </para>
		/// <para>
		///   The seconds and milliseconds fields are totals for the entire elapsed duration. The nanoseconds field
		///   is the remainder within the final millisecond.
		/// </para>
		/// </summary>
		public void Stop (string stopMessage)
		{
			StopCore (stopMessage);
		}

		void StopCore (string? stopMessage)
		{
			if (!active)
				return;

			TimeSpan elapsed = Stopwatch.GetElapsedTime (startTimestamp);
			active = false;
			startTimestamp = 0;
			Logger.Log (LogLevel.Info, LogTag, FormatMessage (stopMessage, elapsed));
		}

		internal static string FormatMessage (string? message, TimeSpan elapsed)
		{
			long elapsedTicks = elapsed.Ticks;
			long seconds = elapsedTicks / TimeSpan.TicksPerSecond;
			long milliseconds = elapsedTicks / TimeSpan.TicksPerMillisecond;
			long nanoseconds = (elapsedTicks % TimeSpan.TicksPerMillisecond) * 100;

			return FormattableString.Invariant ($"{message ?? DefaultMessage}; elapsed: {seconds}:{milliseconds}::{nanoseconds}");
		}

		/// <summary>
		///   Dispose of the current instance. <see cref="Dispose()"/> for more information.
		/// </summary>
		public void Dispose()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		/// <summary>
		///   Dispose of the current instance, stopping timing if necessary. Note that if timing is stopped
		///   here, the log will contain the default message (<see cref="Stop"/>)
		/// </summary>
		protected virtual void Dispose (bool disposing)
		{
			if (!disposed) {
				StopCore (null);
				disposed = true;
			}
		}
	}
}
