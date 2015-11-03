using System;

namespace Java.Interop
{
	static class JniGC {

		internal static void Collect ()
		{
			var runtime = JavaLangRuntime.GetRuntime ();
			try {
				JavaLangRuntime.GC (runtime);
			} finally {
				JniObjectReference.Dispose (ref runtime);
			}
		}
	}
}

