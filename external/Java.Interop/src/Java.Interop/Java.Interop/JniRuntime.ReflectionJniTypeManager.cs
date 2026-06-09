#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Java.Interop {

	public partial class JniRuntime {

		[RequiresDynamicCode ("This JniTypeManager implementation is not compatible with Native AOT. Use a different JniTypeManager implementation that supports Native AOT.")]
		[RequiresUnreferencedCode ("This JniTypeManager implementation is not compatible with Native AOT. Use a different JniTypeManager implementation that supports Native AOT.")]
		public partial class ReflectionJniTypeManager : JniTypeManager {

			protected override JniTypeSignature GetTypeSignatureCore (Type type)
			{
				type = GetUnderlyingType (type, out int rank);

				JniTypeSignature signature = JniTypeSignature.Empty;
				if (GetBuiltInTypeSignature (type, ref signature))
					return signature.AddArrayRank (rank);
				if (GetBuiltInTypeArraySignature (type, ref signature))
					return signature.AddArrayRank (rank);

				var isGeneric = type.IsGenericType;
				var genericDef = isGeneric ? type.GetGenericTypeDefinition () : type;
				if (isGeneric) {
					if (genericDef == typeof (JavaArray<>) || genericDef == typeof (JavaObjectArray<>)) {
						var r = GetTypeSignature (type.GenericTypeArguments [0]);
						return r.AddArrayRank (rank + 1);
					}

					var genericSimpleRef = GetSimpleReference (genericDef);
					if (genericSimpleRef != null)
						return new JniTypeSignature (genericSimpleRef, rank, false);
				}

				var simpleRef = GetSimpleReference (type);
				if (simpleRef != null)
					return new JniTypeSignature (simpleRef, rank, false);

				return default;
			}

			protected override IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type)
			{
				type = GetUnderlyingType (type, out int rank);

				var signature = JniTypeSignature.Empty;
				if (GetBuiltInTypeSignature (type, ref signature))
					yield return signature.AddArrayRank (rank);
				if (GetBuiltInTypeArraySignature (type, ref signature))
					yield return signature.AddArrayRank (rank);

				var isGeneric = type.IsGenericType;
				var genericDef = isGeneric ? type.GetGenericTypeDefinition () : type;
				if (isGeneric) {
					if (genericDef == typeof (JavaArray<>) || genericDef == typeof (JavaObjectArray<>)) {
						var r = GetTypeSignature (type.GenericTypeArguments [0]);
						yield return r.AddArrayRank (rank + 1);
					}

					foreach (var genericSimpleRef in GetSimpleReferences (genericDef)) {
						if (genericSimpleRef == null)
							continue;
						yield return new JniTypeSignature (genericSimpleRef, rank, false);
					}
				}

				foreach (var simpleRef in GetSimpleReferences (type)) {
					if (simpleRef == null)
						continue;
					yield return new JniTypeSignature (simpleRef, rank, false);
				}
			}

			static Type GetUnderlyingType (Type type, out int rank)
			{
				rank = 0;
				var originalType = type;
				while (type.IsArray) {
					if (type.IsArray && type.GetArrayRank () > 1)
						throw new ArgumentException ("Multidimensional array '" + originalType.FullName + "' is not supported.", nameof (type));
					rank++;
					type = type.GetElementType () ?? throw new InvalidOperationException ("Array type has no element type.");
				}

				if (type.IsEnum)
					type = Enum.GetUnderlyingType (type);

				return type;
			}

			// `type` will NOT be an array type.
			protected override string? GetSimpleReference (Type type)
			{
				return GetSimpleReferences (type).FirstOrDefault ();
			}

			// `type` will NOT be an array type.
			protected override IEnumerable<string> GetSimpleReferences (Type type)
			{
				AssertValid ();

				if (type == null)
					throw new ArgumentNullException (nameof (type));
				if (type.IsArray)
					throw new ArgumentException ("Array type '" + type.FullName + "' is not supported.", nameof (type));

				var name = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
				if (name != null) {
					var altRef = GetReplacementType (name.SimpleReference);
					if (altRef != null) {
						yield return altRef;
					} else {
						yield return name.SimpleReference;
					}
				}

				yield break;
			}

			static  readonly    Type[]      EmptyTypeArray      = [];

			readonly struct KnownArrayTypesInfo
			{
				public readonly Dictionary<Type, Type> ArrayTypes;
				public readonly Dictionary<Type, Type> JavaObjectArrayTypes;

				public KnownArrayTypesInfo (Dictionary<Type, Type> arrayTypes, Dictionary<Type, Type> javaObjectArrayTypes)
				{
					ArrayTypes            = arrayTypes;
					JavaObjectArrayTypes = javaObjectArrayTypes;
				}
			}

			static readonly Lazy<KnownArrayTypesInfo> KnownArrayTypes = new Lazy<KnownArrayTypesInfo> (InitKnownArrayTypes);

			static KnownArrayTypesInfo InitKnownArrayTypes ()
			{
				var arrayTypes           = new Dictionary<Type, Type> ();
				var javaObjectArrayTypes = new Dictionary<Type, Type> ();

				AddKnownArrayTypes<string> (arrayTypes, javaObjectArrayTypes);

				AddKnownPrimitiveArrayTypes<Boolean, JavaBooleanArray> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<SByte, JavaSByteArray> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<Char, JavaCharArray> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<Int16, JavaInt16Array> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<Int32, JavaInt32Array> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<Int64, JavaInt64Array> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<Single, JavaSingleArray> (arrayTypes, javaObjectArrayTypes);
				AddKnownPrimitiveArrayTypes<Double, JavaDoubleArray> (arrayTypes, javaObjectArrayTypes);

				return new KnownArrayTypesInfo (arrayTypes, javaObjectArrayTypes);
			}

			static void AddKnownPrimitiveArrayTypes<
					[DynamicallyAccessedMembers (Constructors)]
					T,
					[DynamicallyAccessedMembers (Constructors)]
					TArray> (Dictionary<Type, Type> arrayTypes, Dictionary<Type, Type> javaObjectArrayTypes)
			{
				AddKnownArrayTypes<T> (arrayTypes, javaObjectArrayTypes);
				AddKnownArrayTypes<JavaArray<T>> (arrayTypes, javaObjectArrayTypes);
				AddKnownArrayTypes<JavaPrimitiveArray<T>> (arrayTypes, javaObjectArrayTypes);
				AddKnownArrayTypes<TArray> (arrayTypes, javaObjectArrayTypes);
			}

			static void AddKnownArrayTypes<
					[DynamicallyAccessedMembers (Constructors)]
					T> (Dictionary<Type, Type> arrayTypes, Dictionary<Type, Type> javaObjectArrayTypes)
			{
				arrayTypes [typeof (T)]                         = typeof (T[]);
				arrayTypes [typeof (T[])]                       = typeof (T[][]);
				arrayTypes [typeof (T[][])]                     = typeof (T[][][]);
				javaObjectArrayTypes [typeof (T)]               = typeof (JavaObjectArray<T>);
				javaObjectArrayTypes [typeof (JavaObjectArray<T>)] = typeof (JavaObjectArray<JavaObjectArray<T>>);
			}

			static bool TryMakeArrayType (Type type, out Type? arrayType) =>
				KnownArrayTypes.Value.ArrayTypes.TryGetValue (type, out arrayType);

			static bool TryMakeJavaObjectArrayType (Type type, out Type? arrayType) =>
				KnownArrayTypes.Value.JavaObjectArrayTypes.TryGetValue (type, out arrayType);

			static Type MakeArrayType (Type type) =>
				TryMakeArrayType (type, out var arrayType)
					? arrayType ?? throw new InvalidOperationException ("Should not be reached")
					: type.MakeArrayType ();

			static Type MakeJavaObjectArrayType (Type type) =>
				TryMakeJavaObjectArrayType (type, out var arrayType)
					? arrayType ?? throw new InvalidOperationException ("Should not be reached")
					: typeof (JavaObjectArray<>).MakeGenericType (type);

			[return: DynamicallyAccessedMembers (MethodsConstructors)]
			protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference);

				return jniSimpleReference switch {
					"java/lang/String"                         => typeof (string),
					"net/dot/jni/internal/JavaProxyObject"     => typeof (JavaProxyObject),
					"net/dot/jni/internal/JavaProxyThrowable"  => typeof (JavaProxyThrowable),
					"V"                                        => typeof (void),
					"Z"                                        => typeof (Boolean),
					"java/lang/Boolean"                        => typeof (Boolean?),
					"B"                                        => typeof (SByte),
					"java/lang/Byte"                           => typeof (SByte?),
					"C"                                        => typeof (Char),
					"java/lang/Character"                      => typeof (Char?),
					"S"                                        => typeof (Int16),
					"java/lang/Short"                          => typeof (Int16?),
					"I"                                        => typeof (Int32),
					"java/lang/Integer"                        => typeof (Int32?),
					"J"                                        => typeof (Int64),
					"java/lang/Long"                           => typeof (Int64?),
					"F"                                        => typeof (Single),
					"java/lang/Float"                          => typeof (Single?),
					"D"                                        => typeof (Double),
					"java/lang/Double"                         => typeof (Double?),
					_                                          => null,
				};
			}

			public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
			{
				AssertValid ();

				if (typeSignature.SimpleReference == null)
					return EmptyTypeArray;
				return CreateGetTypesEnumerator (typeSignature);
			}

			public override IEnumerable<ReflectionConstructibleType> GetReflectionConstructibleTypes (JniTypeSignature typeSignature)
			{
				foreach (var type in GetTypes (typeSignature)) {
					yield return new ReflectionConstructibleType (type);
				}
			}

			IEnumerable<Type> CreateGetTypesEnumerator (JniTypeSignature typeSignature)
			{
				if (!typeSignature.IsValid)
					yield break;
				foreach (var type in GetTypesForSimpleReference (typeSignature.SimpleReference ?? throw new InvalidOperationException ("Should not be reached"))) {
					if (typeSignature.ArrayRank == 0) {
						yield return type;
						continue;
					}

					if (typeSignature.IsKeyword) {
						foreach (var t in GetPrimitiveArrayTypesForSimpleReference (typeSignature, type)) {
							yield return t;
						}
						continue;
					}

					if (typeSignature.ArrayRank > 0) {
						var rank        = typeSignature.ArrayRank;
						var arrayType   = type;
						while (rank-- > 0) {
							arrayType = MakeJavaObjectArrayType (arrayType);
						}
						yield return arrayType;
					}

					if (typeSignature.ArrayRank > 0) {
						var rank        = typeSignature.ArrayRank;
						var arrayType   = type;
						while (rank-- > 0) {
							arrayType = MakeArrayType (arrayType);
						}
						yield return arrayType;
					}
				}
			}

			IEnumerable<Type> GetPrimitiveArrayTypesForSimpleReference (JniTypeSignature typeSignature, Type type)
			{
				int index   = -1;
				for (int i = 0; i < JniPrimitiveArrayTypes.Length; ++i) {
					if (JniPrimitiveArrayTypes [i].PrimitiveType == type) {
						index   = i;
						break;
					}
				}
				if (index == -1) {
					throw new InvalidOperationException ($"Should not be reached; Could not find JniPrimitiveArrayInfo for {type}");
				}
				foreach (var t in JniPrimitiveArrayTypes [index].ArrayTypes) {
					var rank        = typeSignature.ArrayRank-1;
					var arrayType   = t;
					while (rank-- > 0) {
						arrayType = MakeJavaObjectArrayType (arrayType);
					}
					yield return arrayType;

					rank            = typeSignature.ArrayRank-1;
					arrayType       = t;
					while (rank-- > 0) {
						arrayType = MakeArrayType (arrayType);
					}
					yield return arrayType;
				}
			}

			protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference);

				// Not sure why CS8604 is reported on following line when we check against null ~9 lines above...
				return CreateGetTypesForSimpleReferenceEnumerator (jniSimpleReference!);
			}

			IEnumerable<Type> CreateGetTypesForSimpleReferenceEnumerator (string jniSimpleReference)
			{
				if (JniBuiltinSimpleReferenceToType.Value.TryGetValue (jniSimpleReference, out var ret)) {
					yield return ret;
				}
				if (RuntimeFeature.ManagedPeerNativeRegistration && jniSimpleReference == ManagedPeer.JniTypeName) {
					yield return typeof (ManagedPeer);
				}
				yield break;
			}

			[return: DynamicallyAccessedMembers (Constructors)]
			protected override Type? GetInvokerTypeCore (
					[DynamicallyAccessedMembers (Constructors)]
					Type type)
			{
				var signature   = type.GetCustomAttribute<JniTypeSignatureAttribute> ();
				if (signature == null || signature.InvokerType == null) {
					return null;
				}

				Type[] arguments = type.GetGenericArguments ();
				if (arguments.Length == 0)
					return signature.InvokerType;

				throw new NotSupportedException ($"Generic invoker type construction for `{type}` is not supported.");
			}

			protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimple) => null;

			protected override string? GetReplacementTypeCore (string jniSimpleReference) => null;

			protected override ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSimpleReference, string jniMethodName, string jniMethodSignature) => null;

			public override void RegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
					Type type,
					ReadOnlySpan<char> methods)
			{
				TryRegisterNativeMembers (nativeClass, type, methods);
			}

			protected bool TryRegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
					Type type,
					ReadOnlySpan<char> methods)
			{
				AssertValid ();

#pragma warning disable CS1717
				methods = methods;
#pragma warning restore CS1717

				return TryLoadJniMarshalMethods (nativeClass, type, null) || TryRegisterNativeMembers (nativeClass, type, null, null);
			}

			[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
			public override void RegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
					Type type,
					string? methods)
			{
				TryRegisterNativeMembers (nativeClass, type, methods);
			}

			[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
			protected bool TryRegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
					Type type,
					string? methods)
			{
				AssertValid ();

				return TryLoadJniMarshalMethods (nativeClass, type, methods) || TryRegisterNativeMembers (nativeClass, type, methods, null);
			}

			static Type [] registerMethodParameters = new Type [] { typeof (JniNativeMethodRegistrationArguments) };

			bool TryLoadJniMarshalMethods (
					JniType nativeClass,
					[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
					Type type,
					string? methods)
			{
				var marshalType = type?.GetNestedType ("__<$>_jni_marshal_methods", BindingFlags.NonPublic);
				if (marshalType == null) {
					return false;
				}

				var registerMethod = marshalType.GetMethod (
						name:           "__RegisterNativeMembers",
						bindingAttr:    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
						binder:         null,
						callConvention: default,
						types:          registerMethodParameters,
						modifiers:      null);
				return TryRegisterNativeMembers (nativeClass, marshalType, methods, registerMethod);
			}

			static List<JniNativeMethodRegistration> sharedRegistrations = new List<JniNativeMethodRegistration> ();

			bool TryRegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (Methods)]
					Type marshalType,
					string? methods,
					MethodInfo? registerMethod)
			{
				bool lockTaken = false;
				bool rv = false;

				try {
					Monitor.TryEnter (sharedRegistrations, ref lockTaken);
					List<JniNativeMethodRegistration> registrations;
					if (lockTaken) {
						sharedRegistrations.Clear ();
						registrations = sharedRegistrations;
					} else {
						registrations = new List<JniNativeMethodRegistration> ();
					}
					JniNativeMethodRegistrationArguments arguments = new JniNativeMethodRegistrationArguments (registrations, methods);
					if (registerMethod != null) {
						registerMethod.Invoke (null, new object [] { arguments });
						rv = true;
					} else
						rv = FindAndCallRegisterMethod (marshalType, arguments);

					if (registrations.Count > 0)
						nativeClass.RegisterNativeMethods (registrations.ToArray ());
				} finally {
					if (lockTaken) {
						Monitor.Exit (sharedRegistrations);
					}
				}

				return rv;
			}

			bool FindAndCallRegisterMethod (
					[DynamicallyAccessedMembers (Methods)]
					Type marshalType,
					JniNativeMethodRegistrationArguments arguments)
			{
				if (!Runtime.JniAddNativeMethodRegistrationAttributePresent)
					return false;

				bool found = false;

				foreach (var methodInfo in marshalType.GetRuntimeMethods ()) {
					if (methodInfo.GetCustomAttribute (typeof (JniAddNativeMethodRegistrationAttribute)) == null) {
						continue;
					}

					var declaringTypeName = methodInfo.DeclaringType?.FullName ?? "<no-decl-type>";

					if ((methodInfo.Attributes & MethodAttributes.Static) != MethodAttributes.Static) {
						throw new InvalidOperationException ($"The method `{declaringTypeName}.{methodInfo}` marked with [{nameof (JniAddNativeMethodRegistrationAttribute)}] must be static!");
					}

					var register = (Action<JniNativeMethodRegistrationArguments>)methodInfo.CreateDelegate (typeof (Action<JniNativeMethodRegistrationArguments>));
					register (arguments);

					found = true;
				}

				return found;
			}

		}
	}
}
