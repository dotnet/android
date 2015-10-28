using System;

namespace Java.Interop {

	public struct JniTransition : IDisposable {

		Exception   pendingException;

		public JniTransition (IntPtr environmentPointer)
		{
			pendingException    = null;

			JniEnvironment.SetEnvironmentPointer (environmentPointer);
#if FEATURE_JNIENVIRONMENT_SAFEHANDLES
			JniEnvironment.PushLocalReferenceFrame ();
#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES
		}

		public void SetPendingException (Exception exception)
		{
			pendingException    = exception;
		}

		public void Dispose ()
		{
			if (pendingException != null) {
				JniEnvironment.Runtime.RaisePendingException (pendingException);
				pendingException    = null;
			}
#if FEATURE_JNIENVIRONMENT_SAFEHANDLES
			JniEnvironment.PopLocalReferenceFrame ();
#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES
		}
	}
}

