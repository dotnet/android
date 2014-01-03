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
			Debug.WriteLine ("# CreateInvoker: handle=" + handle);
			IntPtr p = Marshal.ReadIntPtr (handle);
			return new JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	#if false
	class JniEnvironmentInvoker {

		JniNativeInterfaceStruct JniEnv;

		public unsafe JniEnvironmentInvoker (JniNativeInterfaceStruct* p)
		{
			JniEnv = *p;
		}

		internal delegate int IntPtr_outIntPtr_int_Delegate (JniEnvironmentSafeHandle env, out JavaVMSafeHandle javaVM);
		IntPtr_outIntPtr_int_Delegate _GetJavaVM;
		public IntPtr_outIntPtr_int_Delegate GetJavaVM {
			get {
				if (_GetJavaVM == null)
					_GetJavaVM = (IntPtr_outIntPtr_int_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.GetJavaVM, typeof (IntPtr_outIntPtr_int_Delegate));
				return _GetJavaVM;
			}
		}

		internal delegate JniLocalReference IntPtr_string_IntPtr_Delegate (JniEnvironmentSafeHandle env, string classname);

		IntPtr_string_IntPtr_Delegate __FindClass;
		public IntPtr_string_IntPtr_Delegate _FindClass {
			get {
				if (__FindClass == null)
					__FindClass = (IntPtr_string_IntPtr_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv._FindClass, typeof (IntPtr_string_IntPtr_Delegate));
				return __FindClass;
			}
		}

		internal delegate JniGlobalReference IntPtr_IntPtr_IntPtr_Delegate (JniEnvironmentSafeHandle env, JniReferenceSafeHandle h);
		IntPtr_IntPtr_IntPtr_Delegate _NewGlobalRef;
		public IntPtr_IntPtr_IntPtr_Delegate NewGlobalRef {
			get {
				if (_NewGlobalRef == null)
					_NewGlobalRef = (IntPtr_IntPtr_IntPtr_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.NewGlobalRef, typeof (IntPtr_IntPtr_IntPtr_Delegate));
				return _NewGlobalRef;
			}
		}

		internal delegate void IntPtr_Gref_void_Delegate (JniEnvironmentSafeHandle env, JniGlobalReference h);
		IntPtr_Gref_void_Delegate _DeleteGlobalRef;
		public IntPtr_Gref_void_Delegate DeleteGlobalRef {
			get {
				if (_DeleteGlobalRef == null)
					_DeleteGlobalRef = (IntPtr_Gref_void_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.DeleteGlobalRef, typeof (IntPtr_Gref_void_Delegate));
				return _DeleteGlobalRef;
			}
		}

		internal delegate void IntPtr_Lref_void_Delegate (JniEnvironmentSafeHandle env, JniLocalReference h);
		IntPtr_Lref_void_Delegate _DeleteLocalRef;
		public IntPtr_Lref_void_Delegate DeleteLocalRef {
			get {
				if (_DeleteLocalRef == null)
					_DeleteLocalRef = (IntPtr_Lref_void_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.DeleteLocalRef, typeof (IntPtr_Lref_void_Delegate));
				return _DeleteLocalRef;
			}
		}

		internal delegate JniMethodID IntPtr_IntPtr_string_string_IntPtr_Delegate (JniEnvironmentSafeHandle env, JniReferenceSafeHandle @class, string name, string sig);
		IntPtr_IntPtr_string_string_IntPtr_Delegate _GetMethodID;
		public IntPtr_IntPtr_string_string_IntPtr_Delegate GetMethodID {
			get {
				if (_GetMethodID == null)
					_GetMethodID = (IntPtr_IntPtr_string_string_IntPtr_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.GetMethodID, typeof (IntPtr_IntPtr_string_string_IntPtr_Delegate));
				return _GetMethodID;
			}
		}

		internal delegate JniStaticMethodID IntPtr_IntPtr_string_string_JniStaticMethodID_Delegate (JniEnvironmentSafeHandle env, JniReferenceSafeHandle @class, string name, string sig);
		IntPtr_IntPtr_string_string_JniStaticMethodID_Delegate _GetStaticMethodID;
		public IntPtr_IntPtr_string_string_JniStaticMethodID_Delegate GetStaticMethodID {
			get {
				if (_GetStaticMethodID == null)
					_GetStaticMethodID = (IntPtr_IntPtr_string_string_JniStaticMethodID_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.GetStaticMethodID, typeof (IntPtr_IntPtr_string_string_JniStaticMethodID_Delegate));
				return _GetStaticMethodID;
			}
		}
	}
	#endif

	public sealed partial class JniEnvironment : IDisposable {

		JniEnvironment ()
		{
		}

		internal JniEnvironment (JniEnvironmentSafeHandle safeHandle, JavaVM javaVM)
		{
			Debug.WriteLine ("# JniEnvironment..ctor: begin!");
			SafeHandle = safeHandle;
			if (current == null)
				current = this;
			Debug.WriteLine ("# JniEnvironment..ctor: creating invoker");
			Invoker = SafeHandle.CreateInvoker ();
			JavaVM  = javaVM;
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
			if (Current == this)
				current = null;
			SafeHandle.Dispose ();
			SafeHandle = null;
		}

		public Exception GetExceptionForLastThrowable ()
		{
			return null;
		}

		#if false
		internal static unsafe JniNativeInterfaceInvoker CreateNativeInterface ()
		{
			JniNativeInterfaceStruct* p = (JniNativeInterfaceStruct*) Marshal.ReadIntPtr (Handle);
			return new JniNativeInterfaceInvoker (p);
		}
		#endif
	}
}

