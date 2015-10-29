using System;

namespace Java.Interop {
	partial class JniEnvironment {

		partial class Exceptions {

			public static void Throw (Exception e)
			{
				if (e == null)
					throw new ArgumentNullException ("e");
				var je = e as JavaException;
				if (je == null) {
					je  = new JavaProxyThrowable (e);
				}
				Throw (je.PeerReference);
			}
		}
	}
}

