#if JAVA_INTEROP
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

using Java.Interop;
using Microsoft.Android.Runtime;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

namespace Android.Runtime {

	readonly struct ReferenceLoggingConfiguration {
		public string? GrefPath { get; }
		public string? LrefPath { get; }
		public string? OverrideDirectory { get; }
		public bool LightGref { get; }
		public bool LightLref { get; }
		public bool GrefToLogcat { get; }
		public bool LrefToLogcat { get; }

		public ReferenceLoggingConfiguration (
				string? grefPath,
				string? lrefPath,
				string? overrideDirectory,
				bool lightGref,
				bool lightLref,
				bool grefToLogcat,
				bool lrefToLogcat)
		{
			GrefPath = grefPath;
			LrefPath = lrefPath;
			OverrideDirectory = overrideDirectory;
			LightGref = lightGref;
			LightLref = lightLref;
			GrefToLogcat = grefToLogcat;
			LrefToLogcat = lrefToLogcat;
		}
	}

	enum ReferenceLogEvent {
		GlobalCreated,
		GlobalDeleted,
		WeakGlobalCreated,
		WeakGlobalDeleted,
	}

	internal sealed class ManagedObjectReferenceManager : JniRuntime.JniObjectReferenceManager {
		const string GrefLogTag = "monodroid-gref";
		const string LrefLogTag = "monodroid-lref";
		const UnixFileMode ReferenceLogFileMode =
			UnixFileMode.UserRead |
			UnixFileMode.UserWrite |
			UnixFileMode.GroupRead |
			UnixFileMode.GroupWrite |
			UnixFileMode.OtherRead;

		static ManagedObjectReferenceManager? current;

		readonly object grefLock = new object ();
		readonly object lrefLock = new object ();
		readonly TextWriter? grefLog;
		readonly TextWriter? lrefLog;
		readonly bool grefToLogcat;
		readonly bool lrefToLogcat;

		int grefCount;
		int weakGrefCount;

		public override int GlobalReferenceCount => Volatile.Read (ref grefCount);
		public override int WeakGlobalReferenceCount => Volatile.Read (ref weakGrefCount);

		public override bool LogGlobalReferenceMessages => GlobalReferenceLoggingEnabled;
		public override bool LogLocalReferenceMessages => LocalReferenceLoggingEnabled;

		static bool GlobalReferenceLoggingEnabled => RuntimeFeature.ObjectReferenceLogging && Logger.LogGlobalRef;
		static bool LocalReferenceLoggingEnabled => RuntimeFeature.ObjectReferenceLogging && Logger.LogLocalRef;

		public ManagedObjectReferenceManager ()
			: this (JNIEnvInit.ReferenceLoggingConfiguration)
		{
			Volatile.Write (ref current, this);
			unsafe {
				RuntimeNativeMethods._monodroid_register_reference_logging_callbacks (
					&LogReferenceFromNative,
					&LogMessageFromNative,
					GlobalReferenceLoggingEnabled ? (byte) 1 : (byte) 0);
			}
		}

		ManagedObjectReferenceManager (ReferenceLoggingConfiguration configuration)
		{
			grefToLogcat = configuration.GrefToLogcat;
			lrefToLogcat = configuration.LrefToLogcat;

			if (RuntimeFeature.ObjectReferenceLogging) {
				grefLog = CreateLogWriter (
					Logger.LogGlobalRef,
					configuration.LightGref,
					configuration.GrefPath,
					configuration.OverrideDirectory,
					"grefs.txt",
					GrefLogTag);

				if (grefLog != null && Logger.LogLocalRef && !configuration.LightLref &&
						!string.IsNullOrEmpty (configuration.LrefPath) &&
						configuration.LrefPath == configuration.GrefPath) {
					lrefLog = grefLog;
				} else {
					lrefLog = CreateLogWriter (
						Logger.LogLocalRef,
						configuration.LightLref,
						configuration.LrefPath,
						configuration.OverrideDirectory,
						"lrefs.txt",
						LrefLogTag);
				}
			}

		}

		internal ManagedObjectReferenceManager (TextWriter? grefLog, TextWriter? lrefLog, bool grefToLogcat = false, bool lrefToLogcat = false)
		{
			if (grefLog != null && lrefLog != null && object.ReferenceEquals (grefLog, lrefLog)) {
				this.grefLog = this.lrefLog = TextWriter.Synchronized (grefLog);
			} else {
				this.grefLog = grefLog == null ? null : TextWriter.Synchronized (grefLog);
				this.lrefLog = lrefLog == null ? null : TextWriter.Synchronized (lrefLog);
			}
			this.grefToLogcat = grefToLogcat;
			this.lrefToLogcat = lrefToLogcat;
		}

		internal static TextWriter? CreateLogWriter (
				bool enabled,
				bool light,
				string? customPath,
				string? overrideDirectory,
				string fallbackFilename,
				string logTag)
		{
			if (!enabled || light) {
				return null;
			}

			if (!string.IsNullOrEmpty (customPath)) {
				var writer = TryCreateLogWriter (customPath, logTag);
				if (writer != null) {
					return writer;
				}
			}

			if (string.IsNullOrEmpty (overrideDirectory)) {
				return null;
			}

			try {
				Directory.CreateDirectory (overrideDirectory);
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SecurityException) {
				Logger.Log (LogLevel.Warn, logTag, $"Could not create directory '{overrideDirectory}' for reference logging: {e.Message}");
				return null;
			}

			return TryCreateLogWriter (Path.Combine (overrideDirectory, fallbackFilename), logTag);
		}

		static TextWriter? TryCreateLogWriter (string path, string logTag)
		{
			try {
				var stream = new FileStream (path, FileMode.Create, FileAccess.Write, FileShare.Read);
				try {
					File.SetUnixFileMode (path, ReferenceLogFileMode);
				} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SecurityException || e is PlatformNotSupportedException) {
					Logger.Log (LogLevel.Warn, logTag, $"Could not set permissions on reference log '{path}': {e.Message}");
				}
				var writer = new StreamWriter (stream, new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
				Logger.Log (LogLevel.Debug, logTag, $"Opened file '{path}' for logging.");
				return TextWriter.Synchronized (writer);
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SecurityException) {
				Logger.Log (LogLevel.Warn, logTag, $"Could not open path '{path}' for reference logging: {e.Message}");
				return null;
			}
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			var reference = base.CreateLocalReference (value, ref localReferenceCount);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					LogLocalReference (created: true, localReferenceCount, reference);
				}
			}
			return reference;
		}

		public override void DeleteLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (!value.IsValid) {
				return;
			}

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var reference = value;
					base.DeleteLocalReference (ref value, ref localReferenceCount);
					LogLocalReference (created: false, localReferenceCount, reference);
					return;
				}
			}
			base.DeleteLocalReference (ref value, ref localReferenceCount);
		}

		public override void CreatedLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			if (!value.IsValid) {
				return;
			}

			base.CreatedLocalReference (value, ref localReferenceCount);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					LogLocalReference (created: true, localReferenceCount, value);
				}
			}
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (!value.IsValid) {
				return IntPtr.Zero;
			}

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var reference = value;
					var handle = base.ReleaseLocalReference (ref value, ref localReferenceCount);
					LogLocalReference (created: false, localReferenceCount, reference);
					return handle;
				}
			}
			return base.ReleaseLocalReference (ref value, ref localReferenceCount);
		}

		void LogLocalReference (bool created, int localReferenceCount, JniObjectReference reference)
		{
			string line = string.Create (
				CultureInfo.InvariantCulture,
				$"{(created ? "+l+" : "-l-")} lrefc {localReferenceCount} handle {FormatHandle (reference.Handle, GetObjectRefType (reference.Type))} from thread '{GetThreadName ()}'({Environment.CurrentManagedThreadId})");
			WriteReference (lrefLog, lrefLock, lrefToLogcat, LrefLogTag, line, new StackTrace (true).ToString ());
		}

		public override void WriteLocalReferenceLine (string format, params object?[] args)
		{
			if (!LocalReferenceLoggingEnabled) {
				return;
			}
			WriteReference (
				lrefLog,
				lrefLock,
				lrefToLogcat,
				LrefLogTag,
				"[LREF] " + string.Format (CultureInfo.InvariantCulture, format, args),
				stackTrace: null);
		}

		public override void WriteGlobalReferenceLine (string format, params object?[] args)
		{
			if (!GlobalReferenceLoggingEnabled) {
				return;
			}
			WriteGlobalReferenceLine (string.Format (CultureInfo.InvariantCulture, format, args));
		}

		void WriteGlobalReferenceLine (string line)
		{
			WriteReference (grefLog, grefLock, grefToLogcat, GrefLogTag, line, stackTrace: null, logWithoutFile: false);
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference value)
		{
			var reference = base.CreateGlobalReference (value);
			int count;
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					count = LogReference (
						ReferenceLogEvent.GlobalCreated,
						value.Handle,
						GetObjectRefType (value.Type),
						reference.Handle,
						GetObjectRefType (reference.Type),
						GetThreadName (),
						Environment.CurrentManagedThreadId,
						new StackTrace (true).ToString ());
				} else {
					count = UpdateReferenceCount (ReferenceLogEvent.GlobalCreated, out _);
				}
			} else {
				count = UpdateReferenceCount (ReferenceLogEvent.GlobalCreated, out _);
			}

			if (count >= JNIEnvInit.gref_gc_threshold) {
				Logger.Log (LogLevel.Warn, "monodroid-gc", count + " outstanding GREFs. Performing a full GC!");
				GC.WaitForPendingFinalizers ();
				GC.Collect ();
			}

			return reference;
		}

		public override void DeleteGlobalReference (ref JniObjectReference value)
		{
			if (!value.IsValid) {
				return;
			}

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					LogReference (
						ReferenceLogEvent.GlobalDeleted,
						value.Handle,
						GetObjectRefType (value.Type),
						IntPtr.Zero,
						(byte) 'I',
						GetThreadName (),
						Environment.CurrentManagedThreadId,
						new StackTrace (true).ToString ());
				} else {
					UpdateReferenceCount (ReferenceLogEvent.GlobalDeleted, out _);
				}
			} else {
				UpdateReferenceCount (ReferenceLogEvent.GlobalDeleted, out _);
			}
			base.DeleteGlobalReference (ref value);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference value)
		{
			var reference = base.CreateWeakGlobalReference (value);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					LogReference (
						ReferenceLogEvent.WeakGlobalCreated,
						value.Handle,
						GetObjectRefType (value.Type),
						reference.Handle,
						GetObjectRefType (reference.Type),
						GetThreadName (),
						Environment.CurrentManagedThreadId,
						new StackTrace (true).ToString ());
				} else {
					UpdateReferenceCount (ReferenceLogEvent.WeakGlobalCreated, out _);
				}
			} else {
				UpdateReferenceCount (ReferenceLogEvent.WeakGlobalCreated, out _);
			}
			return reference;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference value)
		{
			if (!value.IsValid) {
				return;
			}

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					LogReference (
						ReferenceLogEvent.WeakGlobalDeleted,
						value.Handle,
						GetObjectRefType (value.Type),
						IntPtr.Zero,
						(byte) 'I',
						GetThreadName (),
						Environment.CurrentManagedThreadId,
						new StackTrace (true).ToString ());
				} else {
					UpdateReferenceCount (ReferenceLogEvent.WeakGlobalDeleted, out _);
				}
			} else {
				UpdateReferenceCount (ReferenceLogEvent.WeakGlobalDeleted, out _);
			}
			base.DeleteWeakGlobalReference (ref value);
		}

		internal int LogReference (
				ReferenceLogEvent kind,
				IntPtr currentHandle,
				byte currentType,
				IntPtr newHandle,
				byte newType,
				string? threadName,
				int threadId,
				string? stackTrace)
		{
			int globalCount = UpdateReferenceCount (kind, out int weakCount);

			if (GlobalReferenceLoggingEnabled) {
				string line = FormatReferenceMessage (
					kind,
					globalCount,
					weakCount,
					currentHandle,
					currentType,
					newHandle,
					newType,
					threadName,
					threadId);
				WriteReference (grefLog, grefLock, grefToLogcat, GrefLogTag, line, stackTrace);
			}

			return globalCount;
		}

		internal int UpdateReferenceCount (ReferenceLogEvent kind, out int weakCount)
		{
			int globalCount;

			switch (kind) {
				case ReferenceLogEvent.GlobalCreated:
					globalCount = Interlocked.Increment (ref grefCount);
					weakCount = Volatile.Read (ref weakGrefCount);
					break;
				case ReferenceLogEvent.GlobalDeleted:
					globalCount = Interlocked.Decrement (ref grefCount);
					weakCount = Volatile.Read (ref weakGrefCount);
					break;
				case ReferenceLogEvent.WeakGlobalCreated:
					weakCount = Interlocked.Increment (ref weakGrefCount);
					globalCount = Volatile.Read (ref grefCount);
					break;
				case ReferenceLogEvent.WeakGlobalDeleted:
					weakCount = Interlocked.Decrement (ref weakGrefCount);
					globalCount = Volatile.Read (ref grefCount);
					break;
				default:
					throw new ArgumentOutOfRangeException (nameof (kind));
			}

			return globalCount;
		}

		internal static string FormatReferenceMessage (
				ReferenceLogEvent kind,
				int globalCount,
				int weakCount,
				IntPtr currentHandle,
				byte currentType,
				IntPtr newHandle,
				byte newType,
				string? threadName,
				int threadId)
		{
			string prefix;
			string handles;
			switch (kind) {
				case ReferenceLogEvent.GlobalCreated:
					prefix = "+g+";
					handles = $"obj-handle {FormatHandle (currentHandle, currentType)} -> new-handle {FormatHandle (newHandle, newType)}";
					break;
				case ReferenceLogEvent.GlobalDeleted:
					prefix = "-g-";
					handles = $"handle {FormatHandle (currentHandle, currentType)}";
					break;
				case ReferenceLogEvent.WeakGlobalCreated:
					prefix = "+w+";
					handles = $"obj-handle {FormatHandle (currentHandle, currentType)} -> new-handle {FormatHandle (newHandle, newType)}";
					break;
				case ReferenceLogEvent.WeakGlobalDeleted:
					prefix = "-w-";
					handles = $"handle {FormatHandle (currentHandle, currentType)}";
					break;
				default:
					throw new ArgumentOutOfRangeException (nameof (kind));
			}

			return string.Create (
				CultureInfo.InvariantCulture,
				$"{prefix} grefc {globalCount} gwrefc {weakCount} {handles} from thread '{threadName ?? "<null>"}'({threadId})");
		}

		static string FormatHandle (IntPtr handle, byte type)
		{
			return string.Create (CultureInfo.InvariantCulture, $"0x{unchecked((nuint)handle):x}/{(char) type}");
		}

		static byte GetObjectRefType (JniObjectReferenceType type)
		{
			switch (type) {
				case JniObjectReferenceType.Invalid:      return (byte) 'I';
				case JniObjectReferenceType.Local:        return (byte) 'L';
				case JniObjectReferenceType.Global:       return (byte) 'G';
				case JniObjectReferenceType.WeakGlobal:   return (byte) 'W';
				default:                                  return (byte) '*';
			}
		}

		static string GetThreadName ()
		{
			return Thread.CurrentThread.Name ?? "<null>";
		}

		static void WriteReference (
				TextWriter? writer,
				object sync,
				bool toLogcat,
				string logTag,
				string line,
				string? stackTrace,
				bool logWithoutFile = true)
		{
			if ((writer == null && logWithoutFile) || toLogcat) {
				Logger.Log (LogLevel.Info, logTag, line);
			}
			if (toLogcat && stackTrace != null) {
				Logger.Log (LogLevel.Debug, logTag, stackTrace);
			}
			if (writer == null) {
				return;
			}

			lock (sync) {
				try {
					writer.WriteLine (line);
					if (stackTrace != null) {
						writer.Write (stackTrace);
						if (!stackTrace.EndsWith (Environment.NewLine, StringComparison.Ordinal)) {
							writer.WriteLine ();
						}
					}
					writer.Flush ();
				} catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is UnauthorizedAccessException || e is SecurityException) {
					Logger.Log (LogLevel.Error, logTag, $"Could not write reference log: {e.Message}");
				}
			}
		}

		[UnmanagedCallersOnly]
		internal static void LogReferenceFromNative (
				int kind,
				IntPtr currentHandle,
				byte currentType,
				IntPtr newHandle,
				byte newType,
				IntPtr threadName,
				int threadId,
				IntPtr stackTrace)
		{
			var manager = Volatile.Read (ref current);
			if (manager == null) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Error, LogCategories.Default, "Native GC bridge reference event arrived before the managed reference manager was initialized.");
				return;
			}

			try {
				var eventKind = (ReferenceLogEvent) kind;
				if (RuntimeFeature.ObjectReferenceLogging) {
					if (Logger.LogGlobalRef) {
						manager.LogReference (
							eventKind,
							currentHandle,
							currentType,
							newHandle,
							newType,
							Marshal.PtrToStringUTF8 (threadName),
							threadId,
							Marshal.PtrToStringUTF8 (stackTrace));
						return;
					}
				}
				manager.UpdateReferenceCount (eventKind, out _);
			} catch (Exception e) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Error, LogCategories.Default, $"Managed native reference callback failed: {e}");
			}
		}

		[UnmanagedCallersOnly]
		internal static void LogMessageFromNative (IntPtr message)
		{
			var manager = Volatile.Read (ref current);
			if (manager == null) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Error, LogCategories.Default, "Native GC bridge diagnostic arrived before the managed reference manager was initialized.");
				return;
			}

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					try {
						manager.WriteGlobalReferenceLine (Marshal.PtrToStringUTF8 (message) ?? "");
					} catch (Exception e) {
						RuntimeNativeMethods.monodroid_log (LogLevel.Error, LogCategories.Default, $"Managed native reference message callback failed: {e}");
					}
				}
			}
		}

		protected override void Dispose (bool disposing)
		{
			if (grefLog != null) {
				grefLog.Dispose ();
			}
			if (lrefLog != null && !object.ReferenceEquals (grefLog, lrefLog)) {
				lrefLog.Dispose ();
			}
			base.Dispose (disposing);
		}
	}
}
#endif // JAVA_INTEROP
