using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Java.Interop
{
	partial class JniRuntime
	{
		partial class CreationOptions {
			public  JniValueManager         ValueManager                {get; set;}
		}

		public  JniValueManager             ValueManager                {get; private set;}

		partial void SetValueManager (CreationOptions options)
		{
			ValueManager  = SetRuntime (options.ValueManager ?? new JniValueManager ());
		}

		public partial class JniValueManager : ISetRuntime, IDisposable {

			public      JniRuntime  Runtime { get; private set; }

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				Runtime = runtime;
			}

			public void Dispose ()
			{
				Dispose (true);
				GC.SuppressFinalize (this);
			}

			protected virtual void Dispose (bool disposing)
			{
				if (!disposing)
					return;

				if (RegisteredInstances == null)
					return;

				foreach (var o in RegisteredInstances.Values) {
					var t = (IDisposable) o.Target;
					t.Dispose ();
				}
				RegisteredInstances.Clear ();
			}

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
					JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
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

			internal void UnRegisterObject<T> (T value)
				where T : IJavaPeerable, IJavaPeerableEx
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

			internal TCleanup SetObjectPeerReference<T, TCleanup> (T value, ref JniObjectReference reference, JniObjectReferenceOptions options, Func<Action, TCleanup> createCleanup)
				where T : IJavaPeerable, IJavaPeerableEx
				where TCleanup : IDisposable
			{
				if (!reference.IsValid)
					throw new ArgumentException ("handle is invalid.", nameof (reference));

				var newRef      = reference;
				if ((options & JniObjectReferenceOptions.Copy) == JniObjectReferenceOptions.Copy) {
					newRef  = reference.NewGlobalRef ();
				}
				value.SetPeerReference (newRef);
				JniObjectReference.Dispose (ref reference, options);

				value.IdentityHashCode = JniSystem.IdentityHashCode (newRef);

				var o = Runtime.ObjectReferenceManager;
				if (o.LogGlobalReferenceMessages) {
					o.WriteGlobalReferenceLine ("Created PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}, Java.Type={4}",
							newRef.ToString (),
							value.IdentityHashCode.ToString ("x"),
							RuntimeHelpers.GetHashCode (value).ToString ("x"),
							value.GetType ().FullName,
							JniEnvironment.Types.GetJniTypeNameFromInstance (newRef));
				}

				if ((options & DoNotRegisterTarget) != DoNotRegisterTarget) {
					RegisterObject (value);
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

				var o = Runtime.ObjectReferenceManager;
				if (o.LogGlobalReferenceMessages) {
					o.WriteGlobalReferenceLine ("Disposing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3} Java.Type={4}",
							h.ToString (),
							value.IdentityHashCode.ToString ("x"),
							RuntimeHelpers.GetHashCode (value).ToString ("x"),
							value.GetType ().ToString (),
							JniEnvironment.Types.GetJniTypeNameFromInstance (h));
				}

				value.Dispose (disposing: true);
				#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				var lref = value.PeerReference.SafeHandle as JniLocalReference;
				if (lref != null && !JniEnvironment.IsHandleValid (lref)) {
					// `lref` was created on another thread, and CANNOT be disposed on this thread.
					// Just invalidate the reference and move on.
					lref.SetHandleAsInvalid ();
				}
				#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				JniObjectReference.Dispose (ref h);
				value.SetPeerReference (new JniObjectReference ());
				GC.SuppressFinalize (value);
			}

			internal void TryCollectObject<T> (T value)
				where T : IJavaPeerable, IJavaPeerableEx
			{
				var h = value.PeerReference;
				var o = Runtime.ObjectReferenceManager;
				// MUST NOT use SafeHandle.ReferenceType: local refs are tied to a JniEnvironment
				// and the JniEnvironment's corresponding thread; it's a thread-local value.
				// Accessing SafeHandle.ReferenceType won't kill anything (so far...), but
				// instead it always returns JniReferenceType.Invalid.
				if (!h.IsValid || h.Type == JniObjectReferenceType.Local) {
					if (o.LogGlobalReferenceMessages) {
						o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
								h.ToString (),
								value.IdentityHashCode.ToString ("x"),
								RuntimeHelpers.GetHashCode (value).ToString ("x"),
								value.GetType ().ToString ());
					}
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
						if (o.LogGlobalReferenceMessages) {
							o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
									h.ToString (),
									value.IdentityHashCode.ToString ("x"),
									RuntimeHelpers.GetHashCode (value).ToString ("x"),
									value.GetType ().ToString ());
						}
						value.Dispose (disposing: false);
					} else {
						value.SetPeerReference (h);
						GC.ReRegisterForFinalize (value);
					}
				} catch (Exception e) {
					Runtime.FailFast ("Unable to perform a GC! " + e);
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
			internal protected virtual bool TryGC (IJavaPeerable value, ref JniObjectReference handle)
			{
				if (!handle.IsValid)
					return true;
				var wgref = handle.NewWeakGlobalRef ();
				JniObjectReference.Dispose (ref handle);
				JniGC.Collect ();
				handle = wgref.NewGlobalRef ();
				JniObjectReference.Dispose (ref wgref);
				return !handle.IsValid;
			}

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
				if (existing != null && (targetType == null || targetType.GetTypeInfo ().IsAssignableFrom (existing.GetType ().GetTypeInfo ()))) {
					JniObjectReference.Dispose (ref reference, transfer);
					return existing;
				}

				return CreateObjectWrapper (ref reference, transfer, targetType);
			}

			protected virtual IJavaPeerable CreateObjectWrapper (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType)
			{
				targetType  = targetType ?? typeof (JavaObject);
				if (!typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ()))
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
					type = Runtime.TypeManager.GetType (Runtime.TypeManager.GetTypeSignature (jniTypeName));

					if (type != null) {
						var ctor = GetActivationConstructor (type);

						if (ctor != null) {
							JniObjectReference.Dispose (ref klass);
							return ctor;
						}
					}

					var super   = JniEnvironment.Types.GetSuperclass (klass);
					jniTypeName = super.IsValid
						? JniEnvironment.Types.GetJniTypeNameFromClass (super)
						: null;

					JniObjectReference.Dispose (ref klass, JniObjectReferenceOptions.CopyAndDispose);
					klass      = super;
				}
				JniObjectReference.Dispose (ref klass, JniObjectReferenceOptions.CopyAndDispose);

				return GetActivationConstructor (fallbackType);
			}

			static ConstructorInfo GetActivationConstructor (Type type)
			{
				return
					(from c in type.GetTypeInfo ().DeclaredConstructors
					 let p = c.GetParameters ()
					 where p.Length == 2 && p [0].ParameterType == ByRefJniObjectReference && p [1].ParameterType == typeof (JniObjectReferenceOptions)
					 select c)
				.FirstOrDefault ();
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
				return GetObject (ref h, JniObjectReferenceOptions.Copy, targetType);
			}

			public T GetObject<T> (IntPtr jniHandle)
				where T : IJavaPeerable
			{
				return (T) GetObject (jniHandle, typeof(T));
			}

			public object GetValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
			{
				if (!reference.IsValid)
					return null;

				var target  = PeekObject (reference);
				var proxy   = target as JavaProxyObject;
				if (proxy != null) {
					JniObjectReference.Dispose (ref reference, options);
					return proxy.Value;
				}

				if (target != null && (targetType == null || targetType.GetTypeInfo ().IsAssignableFrom (target.GetType ().GetTypeInfo ()))) {
					JniObjectReference.Dispose (ref reference, options);
					return target;
				}

				if (targetType == null) {
					var signature   = Runtime.TypeManager.GetTypeSignature (JniEnvironment.Types.GetJniTypeNameFromInstance (reference));
					targetType      = Runtime.TypeManager.GetType (signature);
				}
				var vm  = GetValueMarshaler (targetType);
				return vm.CreateValue (ref reference, options, targetType);
			}

			public T GetValue<T> (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
			{
				if (!reference.IsValid)
					return default (T);

				var target  = PeekObject (reference);
				var proxy   = target as JavaProxyObject;
				if (proxy != null) {
					JniObjectReference.Dispose (ref reference, options);
					return (T) proxy.Value;
				}

				if (target is T) {
					JniObjectReference.Dispose (ref reference, options);
					return (T) target;
				}

				var vm  = GetValueMarshaler<T> ();
				return vm.CreateGenericValue (ref reference, options, typeof (T));
			}

			Dictionary<Type, JniValueMarshaler> Marshalers = new Dictionary<Type, JniValueMarshaler> ();

			public JniValueMarshaler<T> GetValueMarshaler<T>()
			{
				var m   = GetValueMarshaler (typeof (T));
				var r   = m as JniValueMarshaler<T>;
				if (r != null)
					return r;
				lock (Marshalers) {
					JniValueMarshaler d;
					if (Marshalers.TryGetValue (typeof (T), out d))
						return (JniValueMarshaler<T>) d;
					Marshalers.Add (typeof (T), d = new DelegatingValueMarshaler<T> (m));
					return (JniValueMarshaler<T>) d;
				}
			}

			public JniValueMarshaler GetValueMarshaler (Type type)
			{
				if (type == null)
					throw new ArgumentNullException ("type");
				var info = type.GetTypeInfo ();
				if (info.ContainsGenericParameters)
					throw new ArgumentException ("Generic type definitions are not supported.", "type");

				if (typeof (IJavaPeerable) == type)
					return JavaPeerableValueMarshaler.Instance;

				foreach (var marshaler in JniBuiltinMarshalers) {
					if (marshaler.Key == type)
						return marshaler.Value;
				}

				var listIface   = typeof(IList<>).GetTypeInfo ();
				var listType    =
					(from iface in info.ImplementedInterfaces.Concat (new[]{type})
					 let iinfo = iface.GetTypeInfo ()
					 where (listIface).IsAssignableFrom (iinfo.IsGenericType ? iinfo.GetGenericTypeDefinition ().GetTypeInfo () : iinfo)
					 select iinfo)
					.FirstOrDefault ();
				if (listType != null) {
					var elementType = listType.GenericTypeArguments [0];
					if (elementType.GetTypeInfo ().IsValueType) {
						foreach (var marshaler in JniPrimitiveArrayMarshalers) {
							if (info.IsAssignableFrom (marshaler.Key.GetTypeInfo ()))
								return marshaler.Value;
						}
					}

					return (JniValueMarshaler) Activator.CreateInstance (typeof (JavaObjectArray<>.ValueMarshaler).MakeGenericType (elementType));
				}

				if (typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (info)) {
					return JavaPeerableValueMarshaler.Instance;
				}
				return GetValueMarshalerCore (type);
			}

			protected virtual JniValueMarshaler GetValueMarshalerCore (Type type)
			{
				return ProxyValueMarshaler.Instance;
			}

			static TDelegate CreateMethodDelegate<TDelegate>(Type type, string methodName)
				where TDelegate : class
			{
				return (TDelegate) (object) type.GetTypeInfo ().GetDeclaredMethod (methodName).CreateDelegate (typeof (TDelegate));
			}
		}
	}

	class JavaPeerableValueMarshaler : JniValueMarshaler<IJavaPeerable> {

		internal    static  JavaPeerableValueMarshaler      Instance    = new JavaPeerableValueMarshaler ();

		public override IJavaPeerable CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			return JniEnvironment.Runtime.ValueManager.GetObject (ref reference, options, targetType);
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (IJavaPeerable value, ParameterAttributes synchronize)
		{
			if (value == null || !value.PeerReference.IsValid)
				return new JniValueMarshalerState ();
			var r   = value.PeerReference.NewLocalRef ();
			return new JniValueMarshalerState (r);
		}

		public override void DestroyGenericArgumentState (IJavaPeerable value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			var r   = state.ReferenceValue;
			JniObjectReference.Dispose (ref r);
			state   = new JniValueMarshalerState ();
		}
	}

	class DelegatingValueMarshaler<T> : JniValueMarshaler<T> {

		JniValueMarshaler   ValueMarshaler;

		public DelegatingValueMarshaler (JniValueMarshaler valueMarshaler)
		{
			ValueMarshaler  = valueMarshaler;
		}

		public override T CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			return (T) ValueMarshaler.CreateValue (ref reference, options, targetType ?? typeof (T));
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (T value, ParameterAttributes synchronize)
		{
			System.Diagnostics.Debug.WriteLine ("# jonp: DelegatingValueMarshaler.CreateGenericObjectReferenceArgumentState: ValueMarshaler={0}; synchronize={1}", ValueMarshaler.GetType (), synchronize);
			return ValueMarshaler.CreateObjectReferenceArgumentState (value, synchronize);
		}

		public override void DestroyGenericArgumentState (T value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			ValueMarshaler.DestroyArgumentState (value, ref state, synchronize);
		}
	}

	class ProxyValueMarshaler : JniValueMarshaler<object> {

		internal    static  ProxyValueMarshaler     Instance    = new ProxyValueMarshaler ();

		public override object CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			var jvm     = JniEnvironment.Runtime;

			if (targetType == null || targetType == typeof (object)) {
				var signature   = jvm.TypeManager.GetTypeSignature (JniEnvironment.Types.GetJniTypeNameFromInstance (reference));
				targetType      = jvm.TypeManager.GetType (signature);
			}
			if (targetType != null) {
				var vm  = jvm.ValueManager.GetValueMarshaler (targetType);
				if (vm != Instance) {
					return vm.CreateValue (ref reference, options, targetType);
				}
			}

			var target  = jvm.ValueManager.PeekObject (reference);

			JniObjectReference.Dispose (ref reference, options);

			var proxy   = target as JavaProxyObject;
			if (proxy != null) {
				JniObjectReference.Dispose (ref reference, options);
				return proxy.Value;
			}
			return null;
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState (object value, ParameterAttributes synchronize)
		{
			if (value == null)
				return new JniValueMarshalerState ();

			var jvm     = JniEnvironment.Runtime;

			var vm      = jvm.ValueManager.GetValueMarshaler (value.GetType ());
			if (vm != Instance) {
				var s   = vm.CreateObjectReferenceArgumentState (value, synchronize);
				return new JniValueMarshalerState (s, vm);
			}

			var p   = JavaProxyObject.GetProxy (value);
			return new JniValueMarshalerState (p.PeerReference.NewLocalRef ());
		}

		public override void DestroyGenericArgumentState (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			var vm  = state.Extra as JniValueMarshaler;
			if (vm != null) {
				vm.DestroyArgumentState (value, ref state, synchronize);
				return;
			}
			var r   = state.ReferenceValue;
			JniObjectReference.Dispose (ref r);
			state = new JniValueMarshalerState ();
		}
	}

	static class JavaLangRuntime {
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/Runtime");}
		}

		static JniMethodInfo _getRuntime;
		internal static JniObjectReference GetRuntime ()
		{
			TypeRef.GetCachedStaticMethod (ref _getRuntime, "getRuntime", "()Ljava/lang/Runtime;");
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (TypeRef.PeerReference, _getRuntime);
		}

		static JniMethodInfo _gc;
		internal static void GC (JniObjectReference runtime)
		{
			TypeRef.GetCachedInstanceMethod (ref _gc, "gc", "()V");
			JniEnvironment.InstanceMethods.CallVoidMethod (runtime, _gc);
		}
	}

	static class JniGC {

		internal static void Collect ()
		{
			var runtime = JavaLangRuntime.GetRuntime ();
			try {
				JavaLangRuntime.GC (runtime);
			} finally {
				JniObjectReference.Dispose (ref runtime);
			}
		}
	}
}

