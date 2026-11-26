// Originally from: https://github.com/dotnet/java-interop/blob/dd3c1d0514addfe379f050627b3e97493e985da6/src/Java.Runtime.Environment/Java.Interop/JreRuntime.cs
using System;
using System.Diagnostics;
using System.Threading;

namespace Java.Interop {

	class NativeAotJniRuntime : JniRuntime
	{
		/// <safety>
		/// <paramref name="options"/> must supply a live VM and/or current-thread JNI environment with the JNI ABI,
		/// retaining the VM for the runtime lifetime and the class-loader reference through initialization.
		/// </safety>
		internal protected unsafe NativeAotJniRuntime (JniRuntime.CreationOptions options)
			: base (options)
		{
		}

		public override Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			return Android.Runtime.AndroidExceptionManager.GetExceptionForThrowable (ref reference, options);
		}

		public override void OnUserUnhandledException (ref JniTransition transition, Exception e)
		{
			if (Android.Runtime.AndroidEnvironment.TryRaiseUnhandledException (e)) {
				return;
			}

			base.OnUserUnhandledException (ref transition, e);
		}

		public override void RaisePendingException (Exception pendingException)
		{
			Android.Runtime.AndroidExceptionManager.RaisePendingException (pendingException);
		}

		public override string? GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo)
				.ToString ();
		}
	}
}
