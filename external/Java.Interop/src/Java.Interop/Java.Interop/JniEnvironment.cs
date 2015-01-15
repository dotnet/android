using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public sealed class JniEnvironmentSafeHandle : SafeHandle
	{
		JniEnvironmentSafeHandle ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		public JniEnvironmentSafeHandle (IntPtr handle)
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

		[ThreadStatic]
		static  JniEnvironmentSafeHandle    handle;

		[ThreadStatic]
		static JniEnvironment               current;

		JniEnvironment                      previous;
		JavaVM                              vm;
		bool                                disposed;
		Exception                           pendingException;

		internal JniEnvironment (JniEnvironmentSafeHandle safeHandle, JavaVM javaVM)
		{
			handle  = safeHandle;
			vm      = javaVM;
			Invoker = SafeHandle.CreateInvoker ();

			previous    = current;
			current     = this;
		}

		public JniEnvironment (IntPtr jniEnvironmentHandle)
			: this (GetCurrentHandle (jniEnvironmentHandle), null)
		{
		}

		static JniEnvironmentSafeHandle GetCurrentHandle (IntPtr jniEnvironmentHandle)
		{
			if (handle == null)
				return new JniEnvironmentSafeHandle (jniEnvironmentHandle);

			if (handle.DangerousGetHandle () == jniEnvironmentHandle)
				return handle;

			handle.Dispose ();
			handle = null;
			return new JniEnvironmentSafeHandle (jniEnvironmentHandle);
		}

		internal JniEnvironmentInvoker Invoker;

		public JniEnvironmentSafeHandle SafeHandle  {
			get {return handle ?? RootEnvironment.SafeHandle;}
		}

		public JavaVM JavaVM {
			get {
				if (vm != null)
					return vm;

				JavaVMSafeHandle vmh;
				int r = Invoker.GetJavaVM (SafeHandle, out vmh);
				if (r < 0)
					throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);

				vm = JavaVM.GetRegisteredJavaVM (vmh);
				if (vm == null)
					throw new NotSupportedException (
							string.Format ("No JavaVM registered with handle 0x{0}.",
								vmh.DangerousGetHandle ().ToString ("x")));

				return vm;
			}
		}

		List<JniLocalReference> lrefs;
		internal List<JniLocalReference> LocalReferences {
			get {return lrefs ?? (lrefs = new List<JniLocalReference> ());}
		}

		internal    static  bool    HasCurrent {
			get {return current != null;}
		}

		public static   JniEnvironment              Current {
			get {
				return current ?? RootEnvironment;
			}
		}

		internal    static  ThreadLocal<JniEnvironment>     RootEnvironments = new ThreadLocal<JniEnvironment> (
				() => JavaVM.Current.AttachCurrentThread ()
		);

		public static JniEnvironment RootEnvironment {
			get {
				return RootEnvironments.Value ??
					(RootEnvironments.Value = JavaVM.Current.AttachCurrentThread ());
			}
		}

		internal static void SetRootEnvironment (JniEnvironment environment)
		{
			RootEnvironments.Value  = environment;
		}

		public void SetPendingException (Exception e)
		{
			pendingException    = e;
		}

		public void Dispose ()
		{
			if (disposed || handle == null || handle.IsInvalid)
				return;

			disposed    = true;

			if (lrefs != null) {
				// Copy required as lref.Dispose() calls DeleteLocalReference(), alters lrefs.
				var refs    = lrefs.ToList ();
				foreach (var lref in refs) {
					// check required due to https://bugzilla.xamarin.com/show_bug.cgi?id=25850
					if (!lref.IsClosed)
						lref.Dispose ();
				}
				lrefs       = null;
			}

			if (pendingException != null)
				Errors.Throw (pendingException);

			if (Obj_toS != null)
				Obj_toS.Dispose ();
			if (Cls_getN != null)
				Cls_getN.Dispose ();

			if ((previous == null && !RootEnvironments.IsValueCreated) ||
					(RootEnvironments.IsValueCreated && RootEnvironment == this)) {
				handle.Dispose ();
				handle                  = null;
				RootEnvironments.Value  = null;
			}

			current     = previous;
		}

		public JniVersion JniVersion {
			get {return (JniVersion) Versions.GetVersion ();}
		}

		internal    int     LrefCount;

		public int LocalReferenceCount {
			get {
				int lc  = 0;
				for (var c = this; c != null; c = c.previous) {
					lc += c.LrefCount;
				}
				return lc;
			}
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

		JniInstanceMethodID Obj_toS;
		internal    JniInstanceMethodID Object_toString {
			get {
				if (Obj_toS != null)
					return Obj_toS;

				using (var t = new JniType ("java/lang/Object"))
					Obj_toS     = t.GetInstanceMethod ("toString", "()Ljava/lang/String;");

				return Obj_toS;
			}
		}

		JniInstanceMethodID Cls_getN;
		internal    JniInstanceMethodID Class_getName {
			get {
				if (Cls_getN != null)
					return Cls_getN;
				using (var t = new JniType ("java/lang/Class"))
					Cls_getN    = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");

				return Cls_getN;
			}
		}

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

