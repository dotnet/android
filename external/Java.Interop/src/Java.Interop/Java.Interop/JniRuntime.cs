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

			public  IntPtr                      InvocationPointer           {get; set;}
			public  IntPtr                      EnvironmentPointer          {get; set;}

			public  JniObjectReference          ClassLoader                 {get; set;}
			public  IntPtr                      ClassLoader_LoadClass_id    {get; set;}

			public  JniObjectReferenceManager   ObjectReferenceManager      {get; set;}
			public  JniTypeManager              TypeManager                 {get; set;}

			public CreationOptions ()
			{
			}
		}
	}

	partial class JniRuntime {

		interface ISetRuntime {
			void SetRuntime (JniRuntime runtime);
		}
	}

	public partial class JniRuntime : IDisposable
	{

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

		static JniRuntime current;
		public static JniRuntime Current {
			get {
				var c   = current;
				if (c != null)
					return c;
				int     count   = 0;
				foreach (var vm in Runtimes.Values) {
					if (count++ == 0)
						c = vm;
				}
				if (count == 0)
					throw new InvalidOperationException ("No JavaVM has been created. Please use Java.Interop.JreRuntimeBuilder.CreateJreRuntime().");
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} Java Runtimes. Don't know which to use. Use JniRuntime.SetCurrent().", count));
				Interlocked.CompareExchange (ref current, c, null);
				return c;
			}
		}

		public static void SetCurrent (JniRuntime newCurrent)
		{
			if (newCurrent == null)
				throw new ArgumentNullException ("newCurrent");
			Runtimes.TryAdd (newCurrent.InvocationPointer, newCurrent);
			current = newCurrent;
			Thread.MemoryBarrier ();
		}

		ConcurrentDictionary<IntPtr, IDisposable>       TrackedInstances    = new ConcurrentDictionary<IntPtr, IDisposable> ();

		JavaVMInterface                                 Invoker;
		bool                                            DestroyRuntimeOnDispose;

		internal    JniObjectReference                  ClassLoader;
		internal    JniInstanceMethodInfo               ClassLoader_LoadClass;

		public  IntPtr                                  InvocationPointer   {get; private set;}

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

			ObjectReferenceManager      = SetRuntime (options.ObjectReferenceManager ?? new JniObjectReferenceManager ());
			TypeManager                 = SetRuntime (options.TypeManager ?? new JniTypeManager ());

			SetValueMarshaler (options);

			NewObjectRequired   = options.NewObjectRequired;

			InvocationPointer   = options.InvocationPointer;
			Invoker             = CreateInvoker (InvocationPointer);

			Interlocked.CompareExchange (ref current, this, null);

			Runtimes.TryAdd (InvocationPointer, this);

			if (options.EnvironmentPointer != IntPtr.Zero) {
				var env = new JniEnvironmentInfo (options.EnvironmentPointer, this);
				JniEnvironment.SetEnvironmentInfo (env);
			}

#if !XA_INTEGRATION
			ManagedPeer.Init ();
#endif  // !XA_INTEGRATION

			ClassLoader = options.ClassLoader;
			if (options.ClassLoader_LoadClass_id != IntPtr.Zero) {
				ClassLoader_LoadClass   = new JniInstanceMethodInfo (options.ClassLoader_LoadClass_id);
			}

			if (ClassLoader.IsValid) {
				ClassLoader = ClassLoader.NewGlobalRef ();
			}

			if (!ClassLoader.IsValid || ClassLoader_LoadClass == null) {
				using (var t = new JniType ("java/lang/ClassLoader")) {
					if (!ClassLoader.IsValid) {
						var m       = t.GetStaticMethod ("getSystemClassLoader", "()Ljava/lang/ClassLoader;");
						var loader  = m.InvokeObjectMethod (t.PeerReference);
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
			where T : ISetRuntime
		{
			value.SetRuntime (this);
			return value;
		}

		partial void SetValueMarshaler (CreationOptions options);

		static unsafe JavaVMInterface CreateInvoker (IntPtr handle)
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return (JavaVMInterface) Marshal.PtrToStructure (p, typeof (JavaVMInterface));
		}

		~JniRuntime ()
		{
			Dispose (false);
		}

		public virtual void FailFast (string message)
		{
			var t = typeof (Environment);
			var m = t.GetMethod ("FailFast");
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

			JniObjectReference.Dispose (ref ClassLoader);

#if !XA_INTEGRATION
			ValueMarshaler.Dispose ();
#endif  // !XA_INTEGRATION
			ClearTrackedReferences ();
			JniRuntime _;
			Runtimes.TryRemove (InvocationPointer, out _);
			ObjectReferenceManager.Dispose ();
			if (DestroyRuntimeOnDispose)
				DestroyRuntime ();
			InvocationPointer    = IntPtr.Zero;
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
				version = JniVersion.v1_2,
			};
			try {
				if (name != null)
					threadArgs.name = Marshal.StringToHGlobalAnsi (name);
				if (group.IsValid)
					threadArgs.group = group.Handle;
				IntPtr jnienv;
				int r = Invoker.AttachCurrentThread (InvocationPointer, out jnienv, ref threadArgs);
				if (r != 0)
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

		public virtual Exception GetExceptionForThrowable (ref JniObjectReference value, JniObjectReferenceOptions transfer)
		{
#if XA_INTEGRATION
			throw new NotSupportedException ("Do not know h ow to convert a JniObjectReference to a System.Exception!");
#else   // !XA_INTEGRATION
			var o   = ValueMarshaler.PeekObject (value);
			var e   = o as JavaException;
			if (e != null) {
				JniObjectReference.Dispose (ref value, transfer);
				var p   = e as JavaProxyThrowable;
				if (p != null)
					return p.Exception;
				return e;
			}
			return ValueMarshaler.GetObject<JavaException> (ref value, transfer);
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

#if !XA_INTEGRATION

	partial class JniRuntime {

		static IExportedMemberBuilder memberBuilder;
		public virtual IExportedMemberBuilder ExportedMemberBuilder {
			get {
				if (memberBuilder != null)
					return memberBuilder;
				var jie = Assembly.Load ("Java.Interop.Export");
				var t   = jie.GetType ("Java.Interop.ExportedMemberBuilder");
				var b   = (IExportedMemberBuilder) Activator.CreateInstance (t, this);
				if (Interlocked.CompareExchange (ref memberBuilder, b, null) != null) {
					// do nothing; GC will collect
				}
				return memberBuilder;
			}
		}
	}
#endif  // !XA_INTEGRATION
}

