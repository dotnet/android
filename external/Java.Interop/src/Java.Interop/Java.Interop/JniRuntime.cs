using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop
{
	delegate int DestroyJavaVMDelegate (IntPtr javavm);
	delegate int GetEnvDelegate (IntPtr javavm, out IntPtr envptr, int version);
	delegate int AttachCurrentThreadDelegate (IntPtr javavm, out IntPtr env, ref JavaVMThreadAttachArgs args);
	delegate int DetachCurrentThreadDelegate (IntPtr javavm);
	delegate int AttachCurrentThreadAsDaemonDelegate (IntPtr javavm, out IntPtr env, IntPtr args);

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


	partial class JniRuntime {
		public partial class CreationOptions {

			public  bool                        TrackIDs                    {get; set;}
			public  bool                        DestroyRuntimeOnDispose     {get; set;}

			// Prefer JNIEnv::NewObject() over JNIEnv::AllocObject() + JNIEnv::CallNonvirtualVoidMethod()
			public  bool                        NewObjectRequired           {get; set;}

			public  JniVersion                  JniVersion                  {get; set;}

			public  IntPtr                      InvocationPointer           {get; set;}
			public  IntPtr                      EnvironmentPointer          {get; set;}

			public  JniObjectReference          ClassLoader                 {get; set;}
			public  IntPtr                      ClassLoader_LoadClass_id    {get; set;}

			public  JniObjectReferenceManager   ObjectReferenceManager      {get; set;}
			public  JniTypeManager              TypeManager                 {get; set;}

			public CreationOptions ()
			{
				JniVersion                  = JniVersion.v1_2;
			}
		}
	}

	partial class JniRuntime {

		interface ISetRuntime {
			void OnSetRuntime (JniRuntime runtime);
		}
	}

	partial class NativeMethods {
		const string JvmLibrary = "jvm.dll";

		[DllImport (JvmLibrary)]
		internal static extern int JNI_GetCreatedJavaVMs ([Out] IntPtr[] handles, int bufLen, out int nVMs);
	}

	public partial class JniRuntime : IDisposable
	{
		const   int     JNI_OK          = 0;
		const   int     JNI_EDETACHED   = -2;
		const   int     JNI_EVERSION    = -3;


		static ConcurrentDictionary<IntPtr, JniRuntime>     Runtimes = new ConcurrentDictionary<IntPtr, JniRuntime> ();

		public static IEnumerable<JniRuntime> GetRegisteredRuntimes ()
		{
			return Runtimes.Values;
		}

		public static JniRuntime GetRegisteredRuntime (IntPtr invocationPointer)
		{
			JniRuntime vm;
			return Runtimes.TryGetValue (invocationPointer, out vm)
				? vm
				: null;
		}

		public static IEnumerable<IntPtr> GetAvailableInvocationPointers ()
		{
			int nVMs;
			int r = NativeMethods.JNI_GetCreatedJavaVMs (null, 0, out nVMs);
			if (r != 0)
				throw new NotSupportedException ("JNI_GetCreatedJavaVMs() returned: " + r.ToString ());
			var handles = new IntPtr [nVMs];
			r = NativeMethods.JNI_GetCreatedJavaVMs (handles, handles.Length, out nVMs);
			if (r != 0)
				throw new InvalidOperationException ("JNI_GetCreatedJavaVMs() [take 2!] returned: " + r.ToString ());
			return handles;
		}

		static JniRuntime current;
		public static JniRuntime CurrentRuntime {
			get {
				var c   = current;
				if (c != null)
					return c;
				int     count   = 0;
				foreach (var vm in Runtimes.Values) {
					if (count++ == 0)
						c = vm;
				}
				if (count == 1) {
					Interlocked.CompareExchange (ref current, c, null);
					return c;
				}
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} Java Runtimes. Don't know which to use. Use JniRuntime.SetCurrent().", count));
				Debug.Assert (count == 0);
				var available   = GetAvailableInvocationPointers ().FirstOrDefault ();
				if (available == IntPtr.Zero)
					throw new NotSupportedException ("No available Java runtime to attach to. Please create one.");
				var options     = new CreationOptions () {
					DestroyRuntimeOnDispose = false,
					InvocationPointer       = available,
				};
				// Sets `current`
				return new JniRuntime (options);
			}
		}

		public static void SetCurrent (JniRuntime newCurrent)
		{
			if (newCurrent == null)
				throw new ArgumentNullException ("newCurrent");
			Runtimes.TryAdd (newCurrent.InvocationPointer, newCurrent);
			current = newCurrent;
		}

		ConcurrentDictionary<IntPtr, IDisposable>       TrackedInstances    = new ConcurrentDictionary<IntPtr, IDisposable> ();

		JavaVMInterface                                 Invoker;
		bool                                            DestroyRuntimeOnDispose;

		internal    JniObjectReference                  ClassLoader;
		internal    JniMethodInfo                       ClassLoader_LoadClass;

		public  IntPtr                                  InvocationPointer   {get; private set;}

		public      JniVersion                          JniVersion          {get; private set;}

		internal    bool                                TrackIDs            {get; private set;}
		internal    bool                                NewObjectRequired   {get; private set;}

		protected JniRuntime (CreationOptions options)
		{
			if (options == null)
				throw new ArgumentNullException ("options");
			if (options.InvocationPointer == IntPtr.Zero)
				throw new ArgumentException ("options.InvocationPointer is null", "options");

			TrackIDs     = options.TrackIDs;
			DestroyRuntimeOnDispose     = options.DestroyRuntimeOnDispose;

			NewObjectRequired   = options.NewObjectRequired;

			JniVersion          = options.JniVersion;
			InvocationPointer   = options.InvocationPointer;
			Invoker             = CreateInvoker (InvocationPointer);

			SetValueManager (options);
			SetMarshalMemberBuilder (options);

			ObjectReferenceManager      = SetRuntime (options.ObjectReferenceManager);
			TypeManager                 = SetRuntime (options.TypeManager ?? new JniTypeManager ());

			if (Interlocked.CompareExchange (ref current, this, null) != null) {
				Debug.WriteLine ("WARNING: More than one JniRuntime instance created. This is DOOMED TO FAIL.");
			}

			Runtimes.TryAdd (InvocationPointer, this);

			var envp    = options.EnvironmentPointer;
			if (envp == IntPtr.Zero &&
					Invoker.GetEnv (InvocationPointer, out envp, (int) JniVersion) != JNI_OK &&
					(envp = _AttachCurrentThread ()) == IntPtr.Zero) {
				// Shouldn't be reached, as _AttachCurrentThread() throws
				throw new InvalidOperationException ("Could not obtain JNIEnv* value!");
			}
			var env     = new JniEnvironmentInfo (envp, this);
			JniEnvironment.SetEnvironmentInfo (env);

#if !XA_INTEGRATION
			ManagedPeer.Init ();
#endif  // !XA_INTEGRATION

			ClassLoader = options.ClassLoader;
			if (options.ClassLoader_LoadClass_id != IntPtr.Zero) {
				ClassLoader_LoadClass   = new JniMethodInfo (options.ClassLoader_LoadClass_id, isStatic: false);
			}

			if (ClassLoader.IsValid) {
				ClassLoader = ClassLoader.NewGlobalRef ();
			}

			if (!ClassLoader.IsValid || ClassLoader_LoadClass == null) {
				using (var t = new JniType ("java/lang/ClassLoader")) {
					if (!ClassLoader.IsValid) {
						var m       = t.GetStaticMethod ("getSystemClassLoader", "()Ljava/lang/ClassLoader;");
						var loader  = JniEnvironment.StaticMethods.CallStaticObjectMethod (t.PeerReference, m);
						ClassLoader = loader.NewGlobalRef ();
						JniObjectReference.Dispose (ref loader);
					}
					if (ClassLoader_LoadClass == null) {
						ClassLoader_LoadClass   = t.GetInstanceMethod ("loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
					}
				}
			}
		}

		T SetRuntime<T> (T value)
			where T : class, ISetRuntime
		{
			if (value == null)
				return null;

			value.OnSetRuntime (this);
			return value;
		}

		partial void SetValueManager (CreationOptions options);
		partial void SetMarshalMemberBuilder (CreationOptions options);

		static unsafe JavaVMInterface CreateInvoker (IntPtr handle)
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return (JavaVMInterface) Marshal.PtrToStructure (p, typeof (JavaVMInterface));
		}

		~JniRuntime ()
		{
			Dispose (false);
		}

		public virtual string GetCurrentManagedThreadName ()
		{
			return null;
		}

		public virtual string GetCurrentManagedThreadStackTrace (int skipFrames = 0, bool fNeedFileInfo = false)
		{
			return null;
		}

		public virtual void FailFast (string message)
		{
			var t = typeof (Environment).GetTypeInfo ();
			var m = t.DeclaredMethods.FirstOrDefault (x => x.Name == "FailFast");
			m.Invoke (null, new object[]{ message });
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, InvocationPointer.ToString ("x"));
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (InvocationPointer == IntPtr.Zero)
				return;

			Interlocked.CompareExchange (ref current, null, this);

			Runtimes.TryUpdate (InvocationPointer, null, this);

			JniObjectReference.Dispose (ref ClassLoader);

			if (disposing) {
				ClearTrackedReferences ();
#if !XA_INTEGRATION
				ValueManager.Dispose ();
#endif  // !XA_INTEGRATION
				ObjectReferenceManager.Dispose ();
			}

			var environments    = JniEnvironment.Info.Values;
			for (int i = 0; i < environments.Count; ++i) {
				var e   = environments [i];
				if (e.Runtime != this)
					continue;
				environments [i].Dispose ();
			}

			if (DestroyRuntimeOnDispose) {
				DestroyRuntime ();
			}
			InvocationPointer   = IntPtr.Zero;
			Invoker             = default (JavaVMInterface);
		}

		public void AttachCurrentThread (string name = null, JniObjectReference group = default (JniObjectReference))
		{
			var jnienv  = _AttachCurrentThread (name, group);
			JniEnvironment.SetEnvironmentPointer (jnienv, this);
		}

		internal    IntPtr  _AttachCurrentThread (string name = null, JniObjectReference group = default (JniObjectReference))
		{
			AssertValid ();
			var threadArgs = new JavaVMThreadAttachArgs () {
				version = JniVersion,
			};
			try {
				if (name != null)
					threadArgs.name = Marshal.StringToHGlobalAnsi (name);
				if (group.IsValid)
					threadArgs.group = group.Handle;
				IntPtr jnienv;
				int r = Invoker.AttachCurrentThread (InvocationPointer, out jnienv, ref threadArgs);
				if (r != JNI_OK)
					throw new NotSupportedException ("AttachCurrentThread returned " + r.ToString ());
				return jnienv;
			} finally {
				Marshal.FreeHGlobal (threadArgs.name);
			}
		}

		void AssertValid ()
		{
			if (InvocationPointer == IntPtr.Zero)
				throw new ObjectDisposedException (nameof (JniRuntime));
		}

		public void DestroyRuntime ()
		{
			AssertValid ();
			Invoker.DestroyJavaVM (InvocationPointer);
		}

		public virtual Exception GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
#if XA_INTEGRATION
			throw new NotSupportedException ("Do not know h ow to convert a JniObjectReference to a System.Exception!");
#else   // !XA_INTEGRATION
			return ValueManager.GetValue<Exception> (ref reference, options);
#endif  // !̣XA_INTEGRATION
		}

		public int GlobalReferenceCount {
			get {return ObjectReferenceManager.GlobalReferenceCount;}
		}

		public int WeakGlobalReferenceCount {
			get {return ObjectReferenceManager.WeakGlobalReferenceCount;}
		}

		public JniObjectReferenceManager    ObjectReferenceManager      {get; private set;}
		public JniTypeManager               TypeManager                 {get; private set;}

#if !XA_INTEGRATION
		internal void TrackID (IntPtr key, IDisposable value)
		{
			AssertValid ();

			if (TrackIDs)
				TrackedInstances.TryAdd (key, value);
		}
#endif  // !XA_INTEGRATION

		internal void Track (JniType value)
		{
			TrackedInstances.TryAdd (value.PeerReference.Handle, value);
		}

		internal void UnTrack (IntPtr key)
		{
			IDisposable _;
			TrackedInstances.TryRemove (key, out _);
		}

		void ClearTrackedReferences ()
		{
			foreach (var k in TrackedInstances.Keys.ToList ()) {
				IDisposable d;
				if (TrackedInstances.TryRemove (k, out d))
					d.Dispose ();
			}
			TrackedInstances.Clear ();
		}

		public virtual bool ExceptionShouldTransitionToJni (Exception e)
		{
			return !Debugger.IsAttached;
		}
	}

	partial class JniRuntime {

		public virtual void RaisePendingException (Exception pendingException)
		{
			if (pendingException == null)
				throw new ArgumentNullException (nameof (pendingException));
#if XA_INTEGRATION
			throw new NotSupportedException ("Do not know how to marshal System.Exception instances.");
#else   // XA_INTEGRATION
			var je  = pendingException as JavaException;
			if (je == null) {
				je  = new JavaProxyThrowable (pendingException);
			}
			JniEnvironment.Exceptions.Throw (je.PeerReference);
#endif  // !XA_INTEGRATION
		}
	}
}

