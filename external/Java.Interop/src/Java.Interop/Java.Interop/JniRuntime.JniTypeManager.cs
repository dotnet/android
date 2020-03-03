#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Java.Interop {

	public partial class JniRuntime {

		public class JniTypeManager : IDisposable, ISetRuntime {

			JniRuntime?             runtime;
			bool                    disposed;


			public      JniRuntime  Runtime {
				get => runtime ?? throw new NotSupportedException ();
			}

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				AssertValid ();
				this.runtime = runtime;
			}

			public void Dispose ()
			{
				Dispose (false);
			}

			protected virtual void Dispose (bool disposing)
			{
				disposed    = true;
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			void AssertValid ()
			{
				if (!disposed)
					return;
				throw new ObjectDisposedException (nameof (JniTypeManager));
			}

			// NOTE: This method needs to be kept in sync with GetTypeSignatures()
			// This version of the method has removed IEnumerable for performance reasons.
			public JniTypeSignature GetTypeSignature (Type type)
			{
				AssertValid ();

				if (type == null)
 					throw new ArgumentNullException (nameof (type));
				if (type.ContainsGenericParameters)
					throw new ArgumentException ($"'{type}' contains a generic type definition. This is not supported.", nameof (type));

				type = GetUnderlyingType (type, out int rank);

				JniTypeSignature signature = JniTypeSignature.Empty;
				if (GetBuiltInTypeSignature (type, ref signature))
					return signature.AddArrayRank (rank);
				if (GetBuiltInTypeArraySignature (type, ref signature))
					return signature.AddArrayRank (rank);

				var simpleRef = GetSimpleReference (type);
				if (simpleRef != null)
					return new JniTypeSignature (simpleRef, rank, false);

				var name = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
				if (name != null) {
					return new JniTypeSignature (name.SimpleReference, name.ArrayRank + rank, name.IsKeyword);
				}

				var isGeneric = type.IsGenericType;
				var genericDef = isGeneric ? type.GetGenericTypeDefinition () : type;
				if (isGeneric) {
					if (genericDef == typeof (JavaArray<>) || genericDef == typeof (JavaObjectArray<>)) {
						var r = GetTypeSignature (type.GenericTypeArguments [0]);
						return r.AddArrayRank (rank + 1);
					}
				}

				if (isGeneric) {
					simpleRef = GetSimpleReference (genericDef);
					if (simpleRef != null)
						return new JniTypeSignature (simpleRef, rank, false);
				}

				return default;
			}

			// NOTE: This method needs to be kept in sync with GetTypeSignature()
			public IEnumerable<JniTypeSignature> GetTypeSignatures (Type type)
			{
				AssertValid ();

				if (type.ContainsGenericParameters)
					throw new ArgumentException ($"'{type}' contains a generic type definition. This is not supported.", nameof (type));

				type = GetUnderlyingType (type, out int rank);

				var signature = new JniTypeSignature (null);
				if (GetBuiltInTypeSignature (type, ref signature))
					yield return signature.AddArrayRank (rank);
				if (GetBuiltInTypeArraySignature (type, ref signature))
					yield return signature.AddArrayRank (rank);

				foreach (var simpleRef in GetSimpleReferences (type)) {
					if (simpleRef == null)
						continue;
					yield return new JniTypeSignature (simpleRef, rank, false);
				}

				var name = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
				if (name != null) {
					yield return new JniTypeSignature (name.SimpleReference, name.ArrayRank + rank, name.IsKeyword);
				}

				var isGeneric   = type.IsGenericType;
				var genericDef  = isGeneric ? type.GetGenericTypeDefinition () : type;
				if (isGeneric) {
					if (genericDef == typeof(JavaArray<>) || genericDef == typeof(JavaObjectArray<>)) {
						var r = GetTypeSignature (type.GenericTypeArguments [0]);
						yield return r.AddArrayRank (rank + 1);
					}
				}

				if (isGeneric) {
					foreach (var simpleRef in GetSimpleReferences (genericDef)) {
						if (simpleRef == null)
							continue;
						yield return new JniTypeSignature (simpleRef, rank, false);
					}
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
					type = type.GetElementType ();
				}

				if (type.IsEnum)
					type = Enum.GetUnderlyingType (type);

				return type;
			}

			// `type` will NOT be an array type.
			protected virtual string GetSimpleReference (Type type)
			{
				return GetSimpleReferences (type).FirstOrDefault ();
			}

			// `type` will NOT be an array type.
			protected virtual IEnumerable<string> GetSimpleReferences (Type type)
			{
				AssertValid ();

				if (type == null)
					throw new ArgumentNullException (nameof (type));
				if (type.IsArray)
					throw new ArgumentException ("Array type '" + type.FullName + "' is not supported.", nameof (type));
				return EmptyStringArray;
			}

			static  readonly    string[]    EmptyStringArray    = Array.Empty<string> ();
			static  readonly    Type[]      EmptyTypeArray      = Array.Empty<Type> ();


			public  Type    GetType (JniTypeSignature typeSignature)
			{
				AssertValid ();

				return GetTypes (typeSignature).FirstOrDefault ();
			}

			public virtual IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
			{
				AssertValid ();

				if (typeSignature.SimpleReference == null)
					return EmptyTypeArray;
				return CreateGetTypesEnumerator (typeSignature);
			}

			IEnumerable<Type> CreateGetTypesEnumerator (JniTypeSignature typeSignature)
			{
				if (!typeSignature.IsValid)
					yield break;
				foreach (var type in GetTypesForSimpleReference (typeSignature.SimpleReference ?? throw new InvalidOperationException ("Should not be reached")) ){
					if (typeSignature.ArrayRank == 0) {
						yield return type;
						continue;
					}
#if !XA_INTEGRATION
					if (typeSignature.ArrayRank > 0) {
						var rank        = typeSignature.ArrayRank;
						var arrayType   = type;
						if (typeSignature.IsKeyword) {
							arrayType   = typeof (JavaPrimitiveArray<>).MakeGenericType (arrayType);
							rank--;
						}
						while (rank-- > 0) {
							arrayType   = typeof (JavaObjectArray<>).MakeGenericType (arrayType);
						}
						yield return arrayType;
					}
#endif  // !XA_INTEGRATION
					if (typeSignature.ArrayRank > 0) {
						var rank        = typeSignature.ArrayRank;
						var arrayType   = type;
						while (rank-- > 0) {
							arrayType   = arrayType.MakeArrayType ();
						}
						yield return arrayType;
					}
				}
			}

			protected virtual IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
			{
				AssertValid ();

				if (jniSimpleReference == null)
					throw new ArgumentNullException (nameof (jniSimpleReference));
				if (jniSimpleReference != null && jniSimpleReference.Contains ("."))
					throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", nameof (jniSimpleReference));
				if (jniSimpleReference != null && jniSimpleReference.StartsWith ("[", StringComparison.Ordinal))
					throw new ArgumentException ("Only simplified type references are supported.", nameof (jniSimpleReference));
				if (jniSimpleReference != null && jniSimpleReference.StartsWith ("L", StringComparison.Ordinal) && jniSimpleReference.EndsWith (";", StringComparison.Ordinal))
					throw new ArgumentException ("Only simplified type references are supported.", nameof (jniSimpleReference));

				// Not sure why CS8604 is reported on following line when we check against null ~9 lines above...
				return CreateGetTypesForSimpleReferenceEnumerator (jniSimpleReference!);
			}

			IEnumerable<Type> CreateGetTypesForSimpleReferenceEnumerator (string jniSimpleReference)
			{
				if (JniBuiltinSimpleReferenceToType.Value.TryGetValue (jniSimpleReference, out Type ret)) {
					yield return ret;
				}
				yield break;
			}

			public virtual void RegisterNativeMembers (JniType nativeClass, Type type, string? methods)
			{
				TryRegisterNativeMembers (nativeClass, type, methods);
			}

			protected bool TryRegisterNativeMembers (JniType nativeClass, Type type, string? methods)
			{
				AssertValid ();

				return TryLoadJniMarshalMethods (nativeClass, type, methods) || TryRegisterNativeMembers (nativeClass, type, methods, null);
			}

			static Type [] registerMethodParameters = new Type [] { typeof (JniNativeMethodRegistrationArguments) };

			bool TryLoadJniMarshalMethods (JniType nativeClass, Type type, string? methods)
			{
				var marshalType = type?.GetNestedType ("__<$>_jni_marshal_methods", BindingFlags.NonPublic);
				if (marshalType == null)
					return false;

				var registerMethod = marshalType.GetRuntimeMethod ("__RegisterNativeMembers", registerMethodParameters);

				return TryRegisterNativeMembers (nativeClass, marshalType, methods, registerMethod);
			}

			static List<JniNativeMethodRegistration> sharedRegistrations = new List<JniNativeMethodRegistration> ();

			bool TryRegisterNativeMembers (JniType nativeClass, Type marshalType, string? methods, MethodInfo? registerMethod)
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

			bool FindAndCallRegisterMethod (Type marshalType, JniNativeMethodRegistrationArguments arguments)
			{
				if (!Runtime.JniAddNativeMethodRegistrationAttributePresent)
					return false;

				bool found = false;

				foreach (var methodInfo in marshalType.GetRuntimeMethods ()) {
					if (methodInfo.GetCustomAttribute (typeof (JniAddNativeMethodRegistrationAttribute)) == null) {
						continue;
					}

					if ((methodInfo.Attributes & MethodAttributes.Static) != MethodAttributes.Static) {
						throw new InvalidOperationException ($"The method {methodInfo} marked with {nameof (JniAddNativeMethodRegistrationAttribute)} must be static");
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

