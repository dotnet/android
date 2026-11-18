#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools.Debugging
{
	// Optional diagnostics for the Java debugger wait, enabled only by
	// ExecutionConfiguration.CaptureJavaDebuggingLogcat. It reads the guest clock before launch and takes one
	// logcat snapshot for the JDWP process after the forward is removed. It logs only fixed marker kinds,
	// guest times relative to the window start, counts and host UTC bounds.
	sealed class JavaDebuggingLogcatCapture
	{
		internal const int ClockMaxBytes = 64;
		internal const int ClockMaxLines = 2;
		internal static readonly TimeSpan ClockTimeout = TimeSpan.FromSeconds (1);
		internal const int SnapshotMaxBytes = 64 * 1024;
		internal const int SnapshotMaxLines = 256;
		internal const int MaxMarkers = 32;
		internal static readonly TimeSpan SnapshotTimeout = TimeSpan.FromSeconds (3);

		const string Limits = "window-may-start-up-to-1000ms-early,pid-reuse-possible,ring-rotation-possible,missing-marker-not-absence,image-grammar-unattested";

		readonly AndroidDevice device;
		readonly Action<string> logWriter;
		readonly long windowStartSeconds;

		JavaDebuggingLogcatCapture (AndroidDevice device, Action<string> logWriter, long windowStartSeconds)
		{
			this.device = device;
			this.logWriter = logWriter;
			this.windowStartSeconds = windowStartSeconds;
		}

		/// <summary>
		/// Returns null without any device query unless capture is enabled, Java debugging is active and a log
		/// writer exists. Also returns null when the guest clock cannot be read; only cancellation throws.
		/// </summary>
		internal static async Task<JavaDebuggingLogcatCapture?> StartAsync (AndroidDevice device, ExecutionConfiguration configuration, bool javaDebugging, CancellationToken token)
		{
			var logWriter = configuration.LogWiter;
			if (!configuration.CaptureJavaDebuggingLogcat || !javaDebugging || logWriter == null)
				return null;

			try {
				string? clockLine = null;
				int clockLines = 0;
				var hostBegin = DateTimeOffset.UtcNow;
				var stopwatch = Stopwatch.StartNew ();
				var result = await device.RunBoundedShellCommandAsync ("date +%s", ClockMaxBytes, ClockMaxLines, ClockTimeout, line => {
					if (clockLines++ == 0)
						clockLine = line;
				}, token).ConfigureAwait (false);
				var hostEnd = DateTimeOffset.UtcNow;

				long seconds = 0;
				string outcome;
				if (result.Status != BoundedShellCommandStatus.Completed)
					outcome = FormatStatus (result.Status);
				else if (clockLines != 1 || !TryParseEpochSeconds (clockLine, out seconds))
					outcome = "malformed";
				else
					outcome = "completed";

				logWriter ($"JDWP logcat-window result={outcome} host-utc-begin={FormatUtc (hostBegin)} host-utc-end={FormatUtc (hostEnd)} duration-ms={stopwatch.ElapsedMilliseconds} resolution=whole-second");
				return outcome == "completed" ? new JavaDebuggingLogcatCapture (device, logWriter, seconds) : null;
			} catch (OperationCanceledException) when (token.IsCancellationRequested) {
				throw;
			} catch (Exception ex) {
				LogFailure (logWriter, "clock", ex);
				return null;
			}
		}

		internal static void LogFailure (Action<string>? logWriter, string phase, Exception exception)
		{
			try {
				logWriter?.Invoke ($"JDWP logcat-failed phase={phase} exception-type={exception.GetType ().Name}");
			} catch (Exception) {
				// A broken diagnostic writer must not replace the launch outcome.
			}
		}

		internal async Task CaptureAsync (int pid, CancellationToken token)
		{
			if (pid <= 0) {
				logWriter ("JDWP logcat-snapshot result=skipped reason=no-pid");
				return;
			}

			// `-t <seconds>.<ms>` selects entries since a time and implies -d; a value without '.' would be a line count.
			// `--pid` and the time are filtered by logd and `-s` by logcat itself, so the parser checks all three again.
			var command = string.Format (CultureInfo.InvariantCulture,
				"logcat -d -v threadtime -v epoch -t {0}.000 --pid={1} -b main,system -s ActivityThread:W System.out:I",
				windowStartSeconds, pid);
			var parser = new LogcatMarkerParser (pid, windowStartSeconds, MaxMarkers);
			var hostBegin = DateTimeOffset.UtcNow;
			var stopwatch = Stopwatch.StartNew ();
			var result = await device.RunBoundedShellCommandAsync (command, SnapshotMaxBytes, SnapshotMaxLines, SnapshotTimeout, parser.AddLine, token).ConfigureAwait (false);
			var hostEnd = DateTimeOffset.UtcNow;

			for (int i = 0; i < parser.Markers.Count; i++)
				logWriter ($"JDWP logcat-marker order={i} kind={parser.Markers [i].Kind} guest-ms={parser.Markers [i].GuestMs}");

			var outcome = result.Status == BoundedShellCommandStatus.Completed && parser.Malformed > 0
				? "malformed"
				: FormatStatus (result.Status);
			logWriter ($"JDWP logcat-snapshot result={outcome} host-utc-begin={FormatUtc (hostBegin)} host-utc-end={FormatUtc (hostEnd)} duration-ms={stopwatch.ElapsedMilliseconds}" +
				$" bytes={result.Bytes} lines={result.Lines} parsed={parser.Parsed} dividers={parser.Dividers} malformed={parser.Malformed}" +
				$" pid-mismatch={parser.PidMismatch} before-window={parser.BeforeWindow} other={parser.Other}" +
				$" markers={parser.MarkerTotal} marker-cap={(parser.MarkerTotal > parser.Markers.Count ? "true" : "false")}" +
				$" first-guest-ms={FormatGuestMs (parser.FirstGuestMs)} last-guest-ms={FormatGuestMs (parser.LastGuestMs)} limits={Limits}");
		}

		internal static bool TryParseEpochSeconds (string? value, out long seconds)
		{
			seconds = 0;
			if (value == null || value.Length == 0 || value.Length > 12)
				return false;
			foreach (var c in value) {
				if (c < '0' || c > '9')
					return false;
			}
			return long.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out seconds) && seconds > 0;
		}

		static string FormatStatus (BoundedShellCommandStatus status)
		{
			switch (status) {
			case BoundedShellCommandStatus.Completed:
				return "completed";
			case BoundedShellCommandStatus.Timeout:
				return "timeout";
			case BoundedShellCommandStatus.ByteLimit:
				return "bytecap";
			case BoundedShellCommandStatus.LineLimit:
				return "linecap";
			default:
				return "read-failure";
			}
		}

		static string FormatUtc (DateTimeOffset value) => value.ToString ("O", CultureInfo.InvariantCulture);

		static string FormatGuestMs (long? value) => value.HasValue ? value.Value.ToString (CultureInfo.InvariantCulture) : "none";
	}

	// Parses `logcat -v threadtime -v epoch` lines (AOSP 16 liblog/logprint.cpp) and keeps only fixed
	// marker kinds from frameworks/base ActivityThread.java and Debug.java. Message text, tags and
	// identifiers are never retained.
	sealed class LogcatMarkerParser
	{
		static readonly Regex LineFormat = new Regex (
			@"^\s*(?<sec>\d{1,12})\.(?<ms>\d{3})\s+(?<pid>\d+)\s+\d+\s+(?<pri>[VDIWEFS])\s+(?<tag>.*?)\s*: (?<msg>.*)$",
			RegexOptions.CultureInvariant);
		static readonly Regex Settled = new Regex (@"^debugger has settled \(-?\d+\)$", RegexOptions.CultureInvariant);
		// android-16.0.0_r1 frameworks/base/core/java/android/app/ActivityThread.java:7910-7911,7931-7932.
		static readonly Regex ApplicationMarker = new Regex (
			@"\AApplication \S+ is (?<state>waiting for the debugger \.\.\.|suspending\. Debugger needs to resume to continue\.)\z",
			RegexOptions.CultureInvariant);

		readonly int pid;
		readonly long windowStartMs;
		readonly int maxMarkers;

		public LogcatMarkerParser (int pid, long windowStartSeconds, int maxMarkers)
		{
			this.pid = pid;
			windowStartMs = windowStartSeconds * 1000;
			this.maxMarkers = maxMarkers;
		}

		public List<(string Kind, long GuestMs)> Markers { get; } = new List<(string Kind, long GuestMs)> ();
		public int MarkerTotal { get; private set; }
		public int Parsed { get; private set; }
		public int Dividers { get; private set; }
		public int Malformed { get; private set; }
		public int PidMismatch { get; private set; }
		public int BeforeWindow { get; private set; }
		public int Other { get; private set; }
		public long? FirstGuestMs { get; private set; }
		public long? LastGuestMs { get; private set; }

		public void AddLine (string line)
		{
			if (line.StartsWith ("--------- ", StringComparison.Ordinal)) {
				Dividers++;
				return;
			}

			var match = LineFormat.Match (line);
			if (!match.Success ||
					!long.TryParse (match.Groups ["sec"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
					!int.TryParse (match.Groups ["pid"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var linePid)) {
				Malformed++;
				return;
			}
			if (linePid != pid) {
				PidMismatch++;
				return;
			}

			long guestMs = seconds * 1000 + int.Parse (match.Groups ["ms"].Value, CultureInfo.InvariantCulture) - windowStartMs;
			if (guestMs < 0) {
				BeforeWindow++;
				return;
			}

			Parsed++;
			FirstGuestMs = FirstGuestMs ?? guestMs;
			LastGuestMs = guestMs;

			var kind = GetMarkerKind (match.Groups ["tag"].Value, match.Groups ["pri"].Value, match.Groups ["msg"].Value);
			if (kind == null) {
				Other++;
				return;
			}
			MarkerTotal++;
			if (Markers.Count < maxMarkers)
				Markers.Add ((kind, guestMs));
		}

		static string? GetMarkerKind (string tag, string priority, string message)
		{
			if (tag == "ActivityThread" && priority == "W") {
				var application = ApplicationMarker.Match (message);
				if (!application.Success)
					return null;
				return application.Groups ["state"].Value.StartsWith ("waiting", StringComparison.Ordinal) ? "app-waiting" : "app-suspending";
			}
			if (tag != "System.out" || priority != "I")
				return null;
			switch (message) {
			case "Sending WAIT chunk":
				return "wait-chunk";
			case "Waiting for debugger first packet":
				return "first-packet-wait";
			case "Debugger has connected":
				return "debugger-connected";
			case "waiting for debugger to settle...":
				return "debugger-settling";
			case "debugger detached?":
				return "debugger-detached";
			case "Debug.suspendAllAndSendVmStart, resumed":
				return "vm-start-resumed";
			}
			return Settled.IsMatch (message) ? "debugger-settled" : null;
		}
	}
}
