using System;

namespace Java.Lang {

	partial class RuntimeException {

		[Obsolete ("This member does not exist on Android. It was erroneously bound.", error:true)]
		protected RuntimeException (string p0, Throwable p1, bool p2, bool p3)
		{
			throw new NotSupportedException ("The Java.Lang.RuntimeException(string, Throwable, bool, bool) constructor was erroneously bound. It does not exist.");
		}
	}
}
