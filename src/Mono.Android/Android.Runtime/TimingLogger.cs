using System;

namespace Android.Runtime
{
	/// <summary>
	///   A class which uses the native .NET for Android runtime to accurately measure (to the nanosecond level) time
	///   spent executing a portion of code bracketed with calls to <see cref="Start"/> (or the constructor, by
	///   default) and <see cref="Stop"/>.
	///   Timing messages are logged with the <c>Info</c> priority and the <c>monodroid-timing</c> tag in the
	///   device's logcat buffer.
	/// </summary>
	public class TimingLogger : IDisposable
	{
		bool disposed = false;
		IntPtr sequence;
		string? initStartMessage;

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
				sequence = IntPtr.Zero;
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
			if (sequence != IntPtr.Zero)
				return;

			sequence = RuntimeNativeMethods.monodroid_timing_start (startMessage ?? initStartMessage);
		}

		/// <summary>
		///   Stop measuring time and log message specified in the <paramref name="stopMessage"/> parameter. If
		///   message is not specified, the .NET for Android runtime will use the default message, <c>"Managed
		///   Timing"</c>. Time is reported in the following format:
		///
		/// <para>
		///   <c>stopMessage; elapsed: %lis:%lu::%lu</c>
		/// </para>
		/// <para>
		///   The <c>elapsed</c> fields are defined as follows: <c>seconds:milliseconds::nanoseconds</c>
		/// </para>
		/// </summary>
		public void Stop (string stopMessage)
		{
			if (sequence == IntPtr.Zero)
				return;

			RuntimeNativeMethods.monodroid_timing_stop (sequence, stopMessage);
			sequence = IntPtr.Zero;
		}

		/// <summary>
		///   Dispose of the current instance. <see cref="Dispose"/> for more information.
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
				if (sequence != IntPtr.Zero) {
					RuntimeNativeMethods.monodroid_timing_stop (sequence, null);
					sequence = IntPtr.Zero;
				}

				disposed = true;

			}
		}
	}
}
