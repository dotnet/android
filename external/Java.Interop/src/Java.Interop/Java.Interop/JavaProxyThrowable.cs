#nullable enable

using System;

namespace Java.Interop {

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	sealed class JavaProxyThrowable : JavaException
	{
		new internal    const   string  JniTypeName = "net/dot/jni/internal/JavaProxyThrowable";

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

