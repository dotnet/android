using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	delegate int DestroyJavaVMDelegate (JavaVMSafeHandle javavm);
	delegate int GetEnvDelegate (JavaVMSafeHandle javavm, out IntPtr envptr, int version);
	delegate int AttachCurrentThreadDelegate (JavaVMSafeHandle javavm, out IntPtr env, ref JavaVMThreadAttachArgs args);
	delegate int DetachCurrentThreadDelegate (JavaVMSafeHandle javavm);
	delegate int AttachCurrentThreadAsDaemonDelegate (JavaVMSafeHandle javavm, out IntPtr env, IntPtr args);

	struct JavaVMInterface {
		public IntPtr reserved0;
		public IntPtr reserved1;
		public IntPtr reserved2;

		public DestroyJavaVMDelegate DestroyJavaVM; // jint       (*DestroyJavaVM)(JavaVM*);
		public AttachCurrentThreadDelegate AttachCurrentThread;
		public DetachCurrentThreadDelegate DetachCurrentThread;
		public GetEnvDelegate GetEnv;
		public AttachCurrentThreadAsDaemonDelegate AttachCurrentThreadAsDaemon; //jint        (*AttachCurrentThreadAsDaemon)(JavaVM*, JNIEnv**, void*);
	}

	struct JniNativeMethod {

		public string Name;
		public string Sig;
		public Delegate Func;

		public JniNativeMethod (string name, string sig, Delegate func)
		{
			Name = name;
			Sig = sig;
			Func = func;
		}
	}

	public enum JniVersion {
		// v1_1    = 0x00010001,
		v1_2    = 0x00010002,
		v1_4    = 0x00010004,
		v1_6	= 0x00010006,
	}

	struct JavaVMThreadAttachArgs {
		public  JniVersion 	        version;    /*		 must be >= JNI_VERSION_1_2 */
		public  IntPtr              name;       /*		 NULL or name of thread as modified UTF-8 str */
		public  IntPtr              group;      /*		 global ref of a ThreadGroup object, or NULL */
	}

	struct JavaVMOption {
		public  IntPtr /* const char * */   optionString;
		public  IntPtr /* void * */         extraInfo;
	}

	struct JavaVMInitArgs {
		public  JniVersion                      version;    /*		 use JNI_VERSION_1_2 or later */

		public  int                             nOptions;
		public  IntPtr /* JavaVMOption[] */     options;
		public  byte                            ignoreUnrecognized;
	}

	public sealed class JavaVMSafeHandle : SafeHandle {

		JavaVMSafeHandle ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		internal JavaVMSafeHandle (IntPtr handle)
			: this ()
		{
			SetHandle (handle);
		}

		public override bool IsInvalid {
			get {return handle == IntPtr.Zero;}
		}

		internal IntPtr Handle {
			get {return base.handle;}
		}

		protected override bool ReleaseHandle ()
		{
			return false;
		}

		internal unsafe JavaVMInterface CreateInvoker ()
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return (JavaVMInterface) Marshal.PtrToStructure (p, typeof(JavaVMInterface));
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	public sealed class JavaVMBuilder {

		List<string> Options = new List<string> ();

		public  JniVersion  JniVersion                  {get; set;}
		public  bool        IgnoreUnrecognizedOptions   {get; set;}
		public  bool        TrackIDs                    {get; set;}

		public JavaVMBuilder ()
		{
			JniVersion  = JniVersion.v1_2;
		}

		public JavaVMBuilder AddOption (string option)
		{
			Options.Add (option);
			return this;
		}

		public JavaVMBuilder AddSystemProperty (string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (value == null)
				throw new ArgumentNullException ("value");
			Options.Add (string.Format ("-D{0}={1}", name, value));
			return this;
		}

		public unsafe JavaVM CreateJavaVM ()
		{
			var args = new JavaVMInitArgs () {
				version             = JniVersion,
				nOptions            = Options.Count,
				ignoreUnrecognized  = IgnoreUnrecognizedOptions ? (byte) 1 : (byte) 0,
			};
			var options = new JavaVMOption [Options.Count];
			try {
				for (int i = 0; i < options.Length; ++i)
					options [i].optionString = Marshal.StringToHGlobalAnsi (Options [i]);
				fixed (JavaVMOption* popts = options) {
					args.options = (IntPtr) popts;
					return JavaVM.CreateJavaVM (ref args, TrackIDs);
				}
			} finally {
				for (int i = 0; i < options.Length; ++i)
					Marshal.FreeHGlobal (options [i].optionString);
			}
		}
	}

	public sealed class JavaVM : IDisposable
	{
		const string LibraryName = "/System/Library/Frameworks/JavaVM.framework/JavaVM";

		[DllImport (LibraryName)]
		static extern int JNI_CreateJavaVM (out JavaVMSafeHandle javavm, out JniEnvironmentSafeHandle jnienv, ref JavaVMInitArgs args);

		[DllImport (LibraryName)]
		static extern int JNI_GetCreatedJavaVMs ([Out] IntPtr[] handles, int bufLen, out int nVMs);

		static ConcurrentDictionary<IntPtr, JavaVM>     JavaVMs = new ConcurrentDictionary<IntPtr, JavaVM> ();

		static JavaVM current;
		public static JavaVM Current {
			get {
				if (current != null)
					return current;
				JavaVM  c       = null;
				int     count   = 0;
				foreach (var vm in GetCreatedJavaVMs ()) {
					if (count++ == 0)
						c = vm;
				}
				if (count == 0)
					throw new InvalidOperationException ("No JavaVM has been created. Please use JavaVMBuilder.CreateJavaVM().");
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} JavaVMs. Don't know which to use. Use JavaVM.SetCurrent().", count));
				return current = c;
			}
		}

		public static void SetCurrent (JavaVM newCurrent)
		{
			if (newCurrent == null)
				throw new ArgumentNullException ("newCurrent");
			current = newCurrent;
		}

		public static IEnumerable<JavaVM> GetCreatedJavaVMs ()
		{
			int nVMs;
			int r = JNI_GetCreatedJavaVMs (null, 0, out nVMs);
			if (r != 0)
				throw new NotSupportedException ("JNI_GetCreatedJavaVMs() returned: " + r);
			var handles = new IntPtr [nVMs];
			r = JNI_GetCreatedJavaVMs (handles, handles.Length, out nVMs);
			if (r != 0)
				throw new InvalidOperationException ("JNI_GetCreatedJavaVMs() [take 2!] returned: " + r);
			foreach (var h in handles) {
				JavaVM v;
				if (!JavaVMs.TryGetValue (h, out v))
					JavaVMs.TryAdd (h, v = new JavaVM (new JavaVMSafeHandle (h)));
				yield return v;
			}
		}

		public static JavaVM FromHandle (JavaVMSafeHandle handle)
		{
			JavaVM vm;
			if (JavaVMs.TryGetValue (handle.DangerousGetHandle (), out vm))
				return vm;
			return new JavaVM (handle, null);
		}

		internal static JavaVM CreateJavaVM (ref JavaVMInitArgs args, bool trackIds)
		{
			JavaVMSafeHandle            javavm;
			JniEnvironmentSafeHandle    jnienv;
			int r = JNI_CreateJavaVM (out javavm, out jnienv, ref args);
			if (r != 0) {
				var message = string.Format ("{1}JNI_CreateJavaVM returned {0}.",
						r,
						JavaVMs.Count == 0
							? ""
							: "The JDK supports creating at most one JVM per process, ever; " +
							  "do you have a JVM running already, or have you already created (and destroyed?) one? ");
				throw new NotSupportedException (message);
			}
			return new JavaVM (javavm, jnienv) {
				TrackIDs    = trackIds,
				DestroyVM   = true,
			};
		}

		ConcurrentDictionary<IntPtr, JniEnvironment>    Environments = new ConcurrentDictionary<IntPtr, JniEnvironment> ();

		ConcurrentBag<JniInstanceMethodID>              TrackedInstanceMethods;
		ConcurrentBag<JniStaticMethodID>                TrackedStaticMethods;
		ConcurrentBag<JniInstanceFieldID>               TrackedInstanceFields;
		ConcurrentBag<JniStaticFieldID>                 TrackedStaticFields;

		JavaVMInterface                                 Invoker;
		bool                                            DestroyVM;

		public  JavaVMSafeHandle                        SafeHandle      {get; private set;}

		public JavaVM (JavaVMSafeHandle safeHandle, JniEnvironmentSafeHandle jnienv = null)
		{
			if (safeHandle == null)
				throw new ArgumentNullException ("safeHandle");
			if (safeHandle.IsInvalid)
				throw new ArgumentException ("safeHandle is not valid.", "safeHandle");

			SafeHandle  = safeHandle;
			Invoker     = safeHandle.CreateInvoker ();
			Debug.WriteLine ("# JavaVM..ctor: post invoker");

			if (jnienv != null) {
				Debug.WriteLine ("# JavaVM..ctor: creating JniEnvironment");
				var env = new JniEnvironment (jnienv, this);
				Debug.WriteLine ("# JavaVM..ctor: created JniEnvironment");
				Environments.TryAdd (env.SafeHandle.DangerousGetHandle (), env);
			}

			JavaVMs.TryAdd (SafeHandle.DangerousGetHandle (), this);

			if (current == null)
				current = this;
		}

		~JavaVM ()
		{
			Dispose ();
		}

		public override string ToString ()
		{
			return string.Format ("Java.Interop.JavaVM(0x{0})", SafeHandle.DangerousGetHandle ().ToString ("x"));
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;

			if (current == this)
				current = null;

			ClearTrackedReferences ();
			if (DestroyVM)
				DestroyJavaVM ();
			JavaVM _;
			JavaVMs.TryRemove (SafeHandle.DangerousGetHandle (), out _);
			SafeHandle.Dispose ();
			SafeHandle = null;
		}

		public void AttachCurrentThread (string name = null, JniReferenceSafeHandle group = null)
		{
			var threadArgs = new JavaVMThreadAttachArgs () {
				version = JniVersion.v1_2,
			};
			try {
				if (name != null)
					threadArgs.name = Marshal.StringToHGlobalAnsi (name);
				if (group != null)
					threadArgs.group = group.DangerousGetHandle ();
				IntPtr jnienv;
				int r = Invoker.AttachCurrentThread (SafeHandle, out jnienv, ref threadArgs);
				if (r != 0)
					throw new NotSupportedException ("AttachCurrentThread returned " + r);
				Environments.TryAdd (jnienv, new JniEnvironment (new JniEnvironmentSafeHandle (jnienv), this));
			} finally {
				Marshal.FreeHGlobal (threadArgs.name);
			}
		}

		public void DestroyJavaVM ()
		{
			Invoker.DestroyJavaVM (SafeHandle);
		}

		public bool TrackIDs {
			get {
				return TrackedInstanceMethods != null;
			}
			private set {
				TrackedInstanceMethods  = new ConcurrentBag<JniInstanceMethodID> ();
				TrackedStaticMethods    = new ConcurrentBag<JniStaticMethodID> ();
				TrackedStaticFields     = new ConcurrentBag<JniStaticFieldID> ();
				TrackedInstanceFields   = new ConcurrentBag<JniInstanceFieldID> ();
			}
		}

		public void Track (JniInstanceMethodID method)
		{
			if (TrackedInstanceMethods != null)
				TrackedInstanceMethods.Add (method);
		}

		public void Track (JniStaticMethodID method)
		{
			if (TrackedStaticMethods != null)
				TrackedStaticMethods.Add (method);
		}

		public void Track (JniInstanceFieldID field)
		{
			if (TrackedInstanceFields != null)
				TrackedInstanceFields.Add (field);
		}

		public void Track (JniStaticFieldID field)
		{
			if (TrackedStaticFields != null)
				TrackedStaticFields.Add (field);
		}

		void ClearTrackedReferences ()
		{
			foreach (var env in Environments.Values)
				env.Dispose ();
			Environments.Clear ();

			if (TrackedInstanceMethods != null)
				foreach (var m in TrackedInstanceMethods)
					m.Dispose ();
			if (TrackedStaticMethods != null)
				foreach (var m in TrackedStaticMethods)
					m.Dispose ();
			if (TrackedInstanceFields != null)
				foreach (var m in TrackedInstanceFields)
					m.Dispose ();
			if (TrackedStaticFields != null)
				foreach (var m in TrackedStaticFields)
					m.Dispose ();
		}
	}
}

