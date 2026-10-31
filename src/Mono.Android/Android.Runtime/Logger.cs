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

		internal static bool LogGC {
			get {return (Categories & LogCategories.GC) != 0;}
		}

		internal static bool LogTiming {
			get {return (Categories & LogCategories.Timing) != 0;}
		}

		internal static bool LogNet {
			get {return (Categories & LogCategories.Net) != 0;}
		}

		/// <safety>
		/// <paramref name="format"/> must consume exactly one UTF-8 string argument, <paramref name="args"/>,
		/// with no additional variadic reads or writes. <paramref name="zero"/> is only an unused extra argument.
		/// </safety>
		[LibraryImport ("liblog", StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		private static unsafe partial void __android_log_print (LogLevel level, string appname, string format, string args, IntPtr zero);

		public static void Log (LogLevel level, string appname, string? log) {
			foreach (var line in (log ?? "").Split (new[]{Environment.NewLine}, StringSplitOptions.None)) {
				if (!hasNoLibLog) {
					try {
						unsafe {
							// SAFETY: the fixed "%s" format consumes exactly the one string marshaled by the stub.
							__android_log_print (level, appname, "%s", line, IntPtr.Zero);
						}
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
