using System;

namespace Java.Interop {
	partial class JniEnvironment {

		partial class Errors {

			public static void Throw (Exception e)
			{
				if (e == null)
					throw new ArgumentNullException ("e");
				var je = e as JavaException;
				if (je == null) {
					je  = new JavaProxyThrowable (e);
					// because `je` may cross thread boundaries;
					// We'll need to rely on the GC to cleanup
					je.RegisterWithVM ();
				}
				Throw (je.SafeHandle);
			}
		}
	}
}

