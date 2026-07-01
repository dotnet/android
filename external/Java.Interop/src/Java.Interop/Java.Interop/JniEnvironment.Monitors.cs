#nullable enable

using System;
using System.Runtime.ExceptionServices;

namespace Java.Interop {

	partial class JniEnvironment {

		partial class Monitors {

			public static void MonitorEnter (JniObjectReference instance)
			{
				int r = _MonitorEnter (instance);
				if (r != 0) {
					throw new InvalidOperationException (string.Format ("Could not enter monitor; JNIEnv::MonitorEnter() returned {0}.", r));
				}
			}

			public static void MonitorExit (JniObjectReference instance)
			{
				int r = _MonitorExit (instance);
				if (r != 0) {
					var e   = JniEnvironment.GetExceptionForLastThrowable ();
					if (e != null) {
						ExceptionDispatchInfo.Capture (e).Throw ();
					}
					throw new InvalidOperationException (string.Format ("Could not exit monitor; JNIEnv::MonitorExit() returned {0}.", r));
				}
			}
		}
	}
}

