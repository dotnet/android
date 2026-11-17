#nullable enable

using System;

namespace Java.Interop {
	partial class JniEnvironment {

		partial class Exceptions {

			public static void Throw (JniObjectReference toThrow)
			{
				if (!toThrow.IsValid)
					throw new ArgumentException (nameof (toThrow));

				int r = _Throw (toThrow);
				if (r != 0)
					throw new InvalidOperationException (string.Format ("Could not raise an exception; JNIEnv::Throw() returned {0}.", r));
			}


			public static void ThrowNew (JniObjectReference klass, string message)
			{
				if (!klass.IsValid)
					throw new ArgumentException (nameof (klass));
				ArgumentNullException.ThrowIfNull (message);

				int r = _ThrowNew (klass, message);
				if (r != 0)
					throw new InvalidOperationException (string.Format ("Could not raise an exception; JNIEnv::ThrowNew() returned {0}.", r));
			}

			public static void Throw (Exception e)
			{
				ArgumentNullException.ThrowIfNull (e);
				Runtime.RaisePendingException (e);
			}
		}
	}
}
