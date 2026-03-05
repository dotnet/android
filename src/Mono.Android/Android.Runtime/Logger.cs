using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Android.Runtime {
	internal static partial class Logger {
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

		[LibraryImport ("liblog", StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		private static partial void __android_log_print (LogLevel level, string appname, string format, string args, IntPtr zero);

		public static void Log (LogLevel level, string appname, string? log) {
			foreach (var line in (log ?? "").Split (new[]{Environment.NewLine}, StringSplitOptions.None)) {
				if (!hasNoLibLog) {
					try {
						__android_log_print (level, appname, "%s", line, IntPtr.Zero);
					} catch (DllNotFoundException) {
						hasNoLibLog = true;
					}
				}
			}
		}

		internal static void SetLogCategories (LogCategories categories) =>
			Categories = categories;
	}
}
