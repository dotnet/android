using System;
using System.Collections.Generic;
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

			using (var t = new JniType ("java/lang/Object"))
				Object_toString = t.GetInstanceMethod ("toString", "()Ljava/lang/String;");
			using (var t = new JniType ("java/lang/Class"))
				Class_getName = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
		}

		internal JniEnvironmentInvoker Invoker;

		public          JniEnvironmentSafeHandle    SafeHandle  {get; private set;}
		public          JavaVM                      JavaVM      {get; private set;}

		List<JniLocalReference> lrefs;
		internal List<JniLocalReference> LocalReferences {
			get {return lrefs ?? (lrefs = new List<JniLocalReference> ());}
		}

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
			if (SafeHandle == null || SafeHandle.IsInvalid)
				return;
			Object_toString.Dispose ();
			if (current == this)
				current = null;
			JavaVM.UnTrack (SafeHandle);
			SafeHandle.Dispose ();
			SafeHandle = null;
			JavaVM     = null;
		}

		public JniVersion JniVersion {
			get {return (JniVersion) Versions.GetVersion ();}
		}

		internal    int     LrefCount;

		public int LocalReferenceCount {
			get {return LrefCount;}
		}

		internal void LogCreateLocalRef (JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return;
			JavaVM.JniHandleManager.CreatedLocalReference (this, value);
		}

		internal void DeleteLocalReference (JniLocalReference value, IntPtr handle)
		{
			if (lrefs == null || !lrefs.Contains (value)) {
				Debug.WriteLine ("Deleting JNI local reference handle 0x{0} from wrong thread! Ignoring...", handle.ToString ("x"));
				return;
			}
			lrefs.Remove (value);
			JniEnvironment.Current.JavaVM.JniHandleManager.DeleteLocalReference (this, handle);
		}

		internal    JniInstanceMethodID Object_toString;
		internal    JniInstanceMethodID Class_getName;

		public Exception GetExceptionForLastThrowable ()
		{
			using (var e = JniEnvironment.Errors.ExceptionOccurred ()) {
				if (e == null || e.IsInvalid)
					return null;
				// JniEnvironment.Errors.ExceptionDescribe ();
				JniEnvironment.Errors.ExceptionClear ();
				return JavaVM.GetExceptionForThrowable (e, JniHandleOwnership.Transfer);
			}
		}
	}
}

