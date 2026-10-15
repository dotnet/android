using System;

namespace Android.Runtime {
	public static class JNINativeWrapper {

		[Obsolete ("JNI delegate wrapping is no longer supported. Trimmable typemaps invoke marshal methods directly instead of legacy connector methods.", error: true)]
		public static Delegate CreateDelegate (Delegate dlg)
		{
			throw new NotSupportedException ("JNI delegate wrapping is no longer supported. Trimmable typemaps invoke marshal methods directly instead of legacy connector methods.");
		}
	}
}
