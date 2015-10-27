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


	public class JavaVMOptions {

		public  bool        TrackIDs                    {get; set;}
		public  bool        DestroyVMOnDispose          {get; set;}

		// Prefer JNIEnv::NewObject() over JNIEnv::AllocObject() + JNIEnv::CallNonvirtualVoidMethod()
		public  bool        NewObjectRequired           {get; set;}

		public  IntPtr                      InvocationPointer   {get; set;}
		public  IntPtr                      EnvironmentPointer  {get; set;}

		public  JniObjectReferenceManager   ObjectReferenceManager      {get; set;}

		public JavaVMOptions ()
		{
		}
	}

	public abstract partial class JavaVM : IDisposable
	{

		static ConcurrentDictionary<IntPtr, JavaVM>     JavaVMs = new ConcurrentDictionary<IntPtr, JavaVM> ();

		public static IEnumerable<JavaVM> GetRegisteredJavaVMs ()
		{
			return JavaVMs.Values;
		}

		public static JavaVM GetRegisteredJavaVM (IntPtr handle)
		{
			JavaVM vm;
			return JavaVMs.TryGetValue (handle, out vm)
				? vm
				: null;
		}

		static JavaVM current;
		public static JavaVM Current {
			get {
				if (current != null)
					return current;
				JavaVM  c       = null;
				int     count   = 0;
				foreach (var vm in JavaVMs.Values) {
					if (count++ == 0)
						c = vm;
				}
				if (count == 0)
					throw new InvalidOperationException ("No JavaVM has been created. Please use Java.Interop.JreVMBuilder.CreateJreVM().");
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} JavaVMs. Don't know which to use. Use JavaVM.SetCurrent().", count));
				return current = c;
			}
		}

		public static void SetCurrent (JavaVM newCurrent)
		{
			if (newCurrent == null)
				throw new ArgumentNullException ("newCurrent");
			JavaVMs.TryAdd (newCurrent.InvocationPointer, newCurrent);
			current = newCurrent;
		}

		ConcurrentDictionary<IntPtr, IDisposable>       TrackedInstances    = new ConcurrentDictionary<IntPtr, IDisposable> ();

		JavaVMInterface                                 Invoker;
		bool                                            DestroyVM;

		public  IntPtr                                  InvocationPointer   {get; private set;}

		public  bool                                    NewObjectRequired   {get; private set;}

		protected JavaVM (JavaVMOptions options)
		{
			if (options == null)
				throw new ArgumentNullException ("options");
			if (options.InvocationPointer == IntPtr.Zero)
				throw new ArgumentException ("options.InvocationPointer is null", "options");

			TrackIDs     = options.TrackIDs;
			DestroyVM    = options.DestroyVMOnDispose;

			ObjectReferenceManager      = options.ObjectReferenceManager ?? new JniObjectReferenceManager ();

			NewObjectRequired   = options.NewObjectRequired;

			InvocationPointer   = options.InvocationPointer;
			Invoker             = CreateInvoker (InvocationPointer);

			if (current == null)
				current = this;

			if (options.EnvironmentPointer != IntPtr.Zero) {
				var env = new JniEnvironment (options.EnvironmentPointer, this);
				JniEnvironment.SetRootEnvironment (env);
			}

			JavaVMs.TryAdd (InvocationPointer, this);

#if !XA_INTEGRATION
			ManagedPeer.Init ();
#endif  // !XA_INTEGRATION
		}

		static unsafe JavaVMInterface CreateInvoker (IntPtr handle)
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return (JavaVMInterface) Marshal.PtrToStructure (p, typeof (JavaVMInterface));
		}

		~JavaVM ()
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
		}

		protected virtual void Dispose (bool disposing)
		{
			if (InvocationPointer == IntPtr.Zero)
				return;

			if (current == this)
				current = null;

#if !XA_INTEGRATION
			foreach (var o in RegisteredInstances.Values) {
				var t = (IDisposable) o.Target;
				t.Dispose ();
			}
			RegisteredInstances.Clear ();
			ClearTrackedReferences ();
#endif  // !XA_INTEGRATION
			JavaVM _;
			JavaVMs.TryRemove (InvocationPointer, out _);
			ObjectReferenceManager.Dispose ();
			// TODO: Dispose JniEnvironment.RootEnvironments
			// Requires .NET 4.5+
			JniEnvironment.RootEnvironments.Dispose ();
			if (DestroyVM)
				DestroyJavaVM ();
			InvocationPointer    = IntPtr.Zero;
		}

		public JniEnvironment AttachCurrentThread (string name = null, JniObjectReference group = default (JniObjectReference))
		{
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
					throw new NotSupportedException ("AttachCurrentThread returned " + r);
				var env = new JniEnvironment (jnienv, this);
				return env;
			} finally {
				Marshal.FreeHGlobal (threadArgs.name);
			}
		}

		public void DestroyJavaVM ()
		{
			Invoker.DestroyJavaVM (InvocationPointer);
		}

		public virtual Exception GetExceptionForThrowable (ref JniObjectReference value, JniObjectReferenceOptions transfer)
		{
#if XA_INTEGRATION
			throw new NotSupportedException ("Do not know h ow to convert a JniObjectReference to a System.Exception!");
#else   // !XA_INTEGRATION
			var o   = PeekObject (value);
			var e   = o as JavaException;
			if (e != null) {
				JniEnvironment.References.Dispose (ref value, transfer);
				var p   = e as JavaProxyThrowable;
				if (p != null)
					return p.Exception;
				return e;
			}
			return GetObject<JavaException> (ref value, transfer);
#endif  // !̣XA_INTEGRATION
		}

		public int GlobalReferenceCount {
			get {return ObjectReferenceManager.GlobalReferenceCount;}
		}

		public int WeakGlobalReferenceCount {
			get {return ObjectReferenceManager.WeakGlobalReferenceCount;}
		}

		public JniObjectReferenceManager    ObjectReferenceManager      {get; private set;}

		public bool TrackIDs {get; private set;}

		internal void TrackID (IntPtr key, IDisposable value)
		{
			if (TrackIDs)
				TrackedInstances.TryAdd (key, value);
		}

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

#if !XA_INTEGRATION
	partial class JavaVM {

		Dictionary<int, WeakReference>  RegisteredInstances = new Dictionary<int, WeakReference>();

		public List<WeakReference> GetSurfacedObjects ()
		{
			lock (RegisteredInstances) {
				return RegisteredInstances.Values.ToList ();
			}
		}

		internal void RegisterObject<T> (T value)
			where T : IJavaPeerable, IJavaPeerableEx
		{
			var r = value.PeerReference;
			if (!r.IsValid)
				throw new ObjectDisposedException (value.GetType ().FullName);
			if (value.Registered)
				return;

			if (r.Type != JniObjectReferenceType.Global) {
				value.SetPeerReference (r.NewGlobalRef ());
				JniEnvironment.References.Dispose (ref r, JniObjectReferenceOptions.DisposeSourceReference);
			}
			int key = value.IdentityHashCode;
			lock (RegisteredInstances) {
				WeakReference   existing;
				IJavaPeerable     target;
				if (RegisteredInstances.TryGetValue (key, out existing) && (target = (IJavaPeerable) existing.Target) != null)
					throw new NotSupportedException (
							string.Format ("Cannot register instance {0}(0x{1}), as an instance with the same handle {2}(0x{3}) has already been registered.",
								value.GetType ().FullName, value.PeerReference.ToString (),
								target.GetType ().FullName, target.PeerReference.ToString ()));
				RegisteredInstances [key] = new WeakReference (value, trackResurrection: true);
			}
			value.Registered = true;
		}

		internal void UnRegisterObject (IJavaPeerableEx value)
		{
			int key = value.IdentityHashCode;
			lock (RegisteredInstances) {
				WeakReference               wv;
				IJavaPeerable                 t;
				if (RegisteredInstances.TryGetValue (key, out wv) &&
						(t = (IJavaPeerable) wv.Target) != null &&
						object.ReferenceEquals (value, t))
					RegisteredInstances.Remove (key);
				value.Registered = false;
			}
		}

		internal TCleanup SetObjectPeerReference<T, TCleanup> (T value, ref JniObjectReference reference, JniObjectReferenceOptions transfer, Func<Action, TCleanup> createCleanup)
			where T : IJavaPeerable, IJavaPeerableEx
			where TCleanup : IDisposable
		{
			if (!reference.IsValid)
				throw new ArgumentException ("handle is invalid.", nameof (reference));

			bool register   = reference.Flags == JniObjectReferenceFlags.Alloc;

			value.SetPeerReference (reference.NewLocalRef ());
			JniEnvironment.References.Dispose (ref reference, transfer);

			value.IdentityHashCode = JniSystem.IdentityHashCode (value.PeerReference);

			if (register) {
				RegisterObject (value);
				Action unregister = () => {
					UnRegisterObject (value);
					var o = value.PeerReference;
					value.SetPeerReference (o.NewLocalRef ());
					JniEnvironment.References.Dispose (ref o);
				};
				return createCleanup (unregister);
			}
			return createCleanup (null);
		}

		internal void DisposeObject<T> (T value)
			where T : IJavaPeerable, IJavaPeerableEx
		{
			var h = value.PeerReference;
			if (!h.IsValid)
				return;

			if (value.Registered)
				UnRegisterObject (value);
			value.Dispose (disposing: true);
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			var lref = value.PeerReference.SafeHandle as JniLocalReference;
			if (lref != null && !JniEnvironment.IsHandleValid (lref)) {
				// `lref` was created on another thread, and CANNOT be disposed on this thread.
				// Just invalidate the reference and move on.
				lref.SetHandleAsInvalid ();
			}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			JniEnvironment.References.Dispose (ref h);
			value.SetPeerReference (new JniObjectReference ());
			GC.SuppressFinalize (value);
		}

		internal void TryCollectObject<T> (T value)
			where T : IJavaPeerable, IJavaPeerableEx
		{
			var h = value.PeerReference;
			// MUST NOT use SafeHandle.ReferenceType: local refs are tied to a JniEnvironment
			// and the JniEnvironment's corresponding thread; it's a thread-local value.
			// Accessing SafeHandle.ReferenceType won't kill anything (so far...), but
			// instead it always returns JniReferenceType.Invalid.
			if (!h.IsValid || h.Type == JniObjectReferenceType.Local) {
				value.Dispose (disposing: false);
				value.SetPeerReference (new JniObjectReference ());
				return;
			}

			try {
				bool collected  = TryGC (value, ref h);
				if (collected) {
					value.SetPeerReference (new JniObjectReference ());
					if (value.Registered)
						UnRegisterObject (value);
					value.Dispose (disposing: false);
				} else {
					value.SetPeerReference (h);
					GC.ReRegisterForFinalize (value);
				}
			} catch (Exception e) {
				FailFast ("Unable to perform a GC! " + e);
			}
		}

		/// <summary>
		///   Try to garbage collect <paramref name="value"/>.
		/// </summary>
		/// <returns>
		///   <c>true</c>, if <paramref name="value"/> was collected and
		///   <paramref name="handle"/> is invalid; otherwise <c>false</c>.
		/// </returns>
		/// <param name="value">
		///   The <see cref="T:Java.Interop.IJavaPeerable"/> instance to collect.
		/// </param>
		/// <param name="handle">
		///   The <see cref="T:Java.Interop.JniObjectReference"/> of <paramref name="value"/>.
		///   This value may be updated, and <see cref="P:Java.Interop.IJavaObject.PeerReference"/>
		///   will be updated with this value.
		/// </param>
		internal protected abstract bool TryGC (IJavaPeerable value, ref JniObjectReference handle);

		public IJavaPeerable PeekObject (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return null;
			int key = JniSystem.IdentityHashCode (reference);
			lock (RegisteredInstances) {
				WeakReference               wv;
				if (RegisteredInstances.TryGetValue (key, out wv)) {
					IJavaPeerable   t = (IJavaPeerable) wv.Target;
					if (t != null)
						return t;
					RegisteredInstances.Remove (key);
				}
			}
			return null;
		}

		public IJavaPeerable GetObject (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType = null)
		{
			if (!reference.IsValid)
				return null;

			var existing = PeekObject (reference);
			if (existing != null && targetType != null && targetType.IsInstanceOfType (existing)) {
				JniEnvironment.References.Dispose (ref reference, transfer);
				return existing;
			}

			return CreateObjectWrapper (ref reference, transfer, targetType);
		}

		protected virtual IJavaPeerable CreateObjectWrapper (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType)
		{
			targetType  = targetType ?? typeof (JavaObject);
			if (!typeof (IJavaPeerable).IsAssignableFrom (targetType))
				throw new ArgumentException ("targetType must implement IJavaPeerable!", "targetType");

			var ctor = GetWrapperConstructor (reference, targetType);
			if (ctor == null)
				throw new NotSupportedException (string.Format ("Could not find an appropriate constructable wrapper type for Java type '{0}', targetType='{1}'.",
						JniEnvironment.Types.GetJniTypeNameFromInstance (reference), targetType));

			var acts = new object[] {
				reference,
				transfer,
			};
			try {
				return (IJavaPeerable) ctor.Invoke (acts);
			} finally {
				reference   = (JniObjectReference) acts [0];
			}
		}

		static  readonly    Type    ByRefJniObjectReference = typeof (JniObjectReference).MakeByRefType ();

		ConstructorInfo GetWrapperConstructor (JniObjectReference instance, Type fallbackType)
		{
			var klass       = JniEnvironment.Types.GetObjectClass (instance);
			var jniTypeName = JniEnvironment.Types.GetJniTypeNameFromClass (klass);

			Type type = null;
			while (jniTypeName != null) {
				type = GetTypeForJniTypeRefererence (jniTypeName);

				if (type != null) {
					var ctor    = type.GetConstructor (new[] {
						ByRefJniObjectReference,
						typeof(JniObjectReferenceOptions)
					});

					if (ctor != null) {
						JniEnvironment.References.Dispose (ref klass);
						return ctor;
					}
				}

				var super   = JniEnvironment.Types.GetSuperclass (klass);
				jniTypeName = super.IsValid
					? JniEnvironment.Types.GetJniTypeNameFromClass (super)
					: null;

				JniEnvironment.References.Dispose (ref klass, JniObjectReferenceOptions.DisposeSourceReference);
				klass      = super;
			}
			JniEnvironment.References.Dispose (ref klass, JniObjectReferenceOptions.DisposeSourceReference);

			return fallbackType.GetConstructor (new[] {
				ByRefJniObjectReference,
				typeof(JniObjectReferenceOptions)
			});
		}

		public T GetObject<T> (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			where T : IJavaPeerable
		{
			return (T) GetObject (ref reference, transfer, typeof (T));
		}

		public IJavaPeerable GetObject (IntPtr jniHandle, Type targetType = null)
		{
			if (jniHandle == IntPtr.Zero)
				return null;
			var h = new JniObjectReference (jniHandle);
			return GetObject (ref h, JniObjectReferenceOptions.CreateNewReference, targetType);
		}

		public T GetObject<T> (IntPtr jniHandle)
			where T : IJavaPeerable
		{
			return (T) GetObject (jniHandle, typeof(T));
		}
	}
#endif  // !XA_INTEGRATION

	partial class JavaVM {

		public JniTypeSignature GetJniTypeInfoForType (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (type.ContainsGenericParameters)
				throw new ArgumentException ("Generic type definitions are not supported.", "type");

			var originalType    = type;
			int rank            = 0;
			while (type.IsArray) {
				if (type.IsArray && type.GetArrayRank () > 1)
					throw new ArgumentException ("Multidimensional array '" + originalType.FullName + "' is not supported.", "type");
				rank++;
				type    = type.GetElementType ();
			}

			if (type.IsEnum)
				type = Enum.GetUnderlyingType (type);

#if !XA_INTEGRATION
			foreach (var mapping in JniBuiltinTypeNameMappings) {
				if (mapping.Key == type) {
					var r = mapping.Value;
					return r.AddArrayRank (rank);
				}
			}

			foreach (var mapping in JniBuiltinArrayMappings) {
				if (mapping.Key == type) {
					var r = mapping.Value;
					return r.AddArrayRank (rank);
				}
			}
#endif  // !XA_INTEGRATION

			var names = (JniTypeInfoAttribute[]) type.GetCustomAttributes (typeof (JniTypeInfoAttribute), inherit:false);
			if (names.Length != 0)
				return new JniTypeSignature (names [0].JniTypeName, names [0].ArrayRank + rank, names [0].TypeIsKeyword);

#if !XA_INTEGRATION
			if (type.IsGenericType) {
				var def = type.GetGenericTypeDefinition ();
				if (def == typeof(JavaArray<>) || def == typeof(JavaObjectArray<>)) {
					var r = GetJniTypeInfoForType (type.GetGenericArguments () [0]);
					return r.AddArrayRank (rank + 1);
				}
			}
#endif  // !XA_INTEGRATION
			return new JniTypeSignature (GetJniSimplifiedTypeReferenceForType (type), rank, false);
		}

		// Should be protected, but how then would we test?
		public virtual string GetJniSimplifiedTypeReferenceForType (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (type.IsArray)
				throw new ArgumentException ("Array type '" + type.FullName + "' is not supported.", "type");
			return null;
		}

		public virtual Type GetTypeForJniTypeRefererence (string jniTypeReference)
		{
			var info    = GetJniTypeInfoForJniTypeReference (jniTypeReference);
			if (info.SimpleReference == null)
				return null;
			var inner   = GetTypeForJniSimplifiedTypeReference (info.SimpleReference);
			if (inner == null)
				return null;
			var rank    = info.ArrayRank;
			var type    = inner;
#if XA_INTEGRATION
			if (rank > 0)
				throw new NotSupportedException ("Cannot handle arrays at this time.");
#else   // XA_INTEGRATION

			if (info.IsKeyword && rank > 0) {
				type = typeof(JavaPrimitiveArray<>).MakeGenericType (type);
				if (--rank == 0)
					return type;
			}
			while (rank-- > 0) {
				type = typeof (JavaObjectArray<>).MakeGenericType (type);
			}
#endif  // XA_INTEGRATION
			return type;
		}

		public JniTypeSignature GetJniTypeInfoForJniTypeReference (string jniTypeReference)
		{
			if (jniTypeReference == null)
				throw new ArgumentNullException ("jniTypeReference");
			int i = 0;
			int r = 0;
			var n = (string) null;
			var k = false;
			while (i < jniTypeReference.Length && jniTypeReference [i] == '[') {
				i++;
				r++;
			}
			switch (jniTypeReference [i]) {
			case 'B':
			case 'C':
			case 'D':
			case 'I':
			case 'F':
			case 'J':
			case 'S':
			case 'Z':
				if (jniTypeReference.Length - i > 1)
					n   = jniTypeReference.Substring (i);
				else {
					n   = jniTypeReference [i].ToString ();
					k   = true;
				}
				break;
			case 'L':
				int s = jniTypeReference.IndexOf (';', i);
				if (s >= i && s != jniTypeReference.Length-1)
					throw new ArgumentException (
							string.Format ("Malformed JNI type reference: trailing text after ';' in '{0}'.", jniTypeReference),
							"jniTypeReference");
				if (i == 0) {
					n   = s > i
						? jniTypeReference.Substring (i + 1, s - i - 1)
						: jniTypeReference;
				} else {
					if (s < i)
						throw new ArgumentException (
								string.Format ("Malformed JNI type reference; no terminating ';' for type ref: '{0}'.", jniTypeReference.Substring (i)),
								"jniTypeReference");
					if (s != jniTypeReference.Length - 1)
						throw new ArgumentException (
								string.Format ("Malformed jNI type reference: invalid trailing text: '{0}'.", jniTypeReference.Substring (i)),
								"jniTypeReference");
					n   = jniTypeReference.Substring (i + 1, s - i - 1);
				}
				break;
			default:
				if (i != 0)
					throw new ArgumentException (
							string.Format ("Malformed JNI type reference: found unrecognized char '{0}' in '{1}'.",
								jniTypeReference [i], jniTypeReference),
							"jniTypeReference");
				n   = jniTypeReference;
				break;
			}
			int bad = n.IndexOfAny (new[]{ '.', ';' });
			if (bad >= 0)
				throw new ArgumentException (
						string.Format ("Malformed JNI type reference: contains '{0}': {1}", n [bad], jniTypeReference),
						"jniTypeReference");
			return new JniTypeSignature (n, r, k);
		}

		public virtual Type GetTypeForJniSimplifiedTypeReference (string jniTypeReference)
		{
			if (jniTypeReference == null)
				throw new ArgumentNullException ("jniTypeReference");
			if (jniTypeReference != null && jniTypeReference.Contains ("."))
				throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", "jniTypeReference");
			if (jniTypeReference != null && jniTypeReference.StartsWith ("[", StringComparison.Ordinal))
				throw new ArgumentException ("Only simplified type references are supported.", "jniTypeReference");
			if (jniTypeReference != null && jniTypeReference.StartsWith ("L", StringComparison.Ordinal) && jniTypeReference.EndsWith (";", StringComparison.Ordinal))
				throw new ArgumentException ("Only simplified type references are supported.", "jniTypeReference");

#if !XA_INTEGRATION
			foreach (var mapping in JniBuiltinTypeNameMappings) {
				if (mapping.Value.SimpleReference == jniTypeReference)
					return mapping.Key;
			}
#endif  // !XA_INTEGRATION
			return null;
		}
	}

	partial class JavaVM {

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
				// because `je` may cross thread boundaries;
				// We'll need to rely on the GC to cleanup
				je.RegisterWithVM ();
			}
			JniEnvironment.Exceptions.Throw (je.PeerReference);
#endif  // !XA_INTEGRATION
		}
	}

#if !XA_INTEGRATION
	partial class JavaVM {

		public virtual JniMarshalInfo GetJniMarshalInfoForType (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");
			if (type.ContainsGenericParameters)
				throw new ArgumentException ("Generic type definitions are not supported.", "type");

			if (typeof (IJavaPeerable) == type)
				return DefaultObjectMarshaler;

			foreach (var marshaler in JniBuiltinMarshalers) {
				if (marshaler.Key == type)
					return marshaler.Value;
			}

			var listType = type.GetInterfaces ()
				.FirstOrDefault (i => i.IsGenericType && i.GetGenericTypeDefinition () == typeof (IList<>));
			if (listType != null) {
				var elementType = listType.GetGenericArguments () [0];
				if (elementType.IsValueType) {
					foreach (var marshaler in JniPrimitiveArrayMarshalers) {
						if (marshaler.Key == type)
							return marshaler.Value;
					}
				}
				var arrayType   = typeof (JavaObjectArray<>).MakeGenericType (elementType);
				var getValue    = CreateMethodDelegate<CreateValueFromJni> (arrayType, "GetValue");
				var createLRef  = CreateMethodDelegate<Func<object, JniObjectReference>> (arrayType, "CreateLocalRef");
				var createObj   = CreateMethodDelegate<Func<object, IJavaPeerable>> (arrayType, "CreateMarshalCollection");
				var cleanup     = CreateMethodDelegate<Action<IJavaPeerable, object>> (arrayType, "CleanupMarshalCollection");

				return new JniMarshalInfo {
					GetValueFromJni             = getValue,
					CreateLocalRef              = createLRef,
					CreateMarshalCollection     = createObj,
					CleanupMarshalCollection    = cleanup,
				};
			}

			if (typeof (IJavaPeerable).IsAssignableFrom (type)) {
				return DefaultObjectMarshaler;
			}
			return new JniMarshalInfo ();
		}

		static TDelegate CreateMethodDelegate<TDelegate>(Type type, string methodName)
			where TDelegate : class
		{
			return (TDelegate) (object) Delegate.CreateDelegate (
					typeof (TDelegate),
					type.GetMethod (methodName, BindingFlags.Static | BindingFlags.NonPublic));
		}

		static readonly JniMarshalInfo DefaultObjectMarshaler = new JniMarshalInfo {
			GetValueFromJni         = JavaPeerableExtensions.GetValue,
			CreateLocalRef          = JavaPeerableExtensions.CreateLocalRef,
		};
	}

	partial class JavaVM {

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

