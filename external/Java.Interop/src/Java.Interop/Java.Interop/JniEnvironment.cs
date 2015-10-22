using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public sealed partial class JniEnvironment : IDisposable {

		[ThreadStatic]
		static  IntPtr                      envHandle;

		[ThreadStatic]
		static JniEnvironment               current;

		JniEnvironment                      previous;
		JavaVM                              vm;
		bool                                disposed;
		Exception                           pendingException;

		internal JniEnvironment (IntPtr handle, JavaVM javaVM)
		{
			envHandle   = handle;

			vm      = javaVM;
			Invoker = CreateInvoker (handle);

			previous    = current;
			current     = this;
		}

		public JniEnvironment (IntPtr jniEnvironmentHandle)
			: this (GetCurrentHandle (jniEnvironmentHandle), null)
		{
		}

		static IntPtr GetCurrentHandle (IntPtr jniEnvironmentHandle)
		{
			return jniEnvironmentHandle;
		}

		internal JniEnvironmentInvoker Invoker;

		static unsafe JniEnvironmentInvoker CreateInvoker (IntPtr handle)
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return new JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}

		public IntPtr EnvironmentPointer  {
			get {return envHandle != IntPtr.Zero ? envHandle : RootEnvironment.EnvironmentPointer;}
		}

		public JavaVM JavaVM {
			get {
				if (vm != null)
					return vm;

				IntPtr vmh;
				int r = Invoker.GetJavaVM (EnvironmentPointer, out vmh);
				if (r < 0)
					throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);

				vm = JavaVM.GetRegisteredJavaVM (vmh);
				if (vm == null)
					throw new NotSupportedException (
							string.Format ("No JavaVM registered with handle 0x{0}.",
								vmh.ToString ("x")));

				return vm;
			}
		}

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
		List<JniLocalReference> lrefs;
		internal List<JniLocalReference> LocalReferences {
			get {return lrefs ?? (lrefs = new List<JniLocalReference> ());}
		}

		internal static bool IsHandleValid (JniLocalReference lref)
		{
			if (lref == null || lref.IsInvalid || lref.IsClosed)
				return false;

			var e = JniEnvironment.Current;
			for (; e != null; e = e.previous) {
				if (e.lrefs == null)
					continue;
				if (e.lrefs.Contains (lref))
					return true;
			}
			return false;
		}
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES

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
			if (disposed || envHandle == IntPtr.Zero)
				return;

			disposed    = true;

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
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
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES

			if (pendingException != null)
				Exceptions.Throw (pendingException);

			Obj_toS     = null;
			Cls_getN    = null;

			if ((previous == null && !RootEnvironments.IsValueCreated) ||
					(RootEnvironments.IsValueCreated && RootEnvironment == this)) {
				envHandle               = IntPtr.Zero;
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

		internal void LogCreateLocalRef (JniObjectReference value)
		{
			if (!value.IsValid)
				return;
			JavaVM.JniHandleManager.CreatedLocalReference (this, value);
		}

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
		internal void LogCreateLocalRef (JniLocalReference value)
		{
			if (value == null || value.IsInvalid || value.IsClosed)
				return;
			var r = new JniObjectReference (value, JniObjectReferenceType.Local);
			LogCreateLocalRef (r);
		}

		internal void DeleteLocalReference (JniLocalReference value, IntPtr handle)
		{
			var c = current;
			for ( ; c != null; c = c.previous) {
				if (c.lrefs == null || !c.lrefs.Contains (value))
					continue;
				break;
			}
			if (c == null) {
				JavaVM.JniHandleManager.WriteLocalReferenceLine (
						"Deleting JNI local reference handle 0x{0} from wrong thread id={1}! Ignoring...",
						handle.ToString ("x"), Thread.CurrentThread.ManagedThreadId);
				JavaVM.JniHandleManager.WriteLocalReferenceLine ("{0}",
						System.Activator.CreateInstance (Type.GetType ("System.Diagnostics.StackTrace")));
				return;
			}
			c.lrefs.Remove (value);
			var r = new JniObjectReference (value, JniObjectReferenceType.Local);
			JniEnvironment.Current.JavaVM.JniHandleManager.DeleteLocalReference (this, ref r);
			value.SetHandleAsInvalid ();
		}
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
#if FEATURE_HANDLES_ARE_INTPTRS
		internal void LogCreateLocalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			var r = new JniObjectReference (value, JniObjectReferenceType.Local);
			LogCreateLocalRef (r);
		}
#endif  // FEATURE_HANDLES_ARE_INTPTRS

		JniInstanceMethodInfo Obj_toS;
		internal    JniInstanceMethodInfo Object_toString {
			get {
				if (Obj_toS != null)
					return Obj_toS;

				using (var t = new JniType ("java/lang/Object"))
					Obj_toS     = t.GetInstanceMethod ("toString", "()Ljava/lang/String;");

				return Obj_toS;
			}
		}

		JniInstanceMethodInfo Cls_getN;
		internal    JniInstanceMethodInfo Class_getName {
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
			var e = JniEnvironment.Exceptions.ExceptionOccurred ();
			if (!e.IsValid)
				return null;
			// JniEnvironment.Errors.ExceptionDescribe ();
			JniEnvironment.Exceptions.ExceptionClear ();
			JniEnvironment.Current.LogCreateLocalRef (e);
			return JavaVM.GetExceptionForThrowable (ref e, JniHandleOwnership.Transfer);
		}
	}
}

