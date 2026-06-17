#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Java.Interop.Expressions;

namespace Java.Interop
{
	partial class JniRuntime
	{
		[RequiresDynamicCode ("This JniValueManager implementation is not compatible with Native AOT. Use a different JniValueManager implementation that supports Native AOT.")]
		[RequiresUnreferencedCode ("This JniValueManager implementation is not compatible with Native AOT. Use a different JniValueManager implementation that supports Native AOT.")]
		public abstract partial class ReflectionJniValueManager : JniValueManager
		{
			public override void ActivatePeer (
				JniObjectReference reference,
				[DynamicallyAccessedMembers (Constructors)] Type type,
				ConstructorInfo cinfo,
				object?[]? argumentValues)
			{
				try {
					var self = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
					self.SetPeerReference (reference);
					cinfo.Invoke (self, argumentValues);
				} catch (Exception e) {
					var m = string.Format (
							CultureInfo.InvariantCulture,
							"Could not activate {{ PeerReference={0} IdentityHashCode=0x{1} Java.Type={2} }} for managed type '{3}'.",
							reference,
							GetJniIdentityHashCode (reference).ToString ("x", CultureInfo.InvariantCulture),
							JniEnvironment.Types.GetJniTypeNameFromInstance (reference),
							type.FullName);
					Debug.WriteLine (m);

					throw new NotSupportedException (m, e);
				}
			}

			protected override void ConstructPeerCore (IJavaPeerable peer, ref JniObjectReference reference, JniObjectReferenceOptions options)
			{
				if (peer == null)
					throw new ArgumentNullException (nameof (peer));

				var newRef  = peer.PeerReference;
				if (newRef.IsValid) {
					JniObjectReference.Dispose (ref reference, options);

					// Activation? See ManagedPeer.Construct, CreatePeer
					// Instance was already added, don't add again
					if (peer.JniManagedPeerState.HasFlag (JniManagedPeerStates.Activatable)) {
						return;
					}
					var orig = newRef;
					newRef   = orig.NewGlobalRef ();
					JniObjectReference.Dispose (ref orig);
				} else if (options == JniObjectReferenceOptions.None) {
					// `reference` is likely *InvalidJniObjectReference, and can't be touched
					return;
				} else if (!reference.IsValid) {
					throw new ArgumentException ("JNI Object Reference is invalid.", nameof (reference));
				} else {
					newRef  = reference;

					if ((options & JniObjectReferenceOptions.Copy) == JniObjectReferenceOptions.Copy) {
						newRef  = reference.NewGlobalRef ();
					}

					JniObjectReference.Dispose (ref reference, options);
				}

				peer.SetPeerReference (newRef);
				peer.SetJniIdentityHashCode (JniSystem.IdentityHashCode (newRef));

				var o = Runtime.ObjectReferenceManager;
				if (o.LogGlobalReferenceMessages) {
					o.WriteGlobalReferenceLine ("Created PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}, Java.Type={4}",
							newRef.ToString (),
							peer.JniIdentityHashCode.ToString ("x"),
							RuntimeHelpers.GetHashCode (peer).ToString ("x"),
							peer.GetType ().FullName,
							JniEnvironment.Types.GetJniTypeNameFromInstance (newRef));
				}

				if ((options & DoNotRegisterTarget) != DoNotRegisterTarget) {
					AddPeer (peer);
				}
			}

			// This base method implementation is NOT reachable in trimmable typemap - it is featureswitch guarded
			public override IJavaPeerable? CreatePeer (
					ref JniObjectReference reference,
					JniObjectReferenceOptions transfer,
					[DynamicallyAccessedMembers (Constructors)]
					Type? targetType)
			{
				EnsureNotDisposed ();

				if (!reference.IsValid) {
					return null;
				}

				targetType  = targetType ?? typeof (JavaObject);
				targetType  = GetPeerType (targetType);

				if (!typeof (IJavaPeerable).IsAssignableFrom (targetType))
					throw new ArgumentException ($"targetType `{targetType.AssemblyQualifiedName}` must implement IJavaPeerable!", nameof (targetType));

				var targetSig  = Runtime.TypeManager.GetTypeSignature (targetType);
				if (!targetSig.IsValid || targetSig.SimpleReference == null) {
					throw new ArgumentException ($"Could not determine Java type corresponding to `{targetType.AssemblyQualifiedName}`.", nameof (targetType));
				}

				var refClass    = JniEnvironment.Types.GetObjectClass (reference);
				JniObjectReference targetClass;
				try {
					targetClass = JniEnvironment.Types.FindClass (targetSig.SimpleReference);
				} catch (Exception e) {
					JniObjectReference.Dispose (ref refClass);
					throw new ArgumentException ($"Could not find Java class `{targetSig.SimpleReference}`.",
							nameof (targetType),
							e);
				}

				if (!JniEnvironment.Types.IsAssignableFrom (refClass, targetClass)) {
					JniObjectReference.Dispose (ref refClass);
					JniObjectReference.Dispose (ref targetClass);
					return null;
				}

				JniObjectReference.Dispose (ref targetClass);

				var peer = CreatePeerInstance (ref refClass, targetType, ref reference, transfer);
				if (peer == null) {
					throw new NotSupportedException (string.Format ("Could not find an appropriate constructable wrapper type for Java type '{0}', targetType='{1}'.",
							JniEnvironment.Types.GetJniTypeNameFromInstance (reference), targetType));
				}
				peer.SetJniManagedPeerState (peer.JniManagedPeerState | JniManagedPeerStates.Replaceable);
				return peer;
			}

			[return: DynamicallyAccessedMembers (Constructors)]
			static Type GetPeerType ([DynamicallyAccessedMembers (Constructors)] Type type)
			{
				if (type == typeof (object))
					return typeof (JavaObject);
				if (type == typeof (IJavaPeerable))
					return typeof (JavaObject);
				if (type == typeof (Exception))
					return typeof (JavaException);
				return type;
			}

			IJavaPeerable? CreatePeerInstance (
					ref JniObjectReference klass,
					[DynamicallyAccessedMembers (Constructors)]
					Type targetType,
					ref JniObjectReference reference,
					JniObjectReferenceOptions transfer)
			{
				var jniTypeName = JniEnvironment.Types.GetJniTypeNameFromClass (klass);

				while (jniTypeName != null) {
					JniTypeSignature sig;
					if (!JniTypeSignature.TryParse (jniTypeName, out sig))
						return null;

					Type? type = GetTypeAssignableTo (sig, targetType);
					if (type != null) {
						var peer = TryCreatePeerInstance (ref reference, transfer, type);

						if (peer != null) {
							JniObjectReference.Dispose (ref klass);
							return peer;
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

				return TryCreatePeerInstance (ref reference, transfer, targetType);

				[return: DynamicallyAccessedMembers (Constructors)]
				Type? GetTypeAssignableTo (JniTypeSignature sig, Type targetType)
				{
					foreach (var t in Runtime.TypeManager.GetReflectionConstructibleTypes (sig)) {
						if (targetType.IsAssignableFrom (t.Type)) {
							return t.Type;
						}
					}
					return null;
				}
			}

			IJavaPeerable? TryCreatePeerInstance (
					ref JniObjectReference reference,
					JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (Constructors)]
					Type type)
			{
				type    = Runtime.TypeManager.GetInvokerType (type) ?? type;

				var self = (IJavaPeerable) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject (type);
				self.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);

				var constructed = false;
				try {
					constructed = TryConstructPeer (self, ref reference, options, type);
				} finally {
					if (!constructed) {
						GC.SuppressFinalize (self);
						self    = null;
					}
				}
				return self;
			}

			const               BindingFlags    ActivationConstructorBindingFlags   = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			static  readonly    Type            ByRefJniObjectReference = typeof (JniObjectReference).MakeByRefType ();
			static  readonly    Type[]          JIConstructorSignature  = new Type [] { ByRefJniObjectReference, typeof (JniObjectReferenceOptions) };

			protected virtual bool TryConstructPeer (
					IJavaPeerable self,
					ref JniObjectReference reference,
					JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (Constructors)]
					Type type)
			{
				var c = type.GetConstructor (ActivationConstructorBindingFlags, null, JIConstructorSignature, null);
				if (c != null) {
					var args = new object[] {
						reference,
						options,
					};
					c.Invoke (self, args);
					reference = (JniObjectReference) args [0];
					JniObjectReference.Dispose (ref reference, options);
					return true;
				}
				return false;
			}

			protected override object? CreateValueCore (
					ref JniObjectReference reference,
					JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (Constructors)]
					Type? targetType = null)
			{
				EnsureNotDisposed ();

				if (!reference.IsValid)
					return null;

				if (targetType != null && typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
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

			[return: MaybeNull]
			protected override T CreateValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();

				if (!reference.IsValid) {
	#pragma warning disable 8653
					return default (T);
	#pragma warning restore 8653
				}

				if (targetType != null && !typeof (T).IsAssignableFrom (targetType))
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

				if (typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
	#pragma warning disable CS8600,CS8601 // Possible null reference assignment.
					return (T) JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
	#pragma warning restore CS8600,CS8601 // Possible null reference assignment.
				}

				var marshaler   = GetValueMarshaler<T> ();
				return marshaler.CreateGenericValue (ref reference, options, targetType);
			}

			object? PeekBoxedObject (JniObjectReference reference)
			{
				var t   = PeekPeer (reference);
				if (t == null)
					return null;
				object? r;
				return TryUnboxPeerObject (t, out r)
					? r
					: null;
			}

			protected override object? GetValueCore (
					ref JniObjectReference reference,
					JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (Constructors)]
					Type? targetType = null)
			{
				EnsureNotDisposed ();

				if (!reference.IsValid)
					return null;

				var existing = PeekValue (reference);
				if (existing != null && (targetType == null || targetType.IsAssignableFrom (existing.GetType ()))) {
					JniObjectReference.Dispose (ref reference, options);
					return existing;
				}

				if (targetType != null && typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
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


			[return: MaybeNull]
			protected override T GetValueCore<[DynamicallyAccessedMembers (Constructors)] T> (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
			{
				EnsureNotDisposed ();
				if (!reference.IsValid) {
	#pragma warning disable 8653
					return default (T);
	#pragma warning restore 8653
				}

				if (targetType != null && !typeof (T).IsAssignableFrom (targetType))
					throw new ArgumentException (
							string.Format ("Requested runtime '{0}' value of '{1}' is not compatible with requested compile-time type T of '{2}'.",
								nameof (targetType),
								targetType,
								typeof (T)),
							nameof (targetType));

				targetType  = targetType ?? typeof (T);

				var existing = PeekValue (reference);
				if (existing != null && (targetType == null || targetType.IsAssignableFrom (existing.GetType ()))) {
					JniObjectReference.Dispose (ref reference, options);
					return (T) existing;
				}

				if (typeof (IJavaPeerable).IsAssignableFrom (targetType)) {
	#pragma warning disable CS8600,CS8601 // Possible null reference assignment.
					return (T) JavaPeerableValueMarshaler.Instance.CreateGenericValue (ref reference, options, targetType);
	#pragma warning restore CS8600,CS8601 // Possible null reference assignment.
				}

				var marshaler   = GetValueMarshaler<T> ();
				return marshaler.CreateGenericValue (ref reference, options, targetType);
			}
		
			Dictionary<Type, JniValueMarshaler> Marshalers = new Dictionary<Type, JniValueMarshaler> ();

			protected override JniValueMarshaler<T> GetValueMarshalerCore<[DynamicallyAccessedMembers (Constructors)] T> ()
			{
				EnsureNotDisposed ();

				var m   = GetValueMarshaler (typeof (T));
				var r   = m as JniValueMarshaler<T>;
				if (r != null)
					return r;
				lock (Marshalers) {
					if (!Marshalers.TryGetValue (typeof (T), out var d))
						Marshalers.Add (typeof (T), d = new DelegatingValueMarshaler<T> (m));
					return (JniValueMarshaler<T>) d;
				}
			}

			protected override JniValueMarshaler GetValueMarshalerCore (Type type)
			{
				EnsureNotDisposed ();

				if (type == null)
					throw new ArgumentNullException (nameof (type));
				if (type.ContainsGenericParameters)
					throw new ArgumentException ("Generic type definitions are not supported.", nameof (type));

				var marshalerAttr   = type.GetCustomAttribute<JniValueMarshalerAttribute> ();
				if (marshalerAttr != null)
					return (JniValueMarshaler) Activator.CreateInstance (marshalerAttr.MarshalerType)!;

				if (typeof (IJavaPeerable) == type)
					return JavaPeerableValueMarshaler.Instance;

				if (typeof (void) == type)
					return VoidValueMarshaler.Instance;

				foreach (var marshaler in JniBuiltinMarshalers.Value) {
					if (marshaler.Key == type)
						return marshaler.Value;
				}


				var listType    = GetListType (type);
				if (listType != null) {
					var elementType = listType.GenericTypeArguments [0];
					if (elementType.IsValueType) {
						foreach (var marshaler in JniPrimitiveArrayMarshalers.Value) {
							if (type.IsAssignableFrom (marshaler.Key))
								return marshaler.Value;
						}
					}

					return GetObjectArrayMarshaler (elementType);
				}

				if (typeof (IJavaPeerable).IsAssignableFrom (type)) {
					return JavaPeerableValueMarshaler.Instance;
				}

				return ProxyValueMarshaler.Instance;
			}

			protected override JniObjectReference CreateLocalObjectReferenceArgumentCore (
				[DynamicallyAccessedMembers (Constructors)]
				Type type,
				object? value)
			{
				EnsureNotDisposed ();
				var marshaler = GetValueMarshaler (type);
				var state = marshaler.CreateObjectReferenceArgumentState (value);
				try {
					if (!state.ReferenceValue.IsValid) {
						return new JniObjectReference ();
					}
					return state.ReferenceValue.NewLocalRef ();
				} finally {
					marshaler.DestroyArgumentState (value, ref state);
				}
			}

			static Type? GetListType (Type type)
			{
				foreach (var iface in type.GetInterfaces ().Concat (new [] { type })) {
					if (typeof (IList<>).IsAssignableFrom (iface.IsGenericType ? iface.GetGenericTypeDefinition () : iface))
						return iface;
				}
				return null;
			}

			static JniValueMarshaler GetObjectArrayMarshaler (Type elementType)
			{
				Func<JniValueMarshaler> indirect = GetObjectArrayMarshalerHelper<object>;
				var reifiedMethodInfo = indirect.Method.GetGenericMethodDefinition ().MakeGenericMethod (elementType);
				Func<JniValueMarshaler> direct = (Func<JniValueMarshaler>) Delegate.CreateDelegate (typeof (Func<JniValueMarshaler>), reifiedMethodInfo);
				return direct ();
			}

			static JniValueMarshaler GetObjectArrayMarshalerHelper<
					[DynamicallyAccessedMembers (Constructors)]
					T> ()
			{
				return JavaObjectArray<T>.Instance;
			}

		}

		sealed class VoidValueMarshaler : JniValueMarshaler {

			internal    static  VoidValueMarshaler              Instance    = new VoidValueMarshaler ();

			public override Type MarshalType {
				get {return typeof (void);}
			}

			public override object? CreateValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType)
			{
				throw new NotSupportedException ();
			}

			public override JniValueMarshalerState CreateObjectReferenceArgumentState (object? value, ParameterAttributes synchronize)
			{
				throw new NotSupportedException ();
			}

			public override void DestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				throw new NotSupportedException ();
			}
		}
	}

	sealed class JavaPeerableValueMarshaler : JniValueMarshaler<IJavaPeerable?> {

		internal    static  JavaPeerableValueMarshaler      Instance    = new JavaPeerableValueMarshaler ();

		[return: MaybeNull]
		public override IJavaPeerable? CreateGenericValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType)
		{
			var jvm         = JniEnvironment.Runtime;
			var marshaler   = jvm.ValueManager.GetValueMarshaler (targetType ?? typeof(IJavaPeerable));
			if (marshaler != Instance)
				return (IJavaPeerable) marshaler.CreateValue (ref reference, options, targetType)!;
			return jvm.ValueManager.CreatePeer (ref reference, options, targetType);
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull]IJavaPeerable? value, ParameterAttributes synchronize)
		{
			if (value == null || !value.PeerReference.IsValid)
				return new JniValueMarshalerState ();
			var r   = value.PeerReference.NewLocalRef ();
			return new JniValueMarshalerState (r);
		}

		public override void DestroyGenericArgumentState ([MaybeNull]IJavaPeerable? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			var r   = state.ReferenceValue;
			JniObjectReference.Dispose (ref r);
			state   = new JniValueMarshalerState ();
		}

		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			var r = CreateIntermediaryExpressionFromManagedExpression (context, sourceValue);
			var h = Expression.Variable (typeof (IntPtr), sourceValue.Name + "_handle");
			context.LocalVariables.Add (h);
			context.CreationStatements.Add (Expression.Assign (h, Expression.Property (r, "Handle")));

			return h;
		}

		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		Expression CreateIntermediaryExpressionFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			var r   = Expression.Variable (typeof (JniObjectReference), sourceValue.Name + "_ref");
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (
					Expression.IfThenElse (
						test:       Expression.Equal (Expression.Constant (null), sourceValue),
						ifTrue:     Expression.Assign (r, Expression.New (typeof (JniObjectReference))),
						ifFalse:    Expression.Assign (r, Expression.Property (Expression.Convert (sourceValue, typeof (IJavaPeerable)), "PeerReference"))));

			return r;
		}

		[RequiresDynamicCode (ExpressionRequiresUnreferencedCode)]
		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return ReturnObjectReferenceToJni (context, sourceValue.Name, CreateIntermediaryExpressionFromManagedExpression (context, sourceValue));
		}

		[RequiresDynamicCode (ExpressionRequiresUnreferencedCode)]
		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type? targetType)
		{
			targetType ??= typeof (object);

			var r   = Expression.Variable (targetType, sourceValue.Name + "_val");
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (
					Expression.Assign (r,
						Expression.Call (
							context.ValueManager ?? Expression.Property (context.Runtime, "ValueManager"),
							"GetValue",
							new[]{targetType},
							sourceValue)));
			return r;
		}
	}

	sealed class DelegatingValueMarshaler<
			[DynamicallyAccessedMembers (Constructors)]
			T
	>
		: JniValueMarshaler<T>
	{

		JniValueMarshaler   ValueMarshaler;

		public DelegatingValueMarshaler (JniValueMarshaler valueMarshaler)
		{
			ValueMarshaler  = valueMarshaler;
		}

		[return: MaybeNull]
		public override T CreateGenericValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType)
		{
			return (T) ValueMarshaler.CreateValue (ref reference, options, targetType ?? typeof (T))!;
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull]T value, ParameterAttributes synchronize)
		{
			return ValueMarshaler.CreateObjectReferenceArgumentState (value, synchronize);
		}

		public override void DestroyGenericArgumentState ([AllowNull]T value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			ValueMarshaler.DestroyArgumentState (value, ref state, synchronize);
		}

		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			return ValueMarshaler.CreateParameterFromManagedExpression (context, sourceValue, synchronize);
		}

		[RequiresDynamicCode (ExpressionRequiresUnreferencedCode)]
		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type? targetType)
		{
			return ValueMarshaler.CreateParameterToManagedExpression (context, sourceValue, synchronize, targetType);
		}

		[RequiresDynamicCode (ExpressionRequiresUnreferencedCode)]
		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return ValueMarshaler.CreateReturnValueFromManagedExpression (context, sourceValue);
		}
	}

	sealed class ProxyValueMarshaler : JniValueMarshaler<object?> {

		internal    static  ProxyValueMarshaler     Instance    = new ProxyValueMarshaler ();

		[return: MaybeNull]
		public override object? CreateGenericValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType)
		{
			var jvm     = JniEnvironment.Runtime;

			if (targetType == null || targetType == typeof (object)) {
				targetType      = jvm.ValueManager.GetRuntimeType (reference);
			}
			if (targetType != null) {
				var vm  = jvm.ValueManager.GetValueMarshaler (targetType);
				if (vm != Instance) {
					return vm.CreateValue (ref reference, options, targetType)!;
				}
			}

			var target  = jvm.ValueManager.PeekValue (reference);
			if (target != null) {
				JniObjectReference.Dispose (ref reference, options);
				return target;
			}
			// Punt! Hope it's a java.lang.Object
			return jvm.ValueManager.CreatePeer (ref reference, options, targetType);
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull]object? value, ParameterAttributes synchronize)
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
			return new JniValueMarshalerState (p!.PeerReference.NewLocalRef ());
		}

		public override void DestroyGenericArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
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
}
