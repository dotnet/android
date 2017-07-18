using System;

namespace Android.Runtime {

	class JavaProxyThrowable : Java.Lang.Error {

		public  readonly Exception InnerException;

		public JavaProxyThrowable (Exception innerException)
			: base (GetDetailMessage (innerException))
		{
			InnerException  = innerException;
		}

		static string GetDetailMessage (Exception innerException)
		{
			if (innerException == null)
				throw new ArgumentNullException ("innerException");

			return innerException.ToString ();
		}
	}
}