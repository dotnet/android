using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public sealed class JniEnvironmentSafeHandle : SafeHandle
	{
		JniEnvironmentSafeHandle ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		internal JniEnvironmentSafeHandle (IntPtr handle)
			: this ()
		{
			SetHandle (handle);
		}

		public override bool IsInvalid {
			get {return handle == IntPtr.Zero;}
		}

		protected override bool ReleaseHandle ()
		{
			return false;
		}

		internal unsafe JniEnvironmentInvoker CreateInvoker ()
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return new JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	public sealed partial class JniEnvironment : IDisposable {

		internal JniEnvironment (JniEnvironmentSafeHandle safeHandle, JavaVM javaVM)
		{
			SafeHandle = safeHandle;
			Invoker = SafeHandle.CreateInvoker ();
			JavaVM  = javaVM;

			if (current == null)
				current = this;

			Object_class    = new JniType ("java/lang/Object");
			Object_toString = Object_class.GetInstanceMethod ("toString", "()Ljava/lang/String;");
		}

		internal JniEnvironmentInvoker Invoker;

		public          JniEnvironmentSafeHandle    SafeHandle  {get; private set;}
		public          JavaVM                      JavaVM      {get; private set;}

		[ThreadStatic]
		static JniEnvironment current;
		public static   JniEnvironment              Current {
			get {
				if (current != null)
					return current;
				JavaVM.Current.AttachCurrentThread ();
				return current;
			}
		}

		public static void CheckCurrent (IntPtr jniEnvironmentHandle)
		{
			if (current != null && current.SafeHandle.DangerousGetHandle () == jniEnvironmentHandle)
				return;
			if (current != null)
				current.Dispose ();
			current = null;

			JavaVMSafeHandle vm;

			var h = new JniEnvironmentSafeHandle (jniEnvironmentHandle);
			var i = h.CreateInvoker ();
			int r = i.GetJavaVM (h, out vm);
			if (r < 0)
				throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);

			var jvm = JavaVM.GetRegisteredJavaVM (vm);
			if (jvm == null)
				throw new NotSupportedException (
						string.Format ("No JavaVM registered with handle 0x{0}.",
							vm.DangerousGetHandle ().ToString ("x")));

			new JniEnvironment (h, jvm);
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;
			Object_class.Dispose ();
			Object_toString.Dispose ();
			if (current == this)
				current = null;
			JavaVM.UnTrack (SafeHandle);
			SafeHandle.Dispose ();
			SafeHandle = null;
			JavaVM     = null;
		}

		int LrefCount;

		public int LocalReferenceCount {
			get {return LrefCount;}
		}

		internal void LogCreateLocalRef (JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return;
			Interlocked.Increment (ref LrefCount);
			JavaVM.LogCreateLocalRef (SafeHandle, value);
		}

		internal void LogCreateLocalRef (JniLocalReference value, JniReferenceSafeHandle sourceValue)
		{
			if (value == null || value.IsInvalid)
				return;
			Interlocked.Increment (ref LrefCount);
			JavaVM.LogCreateLocalRef (SafeHandle, value, sourceValue);
		}

		internal void LogDestroyLocalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			Interlocked.Decrement (ref LrefCount);
			JavaVM.LogDestroyLocalRef (SafeHandle, value);
		}

		            JniType             Object_class;
		internal    JniInstanceMethodID Object_toString;

		public Exception GetExceptionForLastThrowable ()
		{
			using (var e = JniErrors.ExceptionOccurred ()) {
				if (e == null || e.IsInvalid)
					return null;
				JniErrors.ExceptionDescribe ();
				JniErrors.ExceptionClear ();
				return JavaVM.GetExceptionForThrowable (e);
			}
		}
	}
}

