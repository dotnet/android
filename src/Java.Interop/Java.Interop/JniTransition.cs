#nullable enable

using System;

namespace Java.Interop {

	public struct JniTransition : IDisposable {

		bool        disposed;
		Exception?  pendingException;

		public JniTransition (IntPtr environmentPointer)
		{
			disposed            = false;
			pendingException    = null;

			JniEnvironment.SetEnvironmentPointer (environmentPointer);
		}

		public void SetPendingException (Exception exception)
		{
			if (disposed)
				throw new ObjectDisposedException (nameof (JniTransition));

			pendingException    = exception;
		}

		public void Dispose ()
		{
			if (disposed)
				return;

			disposed    = true;

			if (pendingException != null) {
				JniEnvironment.Runtime.RaisePendingException (pendingException);
				pendingException    = null;
			}
		}
	}
}
