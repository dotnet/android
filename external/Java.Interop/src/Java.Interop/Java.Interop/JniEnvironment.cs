using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

		JniEnvironment ()
		{
		}

		internal JniEnvironment (JniEnvironmentSafeHandle safeHandle, JavaVM javaVM)
		{
			SafeHandle = safeHandle;
			if (current == null)
				current = this;
			Invoker = SafeHandle.CreateInvoker ();
			JavaVM  = javaVM;

			Object_class    = new JniType ("java/lang/Class");
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

		public static JniEnvironment GetEnvironmentFromHandle (IntPtr handle)
		{
			if (current != null && current.SafeHandle.DangerousGetHandle () == handle)
				return current;
			var env = new JniEnvironment (new JniEnvironmentSafeHandle (handle), javaVM:null);
			JavaVMSafeHandle vm;
			int r = env.Invoker.GetJavaVM (env.SafeHandle, out vm);
			if (r < 0)
				throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);
			env.JavaVM = JavaVM.FromHandle (vm);
			return env;
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;
			Object_class.Dispose ();
			if (Current == this)
				current = null;
			SafeHandle.Dispose ();
			SafeHandle = null;
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

