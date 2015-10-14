using System;

namespace Java.Interop
{
	static class JniGC {

		internal static void Collect ()
		{
			var runtime = JniRuntime.GetRuntime ();
			try {
				JniRuntime.GC (runtime);
			} finally {
				JniEnvironment.Handles.Dispose (ref runtime);
			}
		}
	}
}

