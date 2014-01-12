using System;

namespace Java.Interop
{
	static class JniGC {

		internal static void Collect ()
		{
			using (var runtime = JniRuntime.GetRuntime ())
				JniRuntime.GC (runtime);
		}
	}
}

