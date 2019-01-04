using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Android.Runtime {

	struct JnienvInitializeArgs {
		public IntPtr          javaVm;
		public IntPtr          env;
		public IntPtr          grefLoader;
		public IntPtr          Loader_loadClass;
		public IntPtr          grefClass;
		public IntPtr          Class_forName;
		public uint            logCategories;
		public IntPtr          Class_getName;
		public int             version;
		public int             androidSdkVersion;
		public int             localRefsAreIndirect;
		public int             grefGcThreshold;
		public IntPtr          grefIGCUserPeer;
		public int             isRunningOnDesktop;
	}

	public static partial class JNIEnv {

		static IntPtr java_class_loader;
		static IntPtr java_vm;
		static IntPtr load_class_id;
		static IntPtr gref_class;
		static IntPtr mid_Class_forName;
		static int version;
		static int androidSdkVersion;

		static bool AllocObjectSupported;

		static IntPtr cid_System;
		static IntPtr mid_System_identityHashCode;

		internal static int    gref_gc_threshold;
		internal static IntPtr mid_Class_getName;
		
		internal  static  bool  PropagateExceptions;
		static UncaughtExceptionHandler defaultUncaughtExceptionHandler;

		internal static bool IsRunningOnDesktop;

		static AndroidRuntime androidRuntime;

#if !JAVA_INTEROP
		static JNIInvokeInterface invoke_iface;

		[ThreadStatic] static IntPtr handle;
		[ThreadStatic] static JniNativeInterfaceInvoker env;

		static JniNativeInterfaceInvoker Env {
			get { 
				if (Handle == IntPtr.Zero) // Forces thread attach if necessary.
					throw new Exception ("JNIEnv handle is NULL");
				return env;
			}
		}

		static void SetEnv ()
		{
			int r;
			if ((r = invoke_iface.GetEnv (java_vm, out handle, version)) != 0)
				AndroidEnvironment.FailFast ("Unable to get JNI Environment pointer! " +
						"GetEnv(vm=0x" + java_vm.ToString ("x") + ", version=0x" + version.ToString ("x") + ")=" + r +
						"; gettid()=" + gettid () +
						"; Thread.ManagedThreadId=" + Thread.CurrentThread.ManagedThreadId +
						"; Thread.Name=\"" + Thread.CurrentThread.Name + "\"" +
						"; at: " + new StackTrace (true).ToString ());
			env = CreateNativeInterface ();
		}
#endif  // !JAVA_INTEROP

		public static IntPtr Handle {
			get {
#if JAVA_INTEROP
				return JniEnvironment.EnvironmentPointer;
#else   // !JAVA_INTEROP
				if (handle == IntPtr.Zero) {
					SetEnv ();
				}
				return handle;
#endif  // !JAVA_INTEROP
			}
		}

		public static void CheckHandle (IntPtr jnienv)
		{
#if JAVA_INTEROP
			new JniTransition (jnienv).Dispose ();
#else   // !JAVA_INTEROP
			if (Handle != jnienv) {
				SetEnv ();
			}
#endif  // !JAVA_INTEROP
		}

		[DllImport ("libc")]
		static extern int gettid ();

		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			string typeName = new string ((char*) typeName_ptr, 0, typeName_len);

			var __start = new DateTime ();
			if (Logger.LogTiming) {
				__start = DateTime.UtcNow;
				Logger.Log (LogLevel.Info,
						"monodroid-timing",
						"JNIEnv.RegisterJniNatives (\"" + typeName + "\", 0x" + jniClass.ToString ("x") + ") start: " + (__start - new DateTime (1970, 1, 1)).TotalMilliseconds);
			}

			Type type = Type.GetType (typeName);
			if (type == null) {
				Logger.Log (LogLevel.Error, "MonoDroid",
						"Could not load type '" + typeName + "'. Skipping JNI registration of type '" + 
						Java.Interop.TypeManager.GetClassName (jniClass) + "'.");
				return;
			}

			var className = Java.Interop.TypeManager.GetClassName (jniClass);
			TypeManager.RegisterType (className, type);

			JniType jniType = null;
			JniType.GetCachedJniType (ref jniType, className);

			androidRuntime.TypeManager.RegisterNativeMembers (jniType, type, methods_ptr == IntPtr.Zero ? null : new string ((char*) methods_ptr, 0, methods_len));

			if (Logger.LogTiming) {
				var __end = DateTime.UtcNow;
				Logger.Log (LogLevel.Info,
						"monodroid-timing",
						"JNIEnv.RegisterJniNatives total time: " + (__end - new DateTime (1970, 1, 1)).TotalMilliseconds + " [elapsed: " + (__end - __start).TotalMilliseconds + " ms]");
			}
		}

		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			Logger.Categories = (LogCategories) args->logCategories;

			Stopwatch stopper = null;
			long elapsed, totalElapsed = 0;
			if (Logger.LogTiming) {
				stopper = new Stopwatch ();
				stopper.Start ();
				Logger.Log (LogLevel.Info, "monodroid-timing", "JNIEnv.Initialize start");
				elapsed = stopper.ElapsedMilliseconds;
				totalElapsed += elapsed;
				Logger.Log (LogLevel.Info, "monodroid-timing", $"JNIEnv.Initialize: Logger JIT/etc. time: elapsed {elapsed} ms]");
				stopper.Restart ();
			}

			gref_gc_threshold = args->grefGcThreshold;

			mid_Class_getName = args->Class_getName;

			java_vm = args->javaVm;

#if !JAVA_INTEROP
			handle = args->env;
			env = CreateNativeInterface ();

			invoke_iface = (JNIInvokeInterface) Marshal.PtrToStructure (Marshal.ReadIntPtr (java_vm), typeof (JNIInvokeInterface));
#endif  // !JAVA_INTEROP

			version = args->version;

			androidSdkVersion = args->androidSdkVersion;

			java_class_loader = args->grefLoader;
			load_class_id     = args->Loader_loadClass;
			gref_class        = args->grefClass;
			mid_Class_forName = args->Class_forName;

			if (args->localRefsAreIndirect == 1)
				IdentityHash = v => _monodroid_get_identity_hash_code (Handle, v);
			else
				IdentityHash = v => v;

#if JAVA_INTEROP
			androidRuntime = new AndroidRuntime (args->env, args->javaVm, androidSdkVersion > 10, args->grefLoader, args->Loader_loadClass);
#endif // JAVA_INTEROP

			if (Logger.LogTiming) {
				elapsed = stopper.ElapsedMilliseconds;
				totalElapsed += elapsed;
				Logger.Log (LogLevel.Info, "monodroid-timing", $"JNIEnv.Initialize: managed runtime init time: elapsed {elapsed} ms]");
				stopper.Restart ();
				var _ = Java.Interop.TypeManager.jniToManaged;
				elapsed = stopper.ElapsedMilliseconds;
				totalElapsed += elapsed;
				Logger.Log (LogLevel.Info, "monodroid-timing", $"JNIEnv.Initialize: TypeManager init time: elapsed {elapsed} ms]");
			}

			AllocObjectSupported = androidSdkVersion > 10;
			IsRunningOnDesktop = Convert.ToBoolean (args->isRunningOnDesktop);

			Java.Interop.Runtime.grefIGCUserPeer_class = args->grefIGCUserPeer;

#if BROKEN_EXCEPTION_TRANSITIONS	// XA < 5.0
			var propagate =
				Environment.GetEnvironmentVariable ("__XA_PROPAGATE_EXCEPTIONS__") ??
				Environment.GetEnvironmentVariable ("__XA_PROPOGATE_EXCEPTIONS__");
			if (!string.IsNullOrEmpty (propagate)) {
				bool.TryParse (propagate, out PropagateExceptions);
			}
			if (PropagateExceptions) {
				Logger.Log (LogLevel.Info,
						"monodroid",
						"Enabling managed-to-java exception propagation.");
			}
#else
			PropagateExceptions               = true;
			var   brokenExceptionTransitions  = Environment.GetEnvironmentVariable ("XA_BROKEN_EXCEPTION_TRANSITIONS");
			bool  brokenTransitions;
			if (!string.IsNullOrEmpty (brokenExceptionTransitions) && bool.TryParse (brokenExceptionTransitions, out brokenTransitions)) {
				PropagateExceptions = !brokenTransitions;
			}
#endif
			if (PropagateExceptions) {
				defaultUncaughtExceptionHandler = new UncaughtExceptionHandler (Java.Lang.Thread.DefaultUncaughtExceptionHandler);
				if (!IsRunningOnDesktop)
					Java.Lang.Thread.DefaultUncaughtExceptionHandler = defaultUncaughtExceptionHandler;
			}

			if (Logger.LogTiming) {
				totalElapsed += stopper.ElapsedMilliseconds;
				Logger.Log (LogLevel.Info, "monodroid-timing", $"JNIEnv.Initialize end: elapsed {totalElapsed} ms");
			}
		}

		internal static void Exit ()
		{
			/* Reset uncaught exception handler so that we don't mistakenly reuse a
			 * now-invalid handler the next time we reinitialize JNIEnv.
			 */
			var uncaughtExceptionHandler = Java.Lang.Thread.DefaultUncaughtExceptionHandler as UncaughtExceptionHandler;
			if (uncaughtExceptionHandler != null && uncaughtExceptionHandler == defaultUncaughtExceptionHandler)
				Java.Lang.Thread.DefaultUncaughtExceptionHandler = uncaughtExceptionHandler.DefaultHandler;

#if JAVA_INTEROP
			/* Manually dispose surfaced objects and close the current JniEnvironment to
			 * avoid ObjectDisposedException thrown on finalizer threads after shutdown
			 */
			foreach (var surfacedObject in Java.Interop.Runtime.GetSurfacedObjects ()) {
				try {
					var obj = surfacedObject.Target as IDisposable;
					if (obj != null)
						obj.Dispose ();
					continue;
				} catch (Exception e) {
					Logger.Log (LogLevel.Warn,
								"monodroid",
								string.Format ("Couldn't dispose object: {0}", e));
				}
				/* If calling Dispose failed, the assumption is that user-code in
				 * the Dispose(bool) overload is to blame for it. In that case we
				 * fallback to manual deletion of the surfaced object.
				 */
				var jobj = surfacedObject.Target as Java.Lang.Object;
				if (jobj != null)
					ManualJavaObjectDispose (jobj);
			}
			JniEnvironment.Runtime.Dispose ();
#endif // JAVA_INTEROP
		}

		/* FIXME: This reproduces the minimal steps in Java.Lang.Object.Dispose
		 * that needs to be executed so that we don't leak any GREF and prevent
		 * code execution into an appdomain that we are disposing via a finalizer.
		 * Ideally it should be done via another more generic mechanism, likely
		 * from the Java.Interop.Runtime API.
		 */
		static void ManualJavaObjectDispose (Java.Lang.Object obj)
		{
			var peer = obj.PeerReference;
			var handle = peer.Handle;
			var keyHandle = ((IJavaObjectEx)obj).KeyHandle;
			Java.Lang.Object.Dispose (obj, ref handle, keyHandle, (JObjectRefType)peer.Type);
			GC.SuppressFinalize (obj);
		}

		internal static void PropagateUncaughtException (IntPtr env, IntPtr javaThreadPtr, IntPtr javaExceptionPtr)
		{
			if (defaultUncaughtExceptionHandler == null)
				return;

			var javaThread = JavaObject.GetObject<Java.Lang.Thread> (env, javaThreadPtr, JniHandleOwnership.DoNotTransfer);
			var javaException = JavaObject.GetObject<Java.Lang.Throwable> (env, javaExceptionPtr, JniHandleOwnership.DoNotTransfer);

			defaultUncaughtExceptionHandler.UncaughtException (javaThread, javaException);
		}

		[DllImport ("__Internal")]
		extern static void _monodroid_gc_wait_for_bridge_processing ();

		static volatile bool BridgeProcessing; // = false

		public static void WaitForBridgeProcessing ()
		{
			if (!BridgeProcessing)
				return;
			_monodroid_gc_wait_for_bridge_processing ();
		}

		[DllImport ("__Internal")]
		extern static IntPtr _monodroid_get_identity_hash_code (IntPtr env, IntPtr value);

		internal static Func<IntPtr, IntPtr> IdentityHash;

		public static IntPtr AllocObject (string jniClassName)
		{
			IntPtr jniClass = JNIEnv.FindClass (jniClassName);
			try {
				return AllocObject (jniClass);
			}
			finally {
				JNIEnv.DeleteGlobalRef (jniClass);
			}
		}

		public static IntPtr AllocObject (Type type)
		{
			IntPtr jniClass = JNIEnv.FindClass (type);
			try {
				return AllocObject (jniClass);
			}
			finally {
				JNIEnv.DeleteGlobalRef (jniClass);
			}
		}

		public static unsafe IntPtr StartCreateInstance (IntPtr jclass, IntPtr constructorId, JValue* constructorParameters)
		{
			if (AllocObjectSupported) {
				return AllocObject (jclass);
			}
			return NewObject (jclass, constructorId, constructorParameters);
		}

		public static unsafe IntPtr StartCreateInstance (IntPtr jclass, IntPtr constructorId, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				return StartCreateInstance (jclass, constructorId, cp);
		}

		public static unsafe void FinishCreateInstance (IntPtr instance, IntPtr jclass, IntPtr constructorId, JValue* constructorParameters)
		{
			if (!AllocObjectSupported)
				return;
			CallNonvirtualVoidMethod (instance, jclass, constructorId, constructorParameters);
		}

		public static unsafe void FinishCreateInstance (IntPtr instance, IntPtr jclass, IntPtr constructorId, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				FinishCreateInstance (instance, jclass, constructorId, cp);
		}

		public static unsafe IntPtr StartCreateInstance (Type type, string jniCtorSignature, JValue* constructorParameters)
		{
			if (AllocObjectSupported) {
				return AllocObject (type);
			}
			return CreateInstance (type, jniCtorSignature, constructorParameters);
		}

		public static unsafe IntPtr StartCreateInstance (Type type, string jniCtorSignature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				return StartCreateInstance (type, jniCtorSignature, cp);
		}

		public static unsafe IntPtr StartCreateInstance (string jniClassName, string jniCtorSignature, JValue* constructorParameters)
		{
			if (AllocObjectSupported)
				return AllocObject (jniClassName);
			return CreateInstance (jniClassName, jniCtorSignature, constructorParameters);
		}

		public static unsafe IntPtr StartCreateInstance (string jniClassName, string jniCtorSignature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				return StartCreateInstance (jniClassName, jniCtorSignature, cp);
		}

		public static unsafe void FinishCreateInstance (IntPtr instance, string jniCtorSignature, JValue* constructorParameters)
		{
			if (!AllocObjectSupported)
				return;
			InvokeConstructor (instance, jniCtorSignature, constructorParameters);
		}

		public static unsafe void FinishCreateInstance (IntPtr instance, string jniCtorSignature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				FinishCreateInstance (instance, jniCtorSignature, cp);
		}

		public static unsafe void InvokeConstructor (IntPtr instance, string jniCtorSignature, JValue* constructorParameters)
		{
			IntPtr lrefClass = GetObjectClass (instance);
			try {
				IntPtr ctor = JNIEnv.GetMethodID (lrefClass, "<init>", jniCtorSignature);
				if (ctor == IntPtr.Zero)
					throw new ArgumentException (string.Format ("Could not find constructor JNI signature '{0}' on type '{1}'.",
								jniCtorSignature, Java.Interop.TypeManager.GetClassName (lrefClass)));
				CallNonvirtualVoidMethod (instance, lrefClass, ctor, constructorParameters);
			} finally {
				DeleteLocalRef (lrefClass);
			}
		}

		public static unsafe void InvokeConstructor (IntPtr instance, string jniCtorSignature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				InvokeConstructor (instance, jniCtorSignature, cp);
		}

		public static unsafe IntPtr CreateInstance (IntPtr jniClass, string signature, JValue* constructorParameters)
		{
			IntPtr ctor = JNIEnv.GetMethodID (jniClass, "<init>", signature);
			if (ctor == IntPtr.Zero)
				throw new ArgumentException (string.Format ("Could not find constructor JNI signature '{0}' on type '{1}'.",
							signature, Java.Interop.TypeManager.GetClassName (jniClass)));
			return JNIEnv.NewObject (jniClass, ctor, constructorParameters);
		}

		public static unsafe IntPtr CreateInstance (IntPtr jniClass, string signature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				return CreateInstance (jniClass, signature, cp);
		}

		public static unsafe IntPtr CreateInstance (string jniClassName, string signature, JValue* constructorParameters)
		{
			IntPtr jniClass = JNIEnv.FindClass (jniClassName);
			try {
				return CreateInstance (jniClass, signature, constructorParameters);
			}
			finally {
				JNIEnv.DeleteGlobalRef (jniClass);
			}
		}

		public static unsafe IntPtr CreateInstance (string jniClassName, string signature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				return CreateInstance (jniClassName, signature, cp);
		}

		public static unsafe IntPtr CreateInstance (Type type, string signature, JValue* constructorParameters)
		{
			IntPtr jniClass = JNIEnv.FindClass (type);
			try {
				return CreateInstance (jniClass, signature, constructorParameters);
			}
			finally {
				JNIEnv.DeleteGlobalRef (jniClass);
			}
		}

		public static unsafe IntPtr CreateInstance (Type type, string signature, params JValue[] constructorParameters)
		{
			fixed (JValue* cp = constructorParameters)
				return CreateInstance (type, signature, cp);
		}

#if !JAVA_INTEROP
		static unsafe JniNativeInterfaceInvoker CreateNativeInterface ()
		{
			JniNativeInterfaceStruct* p = (JniNativeInterfaceStruct*) Marshal.ReadIntPtr (Handle);
			return new JniNativeInterfaceInvoker (p);
		}
#endif  // !JAVA_INTEROP

		public static IntPtr FindClass (System.Type type)
		{
			int rank = JavaNativeTypeManager.GetArrayInfo (type, out type);
			try {
				return FindClass (JavaNativeTypeManager.ToJniName (GetJniName (type), rank));
			} catch (Java.Lang.Throwable e) {
				if (!((e is Java.Lang.NoClassDefFoundError) || (e is Java.Lang.ClassNotFoundException)))
					throw;
				Logger.Log (LogLevel.Warn, "monodroid", "JNIEnv.FindClass(Type) caught unexpected exception: " + e);
				string jni = Java.Interop.TypeManager.GetJniTypeName (type);
				if (jni != null) {
					e.Dispose ();
					return FindClass (JavaNativeTypeManager.ToJniName (jni, rank));
				}

				// Though it's tempting to call TypeManager.RegisterType() to avoid
				// calling GetCustomAttributes() again, this isn't necessary as
				// JNIEnv.FindClass() will invoke the static constructor for the type,
				// which will (indirectly) call TypeManager.RegisterType().
				jni = JavaNativeTypeManager.ToJniNameFromAttributes (type);
				if (jni != null) {
					e.Dispose ();
					return FindClass (JavaNativeTypeManager.ToJniName (jni, rank));
				}
				throw;
			}
		}

		static readonly int nameBufferLength = 1024;
		[ThreadStatic] static char[] nameBuffer;

		static unsafe IntPtr BinaryName (string classname)
		{
			int index = classname.IndexOf ('/');

			if (index == -1)
				return NewString (classname);

			int length = classname.Length;
			if (length > nameBufferLength)
				return NewString (classname.Replace ('/', '.'));

			if (nameBuffer == null)
				nameBuffer = new char[nameBufferLength];

			fixed (char* src = classname, dst = nameBuffer) {
				char* src_ptr = src;
				char* dst_ptr = dst;
				char* end_ptr = src + length;
				while (src_ptr < end_ptr) {
					*dst_ptr = (*src_ptr == '/') ? '.' : *src_ptr;
					src_ptr++;
					dst_ptr++;
				}
			}
			return NewString (nameBuffer, length);
		}

		public static IntPtr FindClass (string classname)
		{
			IntPtr local_ref;

			IntPtr native_str = BinaryName (classname);
			try {
				local_ref = CallStaticObjectMethod (gref_class, mid_Class_forName, new JValue (native_str), new JValue (true), new JValue (java_class_loader));
			} finally {
				DeleteLocalRef (native_str);
			}

			IntPtr global_ref = NewGlobalRef (local_ref);
			DeleteLocalRef (local_ref);
			return global_ref;
		}

		public static IntPtr FindClass (string className, ref IntPtr cachedJniClassHandle)
		{
			if (cachedJniClassHandle != IntPtr.Zero)
				return cachedJniClassHandle;
			IntPtr h = FindClass (className);
			if (Interlocked.CompareExchange (ref cachedJniClassHandle, h, IntPtr.Zero) != IntPtr.Zero)
				DeleteGlobalRef (h);
			return cachedJniClassHandle;
		}

		public static void Throw (IntPtr obj)
		{
			if (obj == IntPtr.Zero)
				throw new ArgumentException ("'obj' must not be IntPtr.Zero.", "obj");
#if JAVA_INTEROP
			JniEnvironment.Exceptions.Throw (new JniObjectReference (obj));
#else   // !JAVA_INTEROP
			if (Env.Throw (Handle, obj) != 0) {
				ExceptionDescribe ();
				AndroidEnvironment.FailFast ("Unable to raise a Java exception!");
			}
#endif  // !JAVA_INTEROP
		}

		public static void ThrowNew (IntPtr clazz, string message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
#if JAVA_INTEROP
			JniEnvironment.Exceptions.ThrowNew (new JniObjectReference (clazz), message);
#else   // !JAVA_INTEROP
			if (Env.ThrowNew (Handle, clazz, message) != 0) {
				ExceptionDescribe ();
				AndroidEnvironment.FailFast ("Unable to raise a Java exception!");
			}
#endif  // !JAVA_INTEROP
		}

		public static void PushLocalFrame (int capacity)
		{
#if JAVA_INTEROP
			JniEnvironment.References.PushLocalFrame (capacity);
#else   // !JAVA_INTEROP
			int rvalue = Env._PushLocalFrame (Handle, capacity);

			if (rvalue != 0) {
				Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
				if (e != null)
					ExceptionDispatchInfo.Capture (e).Throw ();
			}
#endif  // !JAVA_INTEROP
		}

		public static void EnsureLocalCapacity (int capacity)
		{
#if JAVA_INTEROP
			JniEnvironment.References.EnsureLocalCapacity (capacity);
#else   // !JAVA_INTEROP
			int rvalue = Env._EnsureLocalCapacity (Handle, capacity);

			if (rvalue != 0) {
				Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
				if (e != null)
					ExceptionDispatchInfo.Capture (e).Throw ();
			}
#endif  // !JAVA_INTEROP
		}

		internal static void DeleteRef (IntPtr handle, JniHandleOwnership transfer)
		{
			switch (transfer) {
			case JniHandleOwnership.DoNotTransfer:
				break;
			case JniHandleOwnership.TransferLocalRef:
				JNIEnv.DeleteLocalRef (handle);
				break;
			case JniHandleOwnership.TransferGlobalRef:
				JNIEnv.DeleteGlobalRef (handle);
				break;
			}
		}

		[DllImport ("__Internal")]
		internal static extern int _monodroid_gref_log (string message);

		[DllImport ("__Internal")]
		internal static extern int _monodroid_gref_log_new (IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string threadName, int threadId, [In] StringBuilder from, int from_writable);

		[DllImport ("__Internal")]
		internal static extern void _monodroid_gref_log_delete (IntPtr handle, byte type, string threadName, int threadId, [In] StringBuilder from, int from_writable);

		[DllImport ("__Internal")]
		internal static extern void _monodroid_weak_gref_new (IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string threadName, int threadId, [In] StringBuilder from, int from_writable);

		[DllImport ("__Internal")]
		internal static extern void _monodroid_weak_gref_delete (IntPtr handle, byte type, string threadName, int threadId, [In] StringBuilder from, int from_writable);

		[DllImport ("__Internal")]
		internal static extern int _monodroid_lref_log_new (int lrefc, IntPtr handle, byte type, string threadName, int threadId, [In] StringBuilder from, int from_writable);

		[DllImport ("__Internal")]
		internal static extern void _monodroid_lref_log_delete (int lrefc, IntPtr handle, byte type, string threadName, int threadId, [In] StringBuilder from, int from_writable);

		public static IntPtr NewGlobalRef (IntPtr jobject)
		{
#if JAVA_INTEROP
			var r = new JniObjectReference (jobject);
			return r.NewGlobalRef ().Handle;
#else   // !JAVA_INTEROP
			IntPtr res = Env.NewGlobalRef (Handle, jobject);
			var log		= Logger.LogGlobalRef;
			var ctype	= log ? _GetObjectRefType (jobject) : (byte) '*';
			var ntype	= log ? _GetObjectRefType (res) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			int gc 		= _monodroid_gref_log_new (jobject, ctype, res, ntype, tname, tid, from, 1);
			if (gc >= gref_gc_threshold) {
				Logger.Log (LogLevel.Info, "monodroid-gc", gc + " outstanding GREFs. Performing a full GC!");
				System.GC.Collect ();
			}
			return res;
#endif  // !JAVA_INTEROP
		}

		public static void DeleteGlobalRef (IntPtr jobject)
		{
#if JAVA_INTEROP
			var r = new JniObjectReference (jobject, JniObjectReferenceType.Global);
			JniObjectReference.Dispose (ref r);
#else   // !JAVA_INTEROP
			var log		= Logger.LogGlobalRef;
			var ctype	= log ? _GetObjectRefType (jobject) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			_monodroid_gref_log_delete (jobject, ctype, tname, tid, from, 1);
			Env.DeleteGlobalRef (Handle, jobject);
#endif  // !JAVA_INTEROP
		}

#if !JAVA_INTEROP
		internal static int lref_count;

		static IntPtr LogCreateLocalRef (IntPtr jobject)
		{
			if (jobject == IntPtr.Zero)
				return jobject;

			if (Logger.LogLocalRef) {
				var v = Interlocked.Increment (ref lref_count);

				var tname = Thread.CurrentThread.Name;
				var tid   = Thread.CurrentThread.ManagedThreadId;;
				var from  = new StringBuilder (new StackTrace (true).ToString ());
				_monodroid_lref_log_new (v, jobject, (byte) 'L', tname, tid, from, 1);
			}
			return jobject;
		}
#endif  // !JAVA_INTEROP

		public static IntPtr NewLocalRef (IntPtr jobject)
		{
#if JAVA_INTEROP
			return new JniObjectReference (jobject).NewLocalRef ().Handle;
#else   // !JAVA_INTEROP
			return LogCreateLocalRef (Env.NewLocalRef (Handle, jobject));
#endif  // !JAVA_INTEROP
		}

		public static void DeleteLocalRef (IntPtr jobject)
		{
#if JAVA_INTEROP
			var r = new JniObjectReference (jobject, JniObjectReferenceType.Local);
			JniObjectReference.Dispose (ref r);
#else   // !JAVA_INTEROP
			Env.DeleteLocalRef (Handle, jobject);

			if (jobject == IntPtr.Zero)
				return;

			if (Logger.LogLocalRef) {
				var v = Interlocked.Decrement (ref lref_count);

				var tname = Thread.CurrentThread.Name;
				var tid   = Thread.CurrentThread.ManagedThreadId;;
				var from  = new StringBuilder (new StackTrace (true).ToString ());
				_monodroid_lref_log_delete (v, jobject, (byte) 'L', tname, tid, from, 1);
			}
#endif  // !JAVA_INTEROP
		}

		public static void DeleteWeakGlobalRef (IntPtr jobject)
		{
#if JAVA_INTEROP
			var r = new JniObjectReference (jobject, JniObjectReferenceType.WeakGlobal);
			JniObjectReference.Dispose (ref r);
#else   // !JAVA_INTEROP
			var log		= Logger.LogGlobalRef;
			var ctype	= log ? _GetObjectRefType (jobject) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			_monodroid_weak_gref_delete (jobject, ctype, tname, tid, from, 1);
			Env.DeleteWeakGlobalRef (Handle, jobject);
#endif  // !JAVA_INTEROP
		}

		public static IntPtr NewObject (IntPtr jclass, IntPtr jmethod)
		{
#if JAVA_INTEROP
			var r = JniEnvironment.Object.NewObject (new JniObjectReference (jclass), new JniMethodInfo (jmethod, isStatic: false));
			return r.Handle;
#else   // !JAVA_INTEROP
			Java.Interop.TypeManager.ActivationEnabled = false;
			IntPtr rvalue = Env.NewObject (Handle, jclass, jmethod);
			Java.Interop.TypeManager.ActivationEnabled = true;

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();

			return LogCreateLocalRef (rvalue);
#endif  // !JAVA_INTEROP
		}

		public static unsafe IntPtr NewObject (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
#if JAVA_INTEROP
			var r = JniEnvironment.Object.NewObject (new JniObjectReference (jclass), new JniMethodInfo (jmethod, isStatic: false), (JniArgumentValue*) parms);
			return r.Handle;
#else   // !JAVA_INTEROP
			Java.Interop.TypeManager.ActivationEnabled = false;
			IntPtr rvalue = Env.NewObjectA (Handle, jclass, jmethod, parms);
			Java.Interop.TypeManager.ActivationEnabled = true;

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();

			return LogCreateLocalRef (rvalue);
#endif  // !JAVA_INTEROP
		}

		public static unsafe IntPtr NewObject (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			fixed (JValue* p = parms)
				return NewObject (jclass, jmethod, p);
		}

		public static string GetClassNameFromInstance (IntPtr jobject)
		{
			IntPtr jclass = GetObjectClass (jobject);
			try {
				return Java.Interop.TypeManager.GetClassName (jclass);
			} finally {
				DeleteLocalRef (jclass);
			}
		}

		[DllImport ("__Internal")]
		internal static extern IntPtr monodroid_typemap_managed_to_java (string managed);

		public static string GetJniName (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			var java  = monodroid_typemap_managed_to_java (type.FullName + ", " + type.Assembly.GetName ().Name);
			return java == IntPtr.Zero
				? JavaNativeTypeManager.ToJniName (type)
				: Marshal.PtrToStringAnsi (java);
		}

		public static IntPtr ToJniHandle (IJavaObject value)
		{
			if (value == null)
				return IntPtr.Zero;
			return value.Handle;
		}

		public static IntPtr ToLocalJniHandle (IJavaObject value)
		{
			if (value == null)
				return IntPtr.Zero;
			var ex = value as IJavaObjectEx;
			if (ex != null)
				return ex.ToLocalJniHandle ();
			return NewLocalRef (value.Handle);
		}

#if !JAVA_INTEROP
		static JObjectRefType GetObjectRefType (IntPtr jobject)
		{
			return (JObjectRefType) Env.GetObjectRefType (Handle, jobject);
		}

		static byte _GetObjectRefType (IntPtr jobject)
		{
			var value = GetObjectRefType (jobject);
			switch (value) {
				case JObjectRefType.Invalid:	    return (byte) 'I';
				case JObjectRefType.Local:        return (byte) 'L';
				case JObjectRefType.Global:       return (byte) 'G';
				case JObjectRefType.WeakGlobal:   return (byte) 'W';
				default:                          return (byte) '*';
			}
		}
#endif  // !JAVA_INTEROP

		static IntPtr char_sequence_to_string_id;

		public static string GetCharSequence (IntPtr jobject, JniHandleOwnership transfer)
		{
			if (jobject == IntPtr.Zero)
				return null;
#if JAVA_INTEROP
			var r = JniEnvironment.Object.ToString (new JniObjectReference (jobject));
			return JniEnvironment.Strings.ToString (ref r, JniObjectReferenceOptions.CopyAndDispose);
#else   // !JAVA_INTEROP
			IntPtr str = LogCreateLocalRef (Env.CallObjectMethod (Handle, jobject, Java.Lang.Class.CharSequence_toString)); 
			try {
				return GetString (str, JniHandleOwnership.TransferLocalRef);
			} finally {
				DeleteRef (jobject, transfer);
			}
#endif  // !JAVA_INTEROP
		}

		public static unsafe string GetString (IntPtr value, JniHandleOwnership transfer)
		{
			if (value == IntPtr.Zero)
				return null;
#if JAVA_INTEROP
			var s = JniEnvironment.Strings.ToString (new JniObjectReference (value));
			DeleteRef (value, transfer);
			return s;
#else   // !JAVA_INTEROP
			int len = Env.GetStringLength (Handle, value);
			IntPtr chars = Env.GetStringChars (Handle, value, IntPtr.Zero);
			try {
				return new string ((char*) chars, 0, len);
			} finally {
				Env.ReleaseStringChars (Handle, value, chars);
				DeleteRef (value, transfer);
			}
#endif  // !JAVA_INTEROP
		}

		public static unsafe IntPtr NewString (string text)
		{
			if (text == null)
				return IntPtr.Zero;

#if JAVA_INTEROP
			return JniEnvironment.Strings.NewString (text).Handle;
#else   // !JAVA_INTEROP
			IntPtr rvalue;
			fixed (char *s = text)
				rvalue = LogCreateLocalRef (Env.NewString (Handle, (IntPtr) s, text.Length));

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();

			return rvalue;
#endif  // !JAVA_INTEROP
		}

		public static unsafe IntPtr NewString (char[] text, int length)
		{
			if (text == null)
				return IntPtr.Zero;

#if JAVA_INTEROP
			fixed (char *s = text)
				return JniEnvironment.Strings.NewString (s, length).Handle;
#else   // !JAVA_INTEROP
			IntPtr rvalue;
			fixed (char *s = text)
				rvalue = LogCreateLocalRef (Env.NewString (Handle, (IntPtr) s, length));

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();

			return rvalue;
#endif  // !JAVA_INTEROP
		}

		static void AssertCompatibleArrayTypes (Type sourceType, IntPtr destArray)
		{
			IntPtr grefSource = FindClass (sourceType);
			IntPtr lrefDest   = GetObjectClass (destArray);
			try {
				if (!IsAssignableFrom (grefSource, lrefDest)) {
					throw new InvalidCastException (string.Format ("Unable to cast from '{0}' to '{1}'.",
								Java.Interop.TypeManager.GetClassName (grefSource),
								Java.Interop.TypeManager.GetClassName (lrefDest)));
				}
			} finally {
				DeleteGlobalRef (grefSource);
				DeleteLocalRef (lrefDest);
			}
		}

		static void AssertCompatibleArrayTypes (IntPtr sourceArray, Type destType)
		{
			IntPtr grefDest   = FindClass (destType);
			IntPtr lrefSource = GetObjectClass (sourceArray);
			try {
				if (!IsAssignableFrom (lrefSource, grefDest)) {
					throw new InvalidCastException (string.Format ("Unable to cast from '{0}' to '{1}'.",
								Java.Interop.TypeManager.GetClassName (lrefSource),
								Java.Interop.TypeManager.GetClassName (grefDest)));
				}
			} finally {
				DeleteGlobalRef (grefDest);
				DeleteLocalRef (lrefSource);
			}
		}

		public static void CopyArray (IntPtr src, bool[] dest)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			AssertCompatibleArrayTypes (src, typeof (bool[]));

			_GetBooleanArrayRegion (src, 0, dest.Length, dest);
		}

		public static void CopyArray (IntPtr src, string[] dest)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			for (int i = 0; i < dest.Length; i++)
				dest [i] = GetString (GetObjectArrayElement (src, i), JniHandleOwnership.TransferLocalRef);
		}

		static Dictionary<Type, Func<Type, IntPtr, int, object>> nativeArrayElementToManaged;
		static Dictionary<Type, Func<Type, IntPtr, int, object>> NativeArrayElementToManaged {
			get {
				if (nativeArrayElementToManaged != null)
					return nativeArrayElementToManaged;

				var newValue = CreateNativeArrayElementToManaged ();
				Interlocked.CompareExchange (ref nativeArrayElementToManaged, newValue, null);
				return nativeArrayElementToManaged;
			}
		}

		static Dictionary<Type, Func<Type, IntPtr, int, object>> CreateNativeArrayElementToManaged ()
		{
			return new Dictionary<Type, Func<Type, IntPtr, int, object>> () {
				{ typeof (bool), (type, source, index) => {
					var r = new bool [1];
					_GetBooleanArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (byte), (type, source, index) => {
					var r = new byte [1];
					_GetByteArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (char), (type, source, index) => {
					var r = new char [1];
					_GetCharArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (short), (type, source, index) => {
					var r = new short [1];
					_GetShortArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (int), (type, source, index) => {
					var r = new int [1];
					_GetIntArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (long), (type, source, index) => {
					var r = new long [1];
					_GetLongArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (float), (type, source, index) => {
					var r = new float [1];
					_GetFloatArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (double), (type, source, index) => {
					var r = new double [1];
					_GetDoubleArrayRegion (source, index, 1, r);
					return r [0];
				} },
				{ typeof (string), (type, source, index) => {
					IntPtr elem = GetObjectArrayElement (source, index);
					if (type == typeof (Java.Lang.String))
						return new Java.Lang.String (elem, JniHandleOwnership.TransferLocalRef);
					return GetString (elem, JniHandleOwnership.TransferLocalRef);
				} },
				{ typeof (IJavaObject), (type, source, index) => {
					AssertIsJavaObject (type);

					IntPtr elem = GetObjectArrayElement (source, index);
					return Java.Lang.Object.GetObject (elem, JniHandleOwnership.TransferLocalRef, type);
				} },
				{ typeof (Array), (type, source, index) => {
					IntPtr  elem      = GetObjectArrayElement (source, index);
					return GetArray (elem, JniHandleOwnership.TransferLocalRef, type);
				} },
			};
		}

		static TValue GetConverter<TValue>(Dictionary<Type, TValue> dict, Type elementType, IntPtr array)
		{
			TValue converter;

			if (elementType != null) {
				if (elementType.IsEnum)
					elementType = Enum.GetUnderlyingType (elementType);
				if (dict.TryGetValue (elementType, out converter))
					return converter;
			}

			if (array != IntPtr.Zero) {
				string type = GetClassNameFromInstance (array);
				if (type == null || type.Length < 1 || type [0] != '[')
					throw new InvalidOperationException ("Unsupported java array type: " + type);

				switch (type [1]) {
				case 'B': return dict [typeof (byte)];
				case 'C': return dict [typeof (char)];
				case 'D': return dict [typeof (double)];
				case 'F': return dict [typeof (float)];
				case 'I': return dict [typeof (int)];
				case 'J': return dict [typeof (long)];
				case 'S': return dict [typeof (short)];
				case 'Z': return dict [typeof (bool)];
				case '[':
					if (elementType == null || elementType.IsArray)
						return dict [typeof (Array)];
					break;
				}

				if (type == "[Ljava/lang/String;")
					return dict [typeof (string)];
			}

			if (elementType != null && elementType.IsArray)
				return dict [typeof (Array)];

			AssertIsJavaObject (elementType);
			return dict [typeof (IJavaObject)];
		}

		static unsafe void _GetBooleanArrayRegion (IntPtr array, int start, int length, bool[] buffer)
		{
#if JAVA_INTEROP
			fixed (bool* p = buffer)
				JniEnvironment.Arrays.GetBooleanArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			byte[] b = new byte [buffer.Length];
			Env.GetBooleanArrayRegion (Handle, array, start, length, b);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();

			for (int i = 0; i < buffer.Length; ++i)
				buffer [i] = b [i] != 0;
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetByteArrayRegion (IntPtr array, int start, int length, byte[] buffer)
		{
#if JAVA_INTEROP
			fixed (byte* p = buffer)
				JniEnvironment.Arrays.GetByteArrayRegion (new JniObjectReference (array), start, length, (sbyte*) p);
#else   // !JAVA_INTEROP
			Env.GetByteArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetCharArrayRegion (IntPtr array, int start, int length, char[] buffer)
		{
#if JAVA_INTEROP
			fixed (char* p = buffer)
				JniEnvironment.Arrays.GetCharArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.GetCharArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetShortArrayRegion (IntPtr array, int start, int length, short[] buffer)
		{
#if JAVA_INTEROP
			fixed (short* p = buffer)
				JniEnvironment.Arrays.GetShortArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.GetShortArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetIntArrayRegion (IntPtr array, int start, int length, int[] buffer)
		{
#if JAVA_INTEROP
			fixed (int* p = buffer)
				JniEnvironment.Arrays.GetIntArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.GetIntArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetLongArrayRegion (IntPtr array, int start, int length, long[] buffer)
		{
#if JAVA_INTEROP
			fixed (long* p = buffer)
				JniEnvironment.Arrays.GetLongArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.GetLongArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetFloatArrayRegion (IntPtr array, int start, int length, float[] buffer)
		{
#if JAVA_INTEROP
			fixed (float* p = buffer)
				JniEnvironment.Arrays.GetFloatArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.GetFloatArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _GetDoubleArrayRegion (IntPtr array, int start, int length, double[] buffer)
		{
#if JAVA_INTEROP
			fixed (double* p = buffer)
				JniEnvironment.Arrays.GetDoubleArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.GetDoubleArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		public static void CopyArray (IntPtr src, Array dest, Type elementType = null)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			if (elementType != null && elementType.IsValueType)
				AssertCompatibleArrayTypes (src, elementType.MakeArrayType ());

			if (elementType != null && elementType.IsArray) {
				for (int i = 0; i < dest.Length; ++i) {
					IntPtr a = GetObjectArrayElement (src, i);
					try {
						Array d = (Array) dest.GetValue (i);
						if (d == null)
							dest.SetValue (GetArray (a, JniHandleOwnership.DoNotTransfer, elementType.GetElementType ()), i);
						else
							CopyArray (a, d, elementType.GetElementType ());
					} finally {
						DeleteLocalRef (a);
					}
				}
				return;
			}

			Func<Type, IntPtr, int, object> converter = GetConverter (NativeArrayElementToManaged, elementType, src);

			for (int i = 0; i < dest.Length; i++)
				dest.SetValue (converter (elementType, src, i), i);
		}

		static void AssertIsJavaObject (Type targetType)
		{
			if (targetType != null && !typeof (IJavaObject).IsAssignableFrom (targetType))
				throw new NotSupportedException ("Don't know how to convert type '" + targetType.FullName + "' to an Android.Runtime.IJavaObject.");
		}

		public static void CopyArray<T> (IntPtr src, T[] dest)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			if (typeof (T).IsValueType)
				AssertCompatibleArrayTypes (src, typeof (T[]));

			if (typeof (T).IsArray) {
				CopyArray (src, dest, typeof (T));
				return;
			}

			Func<Type, IntPtr, int, object> converter = GetConverter (NativeArrayElementToManaged, typeof (T), src);

			for (int i = 0; i < dest.Length; i++)
				dest [i] = (T) converter (typeof (T), src, i);
		}

		public static unsafe void CopyArray (bool[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			AssertCompatibleArrayTypes (typeof (bool[]), dest);

#if JAVA_INTEROP
			fixed (bool* p = src)
				JniEnvironment.Arrays.SetBooleanArrayRegion (new JniObjectReference (dest), 0, src.Length, p);
#else   // !JAVA_INTEROP
			byte[] bytes = new byte [src.Length];
			for (int i = 0; i < src.Length; i++)
				bytes [i] = (byte) (src [i] ? 1 : 0);
			SetBooleanArrayRegion (dest, 0, src.Length, bytes);
#endif  // !JAVA_INTEROP
		}

		public static void CopyArray (string[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			for (int i = 0; i < src.Length; i++) {
				IntPtr native = NewString (src [i]);
#if JAVA_INTEROP
				JniEnvironment.Arrays.SetObjectArrayElement (new JniObjectReference (dest), i, new JniObjectReference (native));
#else   // !JAVA_INTEROP
				SetObjectArrayElement (dest, i, native);
#endif  // !JAVA_INTEROP
				DeleteLocalRef (native);
			}
		}

		public static void CopyArray (IJavaObject[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			for (int i = 0; i < src.Length; i++) {
				IJavaObject o = src [i];
#if JAVA_INTEROP
				JniEnvironment.Arrays.SetObjectArrayElement (new JniObjectReference (dest), i, new JniObjectReference (o == null ? IntPtr.Zero : o.Handle));
#else   // !JAVA_INTEROP
				SetObjectArrayElement (dest, i, o == null ? IntPtr.Zero : o.Handle);
#endif  // !JAVA_INTEROP
			}
		}

		static Dictionary<Type, Action<Array, IntPtr>> copyManagedToNativeArray;
		static Dictionary<Type, Action<Array, IntPtr>> CopyManagedToNativeArray {
			get {
				if (copyManagedToNativeArray != null)
					return copyManagedToNativeArray;

				var newValue = CreateCopyManagedToNativeArray ();
				Interlocked.CompareExchange (ref copyManagedToNativeArray, newValue, null);
				return copyManagedToNativeArray;
			}
		}

		static Dictionary<Type, Action<Array, IntPtr>> CreateCopyManagedToNativeArray ()
		{
			return new Dictionary<Type, Action<Array, IntPtr>> () {
				{ typeof (bool),        (source, dest) => CopyArray ((bool[]) source, dest) },
				{ typeof (byte),        (source, dest) => CopyArray ((byte[]) source, dest) },
				{ typeof (char),        (source, dest) => CopyArray ((char[]) source, dest) },
				{ typeof (short),       (source, dest) => CopyArray ((short[]) source, dest) },
				{ typeof (int),         (source, dest) => CopyArray ((int[]) source, dest) },
				{ typeof (long),        (source, dest) => CopyArray ((long[]) source, dest) },
				{ typeof (float),       (source, dest) => CopyArray ((float[]) source, dest) },
				{ typeof (double),      (source, dest) => CopyArray ((double[]) source, dest) },
				{ typeof (string),      (source, dest) => {
					var s = source as string[];
					if (s != null) {
						CopyArray (s, dest);
						return;
					}
					var ijo = source as IJavaObject[];
					if (ijo != null) {
						CopyArray (ijo, dest);
						return;
					}
					throw new NotSupportedException ("Don't know how to copy '" +
							source.GetType ().FullName + "' to '" +
							GetClassNameFromInstance (dest) +
							"'.");
				} },
				{ typeof (IJavaObject), (source, dest) => CopyArray ((IJavaObject[]) source, dest) },
				{ typeof (Array),       (source, dest) => {
					int len = source.GetLength (0);
					for (int i = 0; i < len; ++i) {
						IntPtr _dest    = GetObjectArrayElement (dest, i);
						Array  _source  = (Array) source.GetValue (i);
						CopyArray (_source, _source.GetType ().GetElementType (), _dest);
						JNIEnv.DeleteLocalRef (_dest);
					}
				} },
			};
		}

		public static void CopyArray (Array source, Type elementType, IntPtr dest)
		{
			if (source == null)
				throw new ArgumentNullException ("source");
			if (elementType == null)
				throw new ArgumentNullException ("elementType");

			if (elementType.IsValueType)
				AssertCompatibleArrayTypes (elementType.MakeArrayType (), dest);

			Action<Array, IntPtr> converter = GetConverter (CopyManagedToNativeArray, elementType, dest);

			converter (source, dest);
		}

		public static void CopyArray<T> (T[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			CopyArray (src, typeof (T), dest);
		}

		public static Array GetArray (IntPtr array_ptr, JniHandleOwnership transfer, Type element_type = null)
		{
			try {
				return _GetArray (array_ptr, element_type);
			}
			finally {
				DeleteRef (array_ptr, transfer);
			}
		}

		static Dictionary<Type, Func<Type, IntPtr, int, Array>> nativeArrayToManaged;
		static Dictionary<Type, Func<Type, IntPtr, int, Array>> NativeArrayToManaged {
			get {
				if (nativeArrayToManaged != null)
					return nativeArrayToManaged;

				var newValue = CreateNativeArrayToManaged ();
				Interlocked.CompareExchange (ref nativeArrayToManaged, newValue, null);
				return nativeArrayToManaged;
			}
		}

		static Dictionary<Type, Func<Type, IntPtr, int, Array>> CreateNativeArrayToManaged ()
		{
			return new Dictionary<Type, Func<Type, IntPtr, int, Array>> () {
				{ typeof (bool), (type, source, len) => {
					var r = new bool [len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (byte), (type, source, len) => {
					var r = new byte[len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (char), (type, source, len) => {
					var r = new char [len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (short), (type, source, len) => {
					var r = new short [len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (int), (type, source, len) => {
					var r = new int[len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (long), (type, source, len) => {
					var r = new long[len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (float), (type, source, len) => {
					var r = new float[len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (double), (type, source, len) => {
					var r = new double [len];
					CopyArray (source, r);
					return r;
				} },
				{ typeof (string), (type, source, len) => {
					if (type != null && typeof (Java.Lang.Object).IsAssignableFrom (type)) {
						var r = new Java.Lang.String [len];
						CopyArray (source, r);
						return r;
					} else {
						var r = new string [len];
						CopyArray (source, r);
						return r;
					}
				} },
				{ typeof (IJavaObject), (type, source, len) => {
					var r = Array.CreateInstance (type, len);
					CopyArray (source, r, type);
					return r;
				} },
				{ typeof (Array), (type, source, len) => {
					var r = Array.CreateInstance (type, len);
					CopyArray (source, r, type);
					return r;
				} },
			};
		}

		static Array _GetArray (IntPtr array_ptr, Type element_type)
		{
			if (array_ptr == IntPtr.Zero)
				return null;

			if (element_type != null && element_type.IsValueType)
				AssertCompatibleArrayTypes (array_ptr, element_type.MakeArrayType ());

			int cnt = _GetArrayLength (array_ptr);

			Func<Type, IntPtr, int, Array> converter = GetConverter (NativeArrayToManaged, element_type, array_ptr);

			return converter (element_type, array_ptr, cnt);
		}

		static int _GetArrayLength (IntPtr array_ptr)
		{
#if JAVA_INTEROP
			return JniEnvironment.Arrays.GetArrayLength (new JniObjectReference (array_ptr));
#else   // !JAVA_INTEROP
			return Env.GetArrayLength (Handle, array_ptr);
#endif  // !JAVA_INTEROP
		}

		public static object[] GetObjectArray (IntPtr array_ptr, Type[] element_types)
		{
			if (array_ptr == IntPtr.Zero)
				return null;

			int cnt = _GetArrayLength (array_ptr);

			Func<Type, IntPtr, int, object> converter = GetConverter (NativeArrayElementToManaged, null, array_ptr);

			object[] ret = new object [cnt];

			for (int i = 0; i < cnt; i++) {
				Type targetType	= (element_types != null && i < element_types.Length) ? element_types [i] : null;
				object value    = converter ((targetType == null || targetType.IsValueType) ? null : targetType,
						array_ptr, i);

				ret [i] = value;
				ret [i] = targetType == null || targetType.IsInstanceOfType (value)
					? value
					: Convert.ChangeType (value, targetType);
			}

			return ret;
		}

		public static T[] GetArray<T> (IntPtr array_ptr)
		{
			if (array_ptr == IntPtr.Zero)
				return null;

			if (typeof (T).IsValueType)
				AssertCompatibleArrayTypes (array_ptr, typeof (T[]));

			int cnt = _GetArrayLength (array_ptr);
			T[] ret = new T [cnt];
			CopyArray<T> (array_ptr, ret);
			return ret;
		}

		public static T[] GetArray<T> (Java.Lang.Object[] array)
		{
			if (array == null)
				return null;
			T[] ret = new T [array.Length];
			for (int i = 0; i < array.Length; i++)
				ret [i] = JavaConvert.FromJavaObject<T> (array [i]);
			return ret;
		}

		public static T GetArrayItem<T> (IntPtr array_ptr, int index)
		{
			if (array_ptr == IntPtr.Zero)
				throw new ArgumentException ("array_ptr");
			if (index < 0 || index >= GetArrayLength (array_ptr))
				throw new ArgumentOutOfRangeException ("index");

			Func<Type, IntPtr, int, object> converter = GetConverter (NativeArrayElementToManaged, typeof (T), array_ptr);

			return (T) converter (typeof (T), array_ptr, index);
		}

		public static int GetArrayLength (IntPtr array_ptr)
		{
			if (array_ptr == IntPtr.Zero)
				return 0;
			return _GetArrayLength (array_ptr);
		}

		public static unsafe IntPtr NewArray (bool[] array)
		{
			if (array == null)
				return IntPtr.Zero;
			IntPtr result;
#if JAVA_INTEROP
			var r   = JniEnvironment.Arrays.NewBooleanArray (array.Length);
			fixed (bool* p = array)
				JniEnvironment.Arrays.SetBooleanArrayRegion (r, 0, array.Length, p);
			result  = r.Handle;
#else   // !JAVA_INTEROP
			result  = LogCreateLocalRef (Env.NewBooleanArray (Handle, array.Length));
			byte[] bytes = new byte [array.Length];
			for (int i = 0; i < array.Length; i++)
				bytes [i] = (byte) (array [i] ? 1 : 0);
			SetBooleanArrayRegion (result, 0, array.Length, bytes);
#endif  // !JAVA_INTEROP

			return result;
		}

		public static IntPtr NewArray (string[] array)
		{
			if (array == null)
				return IntPtr.Zero;

			IntPtr result = NewObjectArray (array.Length, Java.Lang.Class.String, IntPtr.Zero);
			CopyArray (array, result);

			return result;
		}

		public static IntPtr NewObjectArray (int length, IntPtr elementClass)
		{
			return NewObjectArray (length, elementClass, IntPtr.Zero);
		}

		public static IntPtr NewObjectArray (int length, IntPtr elementClass, IntPtr initialElement)
		{
#if JAVA_INTEROP
			return JniEnvironment.Arrays.NewObjectArray (length, new JniObjectReference (elementClass), new JniObjectReference (initialElement)).Handle;
#else   // !JAVA_INTEROP
			IntPtr result = LogCreateLocalRef (Env.NewObjectArray (Handle, length, elementClass, initialElement));

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();

			return result;
#endif  // !JAVA_INTEROP
		}

		public static IntPtr NewObjectArray<T>(params T[] values)
		{
			if (values == null)
				return IntPtr.Zero;

			IntPtr grefArrayElementClass = GetArrayElementClass (values);
			if (Java.Interop.TypeManager.GetClassName (grefArrayElementClass) == "mono/android/runtime/JavaObject") {
				DeleteGlobalRef (grefArrayElementClass);
				grefArrayElementClass = NewGlobalRef (Java.Lang.Class.Object);
			}
			try {
				IntPtr lrefArray = NewObjectArray (values.Length, grefArrayElementClass, IntPtr.Zero);

				for (int i = 0; i < values.Length; ++i) {
					JavaConvert.WithLocalJniHandle (values [i], lref => {
							SetObjectArrayElement (lrefArray, i, lref);
							return IntPtr.Zero;
					});
				}

				return lrefArray;
			}
			finally {
				DeleteGlobalRef (grefArrayElementClass);
			}

		}

		static IntPtr GetArrayElementClass<T>(T[] values)
		{
			Type    elementType = typeof (T);
			string  jniClass    = JavaConvert.GetJniClassForType (elementType);
			if (jniClass != null) {
				return FindClass (jniClass);
			}

			if (elementType.IsValueType)
				return NewGlobalRef (Java.Lang.Class.Object);

			return FindClass (elementType);
		}

		public static void CopyObjectArray<T>(IntPtr source, T[] destination)
		{
			if (source == IntPtr.Zero)
				return;
			if (destination == null)
				throw new ArgumentNullException ("destination");

			int len = Math.Min (GetArrayLength (source), destination.Length);
			for (int i = 0; i < len; ++i) {
				IntPtr value = GetObjectArrayElement (source, i);
				destination [i] = JavaConvert.FromJniHandle<T>(value, JniHandleOwnership.TransferLocalRef);
			}
		}

		public static void CopyObjectArray<T>(T[] source, IntPtr destination)
		{
			if (source == null)
				return;
			if (destination == IntPtr.Zero)
				throw new ArgumentException ("Destination is a null JNI handle!", "destination");

			int len = Math.Min (source.Length, GetArrayLength (destination));
			for (int i = 0; i < len; ++i) {
				JavaConvert.WithLocalJniHandle (source [i], lref => {
						SetObjectArrayElement (destination, i, lref);
						return IntPtr.Zero;
				});
			}
		}

		public static IntPtr NewArray (IJavaObject[] array)
		{
			if (array == null)
				return IntPtr.Zero;

			IntPtr result;
			IntPtr grefClass = FindClass (array.GetType ().GetElementType ());
			try {
				result = NewObjectArray (array.Length, grefClass, IntPtr.Zero);
			} finally {
				DeleteGlobalRef (grefClass);
			}

			CopyArray (array, result);

			return result;
		}

		static Dictionary<Type, Func<Array, IntPtr>> createManagedToNativeArray;
		static Dictionary<Type, Func<Array, IntPtr>> CreateManagedToNativeArray {
			get {
				if (createManagedToNativeArray != null)
					return createManagedToNativeArray;

				var newValue = CreateCreateManagedToNativeArray ();
				Interlocked.CompareExchange (ref createManagedToNativeArray, newValue, null);
				return createManagedToNativeArray;
			}
		}

		static Dictionary<Type, Func<Array, IntPtr>> CreateCreateManagedToNativeArray ()
		{
			return new Dictionary<Type, Func<Array, IntPtr>> () {
				{ typeof (bool),          (source) => NewArray ((bool[]) source) },
				{ typeof (byte),          (source) => NewArray ((byte[]) source) },
				{ typeof (char),          (source) => NewArray ((char[]) source) },
				{ typeof (short),         (source) => NewArray ((short[]) source) },
				{ typeof (int),           (source) => NewArray ((int[]) source) },
				{ typeof (long),          (source) => NewArray ((long[]) source) },
				{ typeof (float),         (source) => NewArray ((float[]) source) },
				{ typeof (double),        (source) => NewArray ((double[]) source) },
				{ typeof (string),        (source) => NewArray ((string[]) source) },
				{ typeof (IJavaObject),   (source) => NewArray ((IJavaObject[]) source) },
				{ typeof (Array),         (source) => NewArray (source) },
			};
		}

		public static IntPtr NewArray (Array value, Type elementType = null)
		{
			if (value == null)
				throw new ArgumentNullException ("value");

			elementType = elementType ?? value.GetType ().GetElementType ();

			if (elementType.IsArray) {
				IntPtr array = IntPtr.Zero;
				IntPtr grefArrayClass = FindClass (elementType);
				try {
					array = NewObjectArray (value.Length, grefArrayClass, IntPtr.Zero);

					for (int i = 0; i < value.Length; ++i) {
						IntPtr subarray = NewArray ((Array) value.GetValue (i), elementType.GetElementType ());
						SetObjectArrayElement (array, i, subarray);
						DeleteLocalRef (subarray);
					}

					return array;
				} catch {
					DeleteLocalRef (array);
					throw;
				} finally {
					DeleteGlobalRef (grefArrayClass);
				}
			}

			Func<Array, IntPtr> creator = GetConverter (CreateManagedToNativeArray, elementType, IntPtr.Zero);

			return creator (value);
		}

		public static IntPtr NewArray<T> (T[] array)
		{
			if (array == null)
				return IntPtr.Zero;

			if (typeof (T).IsArray) {
				return NewArray (array, typeof (T));
			}

			Func<Array, IntPtr> creator = GetConverter (CreateManagedToNativeArray, typeof (T), IntPtr.Zero);

			return creator (array);
		}

		static Dictionary<Type, Action<IntPtr, int, object>> setNativeArrayElement;
		static Dictionary<Type, Action<IntPtr, int, object>> SetNativeArrayElement {
			get {
				if (setNativeArrayElement != null)
					return setNativeArrayElement;

				var newValue = CreateSetNativeArrayElement ();
				Interlocked.CompareExchange (ref setNativeArrayElement, newValue, null);
				return setNativeArrayElement;
			}
		}

		static Dictionary<Type, Action<IntPtr, int, object>> CreateSetNativeArrayElement ()
		{
			return new Dictionary<Type, Action<IntPtr, int, object>> () {
				{ typeof (bool), (dest, index, value) => {
					var _value = new[]{(bool) value};
					_SetBooleanArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (byte), (dest, index, value) => {
					var _value = new[]{(byte) value};
					_SetByteArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (char), (dest, index, value) => {
					var _value = new[]{(char) value};
					_SetCharArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (short), (dest, index, value) => {
					var _value = new[]{(short) value};
					_SetShortArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (int), (dest, index, value) => {
					var _value = new[]{(int) value};
					_SetIntArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (long), (dest, index, value) => {
					var _value = new[]{(long) value};
					_SetLongArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (float), (dest, index, value) => {
					var _value = new[]{(float) value};
					_SetFloatArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (double), (dest, index, value) => {
					var _value = new[]{(double) value};
					_SetDoubleArrayRegion (dest, index, _value.Length, _value);
				} },
				{ typeof (string), (dest, index, value) => {
					IntPtr s = NewString (value.ToString ());
					try {
						SetObjectArrayElement (dest, index, s);
					} finally {
						DeleteLocalRef (s);
					}
				} },
				{ typeof (IJavaObject), (dest, index, value) => {
					SetObjectArrayElement (dest, index, value == null ? IntPtr.Zero : ((IJavaObject) value).Handle);
				} },
				{ typeof (Array), (dest, index, value) => {
					IntPtr _v = NewArray ((Array) value);
					SetObjectArrayElement (dest, index, _v);
					JNIEnv.DeleteLocalRef (_v);
				} },
			};
		}

		static unsafe void _SetBooleanArrayRegion (IntPtr array, int start, int length, bool[] buffer)
		{
#if JAVA_INTEROP
			fixed (bool* p = buffer)
				JniEnvironment.Arrays.SetBooleanArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			var _buffer = new byte [buffer.Length];
			for (int i = 0; i < _buffer.Length; ++i)
				_buffer [i] = buffer [i] ? (byte) 1 : (byte) 0;
			Env.SetBooleanArrayRegion (Handle, array, start, length, _buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetByteArrayRegion (IntPtr array, int start, int length, byte[] buffer)
		{
#if JAVA_INTEROP
			fixed (byte* p = buffer)
				JniEnvironment.Arrays.SetByteArrayRegion (new JniObjectReference (array), start, length, (sbyte*) p);
#else   // !JAVA_INTEROP
			Env.SetByteArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetCharArrayRegion (IntPtr array, int start, int length, char[] buffer)
		{
#if JAVA_INTEROP
			fixed (char* p = buffer)
				JniEnvironment.Arrays.SetCharArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.SetCharArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetShortArrayRegion (IntPtr array, int start, int length, short[] buffer)
		{
#if JAVA_INTEROP
			fixed (short* p = buffer)
				JniEnvironment.Arrays.SetShortArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.SetShortArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetIntArrayRegion (IntPtr array, int start, int length, int[] buffer)
		{
#if JAVA_INTEROP
			fixed (int* p = buffer)
				JniEnvironment.Arrays.SetIntArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.SetIntArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetLongArrayRegion (IntPtr array, int start, int length, long[] buffer)
		{
#if JAVA_INTEROP
			fixed (long* p = buffer)
				JniEnvironment.Arrays.SetLongArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.SetLongArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetFloatArrayRegion (IntPtr array, int start, int length, float[] buffer)
		{
#if JAVA_INTEROP
			fixed (float* p = buffer)
				JniEnvironment.Arrays.SetFloatArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.SetFloatArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		static unsafe void _SetDoubleArrayRegion (IntPtr array, int start, int length, double[] buffer)
		{
#if JAVA_INTEROP
			fixed (double* p = buffer)
				JniEnvironment.Arrays.SetDoubleArrayRegion (new JniObjectReference (array), start, length, p);
#else   // !JAVA_INTEROP
			Env.SetDoubleArrayRegion (Handle, array, start, length, buffer);

			Exception e = AndroidEnvironment.GetExceptionForLastThrowable ();
			if (e != null)
				ExceptionDispatchInfo.Capture (e).Throw ();
#endif  // !JAVA_INTEROP
		}

		public static void SetArrayItem<T> (IntPtr array_ptr, int index, T value)
		{
			if (array_ptr == IntPtr.Zero)
				throw new ArgumentException ("array_ptr");
			if (index < 0 || index >= GetArrayLength (array_ptr))
				throw new ArgumentOutOfRangeException ("index");

			Action<IntPtr, int, object> setter = GetConverter (SetNativeArrayElement, typeof (T), array_ptr);

			setter (array_ptr, index, value);
		}

		public static Java.Lang.Object[] ToObjectArray<T> (T[] array)
		{
			if (array == null)
				return null;
			Java.Lang.Object[] ret = new Java.Lang.Object [array.Length];
			for (int i = 0; i < array.Length; i++)
				ret [i] = JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (array [i]));
			return ret;
		}

#if !JAVA_INTEROP
		delegate int GetEnvDelegate (IntPtr javavm, out IntPtr envptr, int version);
		delegate int AttachCurrentThreadDelegate (IntPtr javavm, out IntPtr env, IntPtr args);
		delegate int DetachCurrentThreadDelegate (IntPtr javavm);

		struct JNIInvokeInterface {
			public IntPtr reserved0;
			public IntPtr reserved1;
			public IntPtr reserved2;
 
			public IntPtr DestroyJavaVM; // jint       (*DestroyJavaVM)(JavaVM*);
			public AttachCurrentThreadDelegate AttachCurrentThread;
			public DetachCurrentThreadDelegate DetachCurrentThread;
			public GetEnvDelegate GetEnv;
			public IntPtr AttachCurrentThreadAsDaemon; //jint        (*AttachCurrentThreadAsDaemon)(JavaVM*, JNIEnv**, void*);
		}

		internal struct JNINativeMethod {

			public string Name;
			public string Sig;
			public Delegate Func;

			public JNINativeMethod (string name, string sig, Delegate func)
			{
				Name = name;
				Sig = sig;
				Func = func;
			}
		} 
#endif  // !JAVA_INTEROP

#if ANDROID_8
		[DllImport ("libjnigraphics.so")]
		static extern int AndroidBitmap_getInfo (IntPtr env, IntPtr jbitmap, out Android.Graphics.AndroidBitmapInfo info);

		[DllImport ("libjnigraphics.so")]
		static extern int AndroidBitmap_lockPixels (IntPtr env, IntPtr jbitmap, out IntPtr addrPtr);

		[DllImport ("libjnigraphics.so")]
		static extern int AndroidBitmap_unlockPixels(IntPtr env, IntPtr jbitmap);

		internal static int AndroidBitmap_getInfo (IntPtr jbitmap, out Android.Graphics.AndroidBitmapInfo info)
		{
			return AndroidBitmap_getInfo (Handle, jbitmap, out info);
		}

		internal static int AndroidBitmap_lockPixels (IntPtr jbitmap, out IntPtr addrPtr)
		{
			return AndroidBitmap_lockPixels (Handle, jbitmap, out addrPtr);
		}

		internal static int AndroidBitmap_unlockPixels (IntPtr jbitmap)
		{
			return AndroidBitmap_unlockPixels (Handle, jbitmap);
		}
#endif  // ANDROID_8
	}
}


