using System;
using System.Runtime.InteropServices;

namespace Android.Runtime {
	internal static class Logger {
		static bool hasNoLibLog;

		internal static LogCategories Categories;

		internal static bool LogAssembly {
			get {return (Categories & LogCategories.Assembly) != 0;}
		}

		internal static bool LogDebugger {
			get {return (Categories & LogCategories.Debugger) != 0;}
		}

		internal static bool LogGC {
			get {return (Categories & LogCategories.GC) != 0;}
		}

		internal static bool LogGlobalRef {
			get {return (Categories & LogCategories.GlobalRef) != 0;}
		}

		internal static bool LogLocalRef {
			get {return (Categories & LogCategories.LocalRef) != 0;}
		}

		internal static bool LogTiming {
			get {return (Categories & LogCategories.Timing) != 0;}
		}

		internal static bool LogBundle {
			get {return (Categories & LogCategories.Bundle) != 0;}
		}

		internal static bool LogNet {
			get {return (Categories & LogCategories.Net) != 0;}
		}

		internal static bool LogNetlink {
			get {return (Categories & LogCategories.Netlink) != 0;}
		}

		[DllImport ("liblog")]
		static extern void __android_log_print (LogLevel level, string appname, string format, string args, IntPtr zero);

		public static void Log (LogLevel level, string appname, string log) {
			foreach (var line in (log ?? "").Split (new[]{Environment.NewLine}, StringSplitOptions.None)) {
				if (!hasNoLibLog) {
					try {
						__android_log_print (level, appname, "%s", line, IntPtr.Zero);
					} catch (DllNotFoundException) {
						hasNoLibLog = true;
					}
				}
				if (hasNoLibLog)
					System.Console.WriteLine ("[{0}] {1}: {2}", level, appname, line);
			}
		}

		[DllImport ("__Internal", CallingConvention = CallingConvention.Cdecl)]
		extern static uint monodroid_get_log_categories ();

		static Logger ()
		{
			Categories = (LogCategories) monodroid_get_log_categories ();
		}
	}

	// Keep in sync with the LogLevel enum in
	// monodroid/libmonodroid/logger.{c,h}
	internal enum LogLevel {
		Unknown = 0x00,
		Default = 0x01,
		Verbose = 0x02,
		Debug   = 0x03,
		Info    = 0x04,
		Warn    = 0x05,
		Error   = 0x06,
		Fatal   = 0x07,
		Silent  = 0x08
	}

	// Keep in sync with the LogCategories enum in
	// monodroid/libmonodroid/logger.{c,h}
	[Flags]
	internal enum LogCategories {
		None      = 0,
		Default   = 1 << 0,
		Assembly  = 1 << 1,
		Debugger  = 1 << 2,
		GC        = 1 << 3,
		GlobalRef = 1 << 4,
		LocalRef  = 1 << 5,
		Timing    = 1 << 6,
		Bundle    = 1 << 7,
		Net       = 1 << 8,
		Netlink   = 1 << 9,
	}
}
