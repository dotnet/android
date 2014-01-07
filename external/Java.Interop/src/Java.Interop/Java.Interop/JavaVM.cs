using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

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

		internal    List<string>    Options = new List<string> ();

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

		public JavaVM CreateJavaVM ()
		{
			return new JavaVM (this);
		}
	}

	public class JavaVM : IDisposable
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

		void CreateJavaVM (ref JavaVMInitArgs args)
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
			Initialize (javavm, jnienv);
		}

		ConcurrentDictionary<IntPtr, JniEnvironment>    Environments = new ConcurrentDictionary<IntPtr, JniEnvironment> ();

		ConcurrentDictionary<SafeHandle, IDisposable>   TrackedInstances;

		JavaVMInterface                                 Invoker;
		bool                                            DestroyVM;

		int                                             GrefCount;
		int                                             LrefCount;
		int                                             WgrefCount;

		public  JavaVMSafeHandle                        SafeHandle      {get; private set;}

		protected JavaVM ()
			: this (new JavaVMBuilder ())
		{
		}

		internal protected unsafe JavaVM (JavaVMBuilder builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");

			var args = new JavaVMInitArgs () {
				version             = builder.JniVersion,
				nOptions            = builder.Options.Count,
				ignoreUnrecognized  = builder.IgnoreUnrecognizedOptions ? (byte) 1 : (byte) 0,
			};
			var options = new JavaVMOption [builder.Options.Count];
			try {
				for (int i = 0; i < options.Length; ++i)
					options [i].optionString = Marshal.StringToHGlobalAnsi (builder.Options [i]);
				fixed (JavaVMOption* popts = options) {
					args.options = (IntPtr) popts;
					CreateJavaVM (ref args);
				}
			} finally {
				for (int i = 0; i < options.Length; ++i)
					Marshal.FreeHGlobal (options [i].optionString);
			}

			TrackIDs    = builder.TrackIDs;
			DestroyVM   = true;
		}

		public JavaVM (JavaVMSafeHandle safeHandle, JniEnvironmentSafeHandle jnienv = null)
		{
			Initialize (safeHandle, jnienv);
		}

		void Initialize (JavaVMSafeHandle safeHandle, JniEnvironmentSafeHandle jnienv)
		{
			if (safeHandle == null)
				throw new ArgumentNullException ("safeHandle");
			if (safeHandle.IsInvalid)
				throw new ArgumentException ("safeHandle is not valid.", "safeHandle");

			if (current == null)
				current = this;

			SafeHandle  = safeHandle;
			Invoker     = safeHandle.CreateInvoker ();

			if (jnienv != null) {
				var env = new JniEnvironment (jnienv, this);
				Environments.TryAdd (env.SafeHandle.DangerousGetHandle (), env);
			}

			JavaVMs.TryAdd (SafeHandle.DangerousGetHandle (), this);
		}

		~JavaVM ()
		{
			Dispose (false);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, SafeHandle.DangerousGetHandle ().ToString ("x"));
		}

		public void Dispose ()
		{
			Dispose (true);
		}

		protected virtual void Dispose (bool disposing)
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

		public virtual Exception GetExceptionForThrowable (JniLocalReference value)
		{
			using (var s = JniEnvironment.Current.Object_toString.CallVirtualObjectMethod (value)) {
				return new JniException (JniStrings.ToString (s) ?? "JNI error: no message provided");
			}
		}

		public int LocalReferenceCount {
			get {return LrefCount;}
		}

		public int GlobalReferenceCount {
			get {return GrefCount;}
		}

		public int WeakGlobalReferenceCount {
			get {return WgrefCount;}
		}

		protected internal virtual void LogCreateLocalRef (JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return;
			Interlocked.Increment (ref LrefCount);
		}

		protected internal virtual void LogCreateLocalRef (JniLocalReference value, JniReferenceSafeHandle sourceValue)
		{
			if (value == null || value.IsInvalid)
				return;
			Interlocked.Increment (ref LrefCount);
		}

		protected internal virtual void LogDestroyLocalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			Interlocked.Decrement (ref LrefCount);
		}

		protected internal virtual void LogCreateGlobalRef (JniGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			if (value == null || value.IsInvalid)
				return;
			Interlocked.Increment (ref GrefCount);
		}

		protected internal virtual void LogDestroyGlobalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			Interlocked.Decrement (ref GrefCount);
		}

		protected internal virtual void LogCreateWeakGlobalRef (JniWeakGlobalReference value, JniReferenceSafeHandle sourceValue)
		{
			if (value == null || value.IsInvalid)
				return;
			Interlocked.Increment (ref WgrefCount);
		}

		protected internal virtual void LogDestroyWeakGlobalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			Interlocked.Decrement (ref WgrefCount);
		}

		public bool TrackIDs {
			get {
				return TrackedInstances != null;
			}
			private set {
				TrackedInstances        = new ConcurrentDictionary<SafeHandle, IDisposable> ();
			}
		}

		internal void Track (SafeHandle key, IDisposable value)
		{
			if (TrackedInstances != null)
				TrackedInstances.TryAdd (key, value);
		}

		internal void UnTrack (SafeHandle key)
		{
			if (TrackedInstances != null) {
				IDisposable _;
				TrackedInstances.TryRemove (key, out _);
			}
		}

		void ClearTrackedReferences ()
		{
			if (TrackedInstances != null) {
				foreach (var k in TrackedInstances.Keys.ToList ()) {
					IDisposable d;
					if (TrackedInstances.TryRemove (k, out d))
						d.Dispose ();
				}
			}
			foreach (var env in Environments.Values)
				env.Dispose ();
			Environments.Clear ();
		}
	}
}

