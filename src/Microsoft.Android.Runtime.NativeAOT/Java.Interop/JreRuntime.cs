// Originally from: https://github.com/dotnet/java-interop/blob/dd3c1d0514addfe379f050627b3e97493e985da6/src/Java.Runtime.Environment/Java.Interop/JreRuntime.cs
using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Android.Runtime;

namespace Java.Interop {

	class NativeAotRuntimeOptions : JniRuntime.CreationOptions {

		public NativeAotRuntimeOptions ()
		{
			JniVersion  = JniVersion.v1_2;
		}

		/// <safety>
		/// This options instance must contain a live VM and/or current-thread environment with the JNI ABI.
		/// The VM must outlive the runtime, and any class-loader reference must remain live during initialization.
		/// </safety>
		public unsafe JreRuntime CreateJreVM ()
		{
			unsafe {
				// SAFETY: the caller retains the supplied VM, thread environment and loader through runtime initialization.
				return new JreRuntime (this);
			}
		}
	}

	class JreRuntime : JniRuntime
	{
		/// <safety>
		/// <paramref name="builder"/> must supply a live VM and/or current-thread JNI environment with the JNI ABI,
		/// retaining the VM for the runtime lifetime and the class-loader reference through initialization.
		/// </safety>
		internal protected unsafe JreRuntime (NativeAotRuntimeOptions builder)
			: base (builder)
		{
		}

		public override Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			return Android.Runtime.AndroidExceptionManager.GetExceptionForThrowable (ref reference, options);
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
