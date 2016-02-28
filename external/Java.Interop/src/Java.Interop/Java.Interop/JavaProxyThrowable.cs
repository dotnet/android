using System;

namespace Java.Interop {

	[JniTypeSignature (JniTypeName)]
	class JavaProxyThrowable : JavaException
	{
		new internal    const   string  JniTypeName = "com/xamarin/java_interop/internal/JavaProxyThrowable";

		static void __RegisterNativeMembers (JniType type, string members)
		{
		}

		public  Exception   Exception {get; private set;}

		public JavaProxyThrowable (Exception exception)
			: base (_GetMessage (exception))
		{
			Exception   = exception;
		}

		static string _GetMessage (Exception exception)
		{
			if (exception == null)
				throw new ArgumentNullException ("exception");
			return exception.ToString ();
		}
	}
}

