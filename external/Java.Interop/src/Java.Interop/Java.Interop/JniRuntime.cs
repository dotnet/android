#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

			public  JniObjectReferenceManager?  ObjectReferenceManager      {get; set;}
			public  JniTypeManager?             TypeManager                 {get; set;}
			public  string?                     JvmLibraryPath              {get; set;}
			public  bool                        JniAddNativeMethodRegistrationAttributePresent { get; set; } = true;

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

	public partial class JniRuntime : IDisposable
	{
		const   int     JNI_OK          = 0;
		const   int     JNI_EDETACHED   = -2;
		const   int     JNI_EVERSION    = -3;

		static Dictionary<IntPtr, JniRuntime>   Runtimes = new Dictionary<IntPtr, JniRuntime> ();

		public static IEnumerable<JniRuntime> GetRegisteredRuntimes ()
		{
			return Runtimes.Values;
		}

		public static JniRuntime? GetRegisteredRuntime (IntPtr invocationPointer)
		{
			lock (Runtimes) {
				return Runtimes.TryGetValue (invocationPointer, out var vm)
					? vm
					: null;
			}
		}

		[Obsolete ("Not sensible/usable at this level, and cannot work on e.g. Android.  " +
				"Try Java.Interop.JreRuntime.GetAvailableInvocationPointers() in Java.Runtime.Environment.dll, " +
				"or rethink your structure.", error: true)]
		[SuppressMessage ("Design", "CA1024:Use properties where appropriate",
				Justification = "ABI compatibility")]
		public static IEnumerable<IntPtr> GetAvailableInvocationPointers ()
		{
			throw new NotSupportedException ();
		}

		static JniRuntime? current;
		public static JniRuntime CurrentRuntime {
			get {
				var c   = current;
				if (c != null)
					return c;
				int     count   = 0;
				lock (Runtimes) {
					foreach (var vm in Runtimes.Values) {
						if (count++ == 0)
							c = vm;
					}
				}
				if (count == 1) {
					Interlocked.CompareExchange (ref current, c, null);
					return c!;
				}
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} known Java Runtime instances. Don't know which to use. Use JniRuntime.SetCurrent().", count));
				Debug.Assert (count == 0);
				throw new NotSupportedException ("No available Java runtime to attach to. Please create one.");
			}
		}

		public static void SetCurrent (JniRuntime newCurrent)
		{
			if (newCurrent == null)
				throw new ArgumentNullException (nameof (newCurrent));
			lock (Runtimes) {
				Runtimes [newCurrent.InvocationPointer] = newCurrent;
			}
			current = newCurrent;
		}

		Dictionary<IntPtr, IDisposable>                 TrackedInstances    = new Dictionary<IntPtr, IDisposable> ();

		JavaVMInterface                                 Invoker;
		bool                                            DestroyRuntimeOnDispose;

		internal    JniObjectReference                  ClassLoader;
		internal    JniMethodInfo?                      ClassLoader_LoadClass;

		public  IntPtr                                  InvocationPointer   {get; private set;}

		public      JniVersion                          JniVersion          {get; private set;}

		internal    bool                                TrackIDs            {get; private set;}
		internal    bool                                NewObjectRequired   {get; private set;}
		internal    bool                                JniAddNativeMethodRegistrationAttributePresent { get; }

		protected JniRuntime (CreationOptions options)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));
			if (options.InvocationPointer == IntPtr.Zero)
				throw new ArgumentException ("options.InvocationPointer is null", nameof (options));

			TrackIDs     = options.TrackIDs;
			DestroyRuntimeOnDispose     = options.DestroyRuntimeOnDispose;
			JniAddNativeMethodRegistrationAttributePresent = options.JniAddNativeMethodRegistrationAttributePresent;

			NewObjectRequired   = options.NewObjectRequired;

			JniVersion          = options.JniVersion;
			InvocationPointer   = options.InvocationPointer;
			Invoker             = CreateInvoker (InvocationPointer);

			SetValueManager (options);
			SetMarshalMemberBuilder (options);

			ObjectReferenceManager      = SetRuntime (options.ObjectReferenceManager ?? throw new NotSupportedException ($"Please set {nameof (CreationOptions)}.{nameof (options.ObjectReferenceManager)}!"));
			TypeManager                 = SetRuntime (options.TypeManager ?? new JniTypeManager ());

			if (Interlocked.CompareExchange (ref current, this, null) != null) {
				Debug.WriteLine ("WARNING: More than one JniRuntime instance created. This is DOOMED TO FAIL.");
			}

			lock (Runtimes) {
				Runtimes [InvocationPointer] = this;
			}

			var envp    = options.EnvironmentPointer;
			if (envp == IntPtr.Zero &&
					Invoker.GetEnv (InvocationPointer, out envp, (int) JniVersion) != JNI_OK &&
					(envp = _AttachCurrentThread ()) == IntPtr.Zero) {
				// Shouldn't be reached, as _AttachCurrentThread() throws
				throw new InvalidOperationException ("Could not obtain JNIEnv* value!");
			}
			var env     = new JniEnvironmentInfo (envp, this);
			JniEnvironment.SetEnvironmentInfo (env);

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

#if !XA_JI_EXCLUDE
			ManagedPeer.Init ();
#endif  // !XA_JI_EXCLUDE
		}

		T SetRuntime<T> (T value)
			where T : class, ISetRuntime
		{
			if (value == null)
				throw new NotSupportedException ();

			value.OnSetRuntime (this);
			return value;
		}

		partial void SetValueManager (CreationOptions options);
		partial void SetMarshalMemberBuilder (CreationOptions options);

		static unsafe JavaVMInterface CreateInvoker (IntPtr handle)
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return (JavaVMInterface) Marshal.PtrToStructure (p, typeof (JavaVMInterface))!;
		}

		~JniRuntime ()
		{
			Dispose (false);
		}

		public virtual string? GetCurrentManagedThreadName ()
		{
			return null;
		}

		public virtual string? GetCurrentManagedThreadStackTrace (int skipFrames = 0, bool fNeedFileInfo = false)
		{
			return null;
		}

		public virtual void FailFast (string? message)
		{
			var m = typeof (Environment).GetMethod ("FailFast");

			if (m is null)
				Environment.Exit (1);

			m!.Invoke (null, new object?[]{ message });
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

			lock (Runtimes) {
				if (Runtimes.TryGetValue (InvocationPointer, out var vm) && vm == this) {
					Runtimes.Remove (InvocationPointer);
				}
			}

			if (disposing) {
				JniObjectReference.Dispose (ref ClassLoader);

				ClearTrackedReferences ();
				ValueManager.Dispose ();
				marshalMemberBuilder?.Dispose ();
				TypeManager.Dispose ();
				ObjectReferenceManager.Dispose ();

				var environments = JniEnvironment.Info.Values;
				for (int i = 0; i < environments.Count; ++i) {
					var e = environments [i];
					if (e.Runtime != this)
						continue;
					environments [i].Dispose ();
				}
			}

			if (DestroyRuntimeOnDispose) {
				DestroyRuntime ();
			}
			InvocationPointer   = IntPtr.Zero;
			Invoker             = default (JavaVMInterface);
		}

		public void AttachCurrentThread (string? name = null, JniObjectReference group = default (JniObjectReference))
		{
			var jnienv  = _AttachCurrentThread (name, group);
			JniEnvironment.SetEnvironmentPointer (jnienv, this);
		}

		internal    IntPtr  _AttachCurrentThread (string? name = null, JniObjectReference group = default (JniObjectReference))
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

		public virtual Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			return ValueManager.GetValue<Exception> (ref reference, options);
		}

		public int GlobalReferenceCount {
			get {return ObjectReferenceManager.GlobalReferenceCount;}
		}

		public int WeakGlobalReferenceCount {
			get {return ObjectReferenceManager.WeakGlobalReferenceCount;}
		}

		public JniObjectReferenceManager    ObjectReferenceManager      {get; private set;}
		public JniTypeManager               TypeManager                 {get; private set;}

		internal void Track (JniType value)
		{
			lock (TrackedInstances) {
				if (!TrackedInstances.ContainsKey (value.PeerReference.Handle))
					TrackedInstances [value.PeerReference.Handle] = value;
			}
		}

		internal void UnTrack (IntPtr key)
		{
			lock (TrackedInstances) {
				if (TrackedInstances.ContainsKey (key))
					TrackedInstances.Remove (key);
			}
		}

		void ClearTrackedReferences ()
		{
			List<IDisposable> values;
			lock (TrackedInstances) {
				values = new List<IDisposable> (TrackedInstances.Values);
				TrackedInstances.Clear ();
			}

			foreach (var d in values)
				d.Dispose ();
		}

		public virtual bool ExceptionShouldTransitionToJni (Exception e)
		{
			return !Debugger.IsAttached;
		}
	}

	partial class JniRuntime {

		public virtual void RaisePendingException (Exception pendingException)
		{
			JniEnvironment.Exceptions.Throw (pendingException);
		}
	}
}

