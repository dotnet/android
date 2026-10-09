using System;

using Java.Interop;

namespace Android.Runtime
{
	static class AndroidExceptionManager
	{
		internal static Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			if (!reference.IsValid)
				return null;

			try {
				var peeked = JniEnvironment.Runtime.ValueManager.PeekPeer (reference);
				if (peeked is JavaProxyThrowable proxyThrowable)
					return proxyThrowable.InnerException;
				if (peeked is not Exception exception)
					return Java.Lang.Object.GetObject<Java.Lang.Throwable> (reference.Handle, JniHandleOwnership.DoNotTransfer);

				var unwrapped = JniEnvironment.Runtime.ValueManager.PeekValue (peeked.PeerReference) as Exception;
				return unwrapped ?? exception;
			} finally {
				JniObjectReference.Dispose (ref reference, options);
			}
		}

		internal static void RaisePendingException (Exception pendingException)
		{
			if (pendingException == null)
				throw new ArgumentNullException (nameof (pendingException));

			var throwable = pendingException as JavaException ?? JavaProxyThrowable.Create (pendingException);
			JniEnvironment.Exceptions.Throw (throwable.PeerReference);
			GC.KeepAlive (throwable);
		}
	}
}
