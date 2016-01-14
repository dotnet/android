using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop.Expressions;

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
			var manager     = options.ValueManager;
			if (manager == null)
				throw new ArgumentException (
						"No JniValueManager specified in JniRuntime.CreationOptions.ValueManager.",
						nameof (options));
			ValueManager    = SetRuntime (manager);
		}

		public abstract partial class JniValueManager : ISetRuntime, IDisposable {

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
					if (t == null)
						continue;
					t.Dispose ();
				}
				RegisteredInstances.Clear ();
			}

			public abstract void WaitForGCBridgeProcessing ();

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
					if (RegisteredInstances.TryGetValue (key, out existing) && (target = (IJavaPeerable)existing.Target) != null)
						Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
								"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
								"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
								value.PeerReference.ToString (),
								key.ToString ("x"),
								RuntimeHelpers.GetHashCode (value).ToString ("x"),
								value.GetType ().FullName,
								JniEnvironment.Types.GetJniTypeNameFromInstance (value.PeerReference),
								target.PeerReference.ToString (),
								RuntimeHelpers.GetHashCode (target).ToString ("x"),
								target.GetType ().FullName,
								JniEnvironment.Types.GetJniTypeNameFromInstance (target.PeerReference));
					else
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

			internal protected virtual IJavaPeerable PeekObject (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return null;

				int key = JniSystem.IdentityHashCode (reference);

				WeakReference   wv;
				lock (RegisteredInstances) {
					if (!RegisteredInstances.TryGetValue (key, out wv)) {
						RegisteredInstances.Remove (key);
					}
				}
				return wv == null ? null : (IJavaPeerable) wv.Target;
			}

			public object PeekValue (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return null;

				var t   = PeekObject (reference);
				var b   = Unbox (t);
				if (b != null)
					return b;
				return t;
			}

			static object Unbox (IJavaPeerable value)
			{
				var p   = value as JavaProxyObject;
				if (p != null)
					return p.Value;
				var x   = value as JavaProxyThrowable;
				if (x != null)
					return x.Exception;
				return null;
			}

			object PeekBoxedObject (JniObjectReference reference)
			{
				var t   = PeekObject (reference);
				return Unbox (t);
			}

			static  readonly    KeyValuePair<Type, Type>[]      WrapperTypeMappings = new []{
				new KeyValuePair<Type, Type>(typeof (object),           typeof (JavaObject)),
				new KeyValuePair<Type, Type>(typeof (IJavaPeerable),    typeof (JavaObject)),
				new KeyValuePair<Type, Type>(typeof (Exception),        typeof (JavaException)),
			};

			static Type GetWrapperType (Type type)
			{
				foreach (var m in WrapperTypeMappings) {
					if (m.Key == type)
						return m.Value;
				}
				return type;
			}

			internal protected virtual IJavaPeerable CreateObject (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType)
			{
				targetType  = targetType ?? typeof (JavaObject);
				targetType  = GetWrapperType (targetType);

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
					type = Runtime.TypeManager.GetType (JniTypeSignature.Parse (jniTypeName));

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


			public object CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
			{
				if (!reference.IsValid)
					return null;

				if (targetType != null && typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ())) {
					return JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
				}

				var boxed   = PeekBoxedObject (reference);
				if (boxed != null) {
					JniObjectReference.Dispose (ref reference, options);
					if (targetType != null)
						return Convert.ChangeType (boxed, targetType);
					return boxed;
				}

				targetType = targetType ?? GetRuntimeType (reference);
				if (targetType == null) {
					// Let's hope this is an IJavaPeerable!
					return JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
				}
				var marshaler   = GetValueMarshaler (targetType);
				return marshaler.CreateValue (ref reference, options, targetType);
			}

			public T CreateValue<T> (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
			{
				if (!reference.IsValid)
					return default (T);

				if (targetType != null && !typeof (T).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ()))
					throw new ArgumentException (
							string.Format ("Requested runtime '{0}' value of '{1}' is not compatible with requested compile-time type T of '{2}'.",
								nameof (targetType),
								targetType,
								typeof (T)),
							nameof (targetType));

				var boxed   = PeekBoxedObject (reference);
				if (boxed != null) {
					JniObjectReference.Dispose (ref reference, options);
					return (T) Convert.ChangeType (boxed, targetType ?? typeof (T));
				}

				targetType  = targetType ?? typeof (T);

				if (typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ())) {
					return (T) JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
				}

				var marshaler   = GetValueMarshaler<T> ();
				return marshaler.CreateGenericValue (ref reference, options, targetType);
			}

			internal Type GetRuntimeType (JniObjectReference reference)
			{
				var signature   = JniTypeSignature.Parse (JniEnvironment.Types.GetJniTypeNameFromInstance (reference));
				return Runtime.TypeManager.GetType (signature);
			}

			public object GetValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
			{
				if (!reference.IsValid)
					return null;

				var existing = PeekValue (reference);
				if (existing != null && (targetType == null || targetType.GetTypeInfo ().IsAssignableFrom (existing.GetType ().GetTypeInfo ()))) {
					JniObjectReference.Dispose (ref reference, options);
					return existing;
				}

				if (targetType != null && typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ())) {
					return JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
				}

				targetType = targetType ?? GetRuntimeType (reference);
				if (targetType == null) {
					// Let's hope this is an IJavaPeerable!
					return JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
				}
				var marshaler   = GetValueMarshaler (targetType);
				return marshaler.CreateValue (ref reference, options, targetType);
			}

			public T GetValue<T> (IntPtr handle)
			{
				var r   = new JniObjectReference (handle);
				return GetValue<T> (ref r, JniObjectReferenceOptions.Copy);
			}

			public T GetValue<T> (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType = null)
			{
				if (!reference.IsValid)
					return default (T);

				if (targetType != null && !typeof (T).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ()))
					throw new ArgumentException (
							string.Format ("Requested runtime '{0}' value of '{1}' is not compatible with requested compile-time type T of '{2}'.",
								nameof (targetType),
								targetType,
								typeof (T)),
							nameof (targetType));

				targetType  = targetType ?? typeof (T);

				var existing = PeekValue (reference);
				if (existing != null && (targetType == null || targetType.GetTypeInfo ().IsAssignableFrom (existing.GetType ().GetTypeInfo ()))) {
					JniObjectReference.Dispose (ref reference, options);
					return (T) existing;
				}

				if (typeof (IJavaPeerable).GetTypeInfo ().IsAssignableFrom (targetType.GetTypeInfo ())) {
					return (T) JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
				}

				var marshaler   = GetValueMarshaler<T> ();
				return marshaler.CreateGenericValue (ref reference, options, targetType);
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

				var marshalerAttr   = info.GetCustomAttribute<JniValueMarshalerAttribute> ();
				if (marshalerAttr != null)
					return (JniValueMarshaler) Activator.CreateInstance (marshalerAttr.MarshalerType);

				if (typeof (IJavaPeerable) == type)
					return JavaPeerableValueMarshaler.Instance;

				if (typeof (void) == type)
					return VoidValueMarshaler.Instance;

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
		}
	}

	class VoidValueMarshaler : JniValueMarshaler {

		internal    static  VoidValueMarshaler              Instance    = new VoidValueMarshaler ();

		public override Type MarshalType {
			get {return typeof (void);}
		}

		public override object CreateValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			throw new NotSupportedException ();
		}

		public override JniValueMarshalerState CreateObjectReferenceArgumentState (object value, ParameterAttributes synchronize)
		{
			throw new NotSupportedException ();
		}

		public override void DestroyArgumentState (object value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			throw new NotSupportedException ();
		}
	}

	class JavaPeerableValueMarshaler : JniValueMarshaler<IJavaPeerable> {

		internal    static  JavaPeerableValueMarshaler      Instance    = new JavaPeerableValueMarshaler ();

		public override IJavaPeerable CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			var jvm         = JniEnvironment.Runtime;
			var marshaler   = jvm.ValueManager.GetValueMarshaler (targetType ?? typeof(IJavaPeerable));
			if (marshaler != Instance)
				return (IJavaPeerable) marshaler.CreateValue (ref reference, options, targetType);
			return jvm.ValueManager.CreateObject (ref reference, options, targetType);
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

		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			var r   = Expression.Variable (typeof (JniObjectReference), sourceValue.Name + "_ref");
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (
					Expression.IfThenElse (
						test:       Expression.Equal (Expression.Constant (null), sourceValue),
						ifTrue:     Expression.Assign (r, Expression.New (typeof (JniObjectReference))),
						ifFalse:    Expression.Assign (r, Expression.Property (sourceValue, "PeerReference"))));
			context.CleanupStatements.Add (DisposeObjectReference (r));

			var h   = Expression.Variable (typeof (IntPtr), sourceValue + "_handle");
			context.LocalVariables.Add (h);
			context.CreationStatements.Add (Expression.Assign (h, Expression.Property (r, "Handle")));
			return h;
		}

		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			CreateParameterFromManagedExpression (context, sourceValue, 0);
			var r   = context.LocalVariables [sourceValue + "_ref"];
			return ReturnObjectReferenceToJni (context, sourceValue.Name, r);
		}

		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type targetType)
		{
			var r   = Expression.Variable (targetType, sourceValue + "_val");
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (
					Expression.Assign (r,
						Expression.Call (
							Expression.Property (context.Runtime, "ValueManager"),
							"GetValue",
							new[]{targetType},
							sourceValue)));
			return r;
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
			return ValueMarshaler.CreateObjectReferenceArgumentState (value, synchronize);
		}

		public override void DestroyGenericArgumentState (T value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			ValueMarshaler.DestroyArgumentState (value, ref state, synchronize);
		}

		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			return ValueMarshaler.CreateParameterFromManagedExpression (context, sourceValue, synchronize);
		}

		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type targetType)
		{
			return ValueMarshaler.CreateParameterToManagedExpression (context, sourceValue, synchronize, targetType);
		}

		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return ValueMarshaler.CreateReturnValueFromManagedExpression (context, sourceValue);
		}
	}

	class ProxyValueMarshaler : JniValueMarshaler<object> {

		internal    static  ProxyValueMarshaler     Instance    = new ProxyValueMarshaler ();

		public override object CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			var jvm     = JniEnvironment.Runtime;

			if (targetType == null || targetType == typeof (object)) {
				targetType      = jvm.ValueManager.GetRuntimeType (reference);
			}
			if (targetType != null) {
				var vm  = jvm.ValueManager.GetValueMarshaler (targetType);
				if (vm != Instance) {
					return vm.CreateValue (ref reference, options, targetType);
				}
			}

			var target  = jvm.ValueManager.PeekValue (reference);
			if (target != null) {
				JniObjectReference.Dispose (ref reference, options);
				return target;
			}
			// Punt! Hope it's a java.lang.Object
			return jvm.ValueManager.CreateObject (ref reference, options, targetType);
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

