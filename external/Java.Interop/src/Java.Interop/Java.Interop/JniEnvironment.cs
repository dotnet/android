using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public static partial class JniEnvironment {

		internal    static  readonly    ThreadLocal<JniEnvironmentInfo>     Info    = new ThreadLocal<JniEnvironmentInfo> (() => new JniEnvironmentInfo ());

		internal    static  JniEnvironmentInfo      CurrentInfo {
			get {return Info.Value;}
		}

		static              JniEnvironmentInvoker   Invoker {
			get {return Info.Value.Invoker;}
		}

		public      static  JniRuntime              Runtime {
			get {return Info.Value.Runtime;}
		}

		public      static  IntPtr                  EnvironmentPointer {
			get {return Info.Value.EnvironmentPointer;}
		}

		public      static  JniVersion              JniVersion {
			get {return (JniVersion) Versions.GetVersion ();}
		}

		public      static  int                     LocalReferenceCount {
			get {return Info.Value.LocalReferenceCount;}
		}

		internal    static  void    SetEnvironmentPointer (IntPtr environmentPointer)
		{
			Info.Value.EnvironmentPointer   = environmentPointer;
		}

		internal    static  void    SetEnvironmentPointer (IntPtr environmentPointer, JniRuntime runtime)
		{
			if (!Info.IsValueCreated) {
				Info.Value = new JniEnvironmentInfo (environmentPointer, runtime);
				return;
			}
			Info.Value.EnvironmentPointer   = environmentPointer;
		}

		internal    static  void    SetEnvironmentInfo (JniEnvironmentInfo info)
		{
			Info.Value  = info;
		}

		public      static  Exception   GetExceptionForLastThrowable ()
		{
			var e   = JniEnvironment.Exceptions.ExceptionOccurred ();
			if (!e.IsValid)
				return null;
			// JniEnvironment.Errors.ExceptionDescribe ();
			JniEnvironment.Exceptions.ExceptionClear ();
			JniEnvironment.LogCreateLocalRef (e);
			return Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.DisposeSourceReference);
		}

		internal    static  Exception   GetExceptionForLastThrowable (IntPtr thrown)
		{
			if (thrown == IntPtr.Zero)
				return null;
			var e   = new JniObjectReference (thrown, JniObjectReferenceType.Local);
			// JniEnvironment.Errors.ExceptionDescribe ();
			JniEnvironment.Exceptions.ExceptionClear ();
			JniEnvironment.LogCreateLocalRef (e);
			return Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.DisposeSourceReference);
		}
		internal    static  void        LogCreateLocalRef (JniObjectReference value)
		{
			if (!value.IsValid)
				return;
			Runtime.ObjectReferenceManager.CreatedLocalReference (Info.Value, value);
		}

#if FEATURE_JNIENVIRONMENT_SAFEHANDLES
		internal    static  void    PushLocalReferenceFrame ()
		{
			Info.Value.LocalReferences.Add (new List<JniLocalReference> ());
		}

		internal    static  void    PopLocalReferenceFrame ()
		{
			var localRefs   = Info.Value.LocalReferences;
			int last        = localRefs.Count - 1;
			var curRefs     = localRefs [last];
			localRefs.RemoveAt (last);

			foreach (var lref in curRefs) {
				// check required due to https://bugzilla.xamarin.com/show_bug.cgi?id=25850
				if (!lref.IsClosed)
					lref.Dispose ();
			}
		}

		internal    static  void    AddLocalReference (JniLocalReference value)
		{
			var localRefs   = Info.Value.LocalReferences;
			var cur         = localRefs [localRefs.Count - 1];
			cur.Add (value);
		}

		internal    static  void    LogCreateLocalRef (JniLocalReference value)
		{
			if (value == null || value.IsInvalid || value.IsClosed)
				return;
			var r = new JniObjectReference (value, JniObjectReferenceType.Local);
			LogCreateLocalRef (r);
		}

		internal static     void    DeleteLocalReference (JniLocalReference value, IntPtr handle)
		{
			var localRefs   = Info.Value.LocalReferences;
			var c           = localRefs.FirstOrDefault (r => r.Contains (value));
			if (c == null) {
				Runtime.ObjectReferenceManager.WriteLocalReferenceLine (
						"Deleting JNI local reference handle 0x{0} from wrong thread id={1}! Ignoring...",
						handle.ToString ("x"), Thread.CurrentThread.ManagedThreadId);
				Runtime.ObjectReferenceManager.WriteLocalReferenceLine ("{0}",
						System.Activator.CreateInstance (Type.GetType ("System.Diagnostics.StackTrace")));
				return;
			}
			c.Remove (value);
			var lref    = new JniObjectReference (value, JniObjectReferenceType.Local);
			Runtime.ObjectReferenceManager.DeleteLocalReference (Info.Value, ref lref);
			value.SetHandleAsInvalid ();
		}

		internal    static  bool    IsHandleValid (JniLocalReference lref)
		{
			if (lref == null || lref.IsInvalid || lref.IsClosed)
				return false;

			return  Info.Value.LocalReferences.FirstOrDefault (r => r.Contains (lref)) != null;
		}
#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES

#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
		internal    static  void    LogCreateLocalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			var r = new JniObjectReference (value, JniObjectReferenceType.Local);
			LogCreateLocalRef (r);
		}
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
	}

	public  class JniEnvironmentInfo {

		IntPtr                  environmentPointer;

		public      JniRuntime              Runtime                 {get; private set;}
		internal    JniEnvironmentInvoker   Invoker                 {get; private set;}
		public      int                     LocalReferenceCount     {get; internal set;}

		public      IntPtr                  EnvironmentPointer {
			get {return environmentPointer;}
			set {
				if (environmentPointer == value)
					return;

				environmentPointer  = value;
				Invoker             = CreateInvoker (environmentPointer);

				IntPtr  vmh;
				int r   = Invoker.GetJavaVM (EnvironmentPointer, out vmh);
				if (r < 0)
					throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);

				var vm = JniRuntime.GetRegisteredRuntime (vmh);
				if (vm == null)
					throw new NotSupportedException (
							string.Format ("No JavaVM registered with handle 0x{0}.",
								vmh.ToString ("x")));
				Runtime = vm;
			}
		}

		public JniEnvironmentInfo ()
		{
			Runtime             = JniRuntime.Current;
			EnvironmentPointer  = Runtime._AttachCurrentThread ();
		}

		internal    JniEnvironmentInfo (IntPtr environmentPointer, JniRuntime runtime)
		{
			EnvironmentPointer  = environmentPointer;
			Runtime             = runtime;
		}

#if FEATURE_JNIENVIRONMENT_SAFEHANDLES
		internal    List<List<JniLocalReference>>   LocalReferences = new List<List<JniLocalReference>> () {
			new List<JniLocalReference> (),
		};
#endif  // FEATURE_JNIENVIRONMENT_SAFEHANDLES

		static unsafe JniEnvironmentInvoker CreateInvoker (IntPtr handle)
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return new JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}
	}
}

