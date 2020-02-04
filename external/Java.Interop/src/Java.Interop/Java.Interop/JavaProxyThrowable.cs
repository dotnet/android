#nullable enable

using System;

namespace Java.Interop {

	[JniTypeSignature (JniTypeName)]
	sealed class JavaProxyThrowable : JavaException
	{
		new internal    const   string  JniTypeName = "com/xamarin/java_interop/internal/JavaProxyThrowable";

		public  Exception   Exception {get; private set;}

		public JavaProxyThrowable (Exception exception)
			: base (GetMessage (exception))
		{
			Exception   = exception;
		}

		static string GetMessage (Exception exception)
		{
			if (exception == null)
				throw new ArgumentNullException (nameof (exception));
			return exception.ToString ();
		}
	}
}

