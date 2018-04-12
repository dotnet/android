using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Java.Interop {

	public partial class JniRuntime {

		public class JniTypeManager : IDisposable, ISetRuntime {

			bool                    disposed;


			public      JniRuntime  Runtime { get; private set; }

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				AssertValid ();
				Runtime = runtime;
			}

			public void Dispose ()
			{
				Dispose (false);
			}

			protected virtual void Dispose (bool disposing)
			{
				disposed    = true;
			}

			void AssertValid ()
			{
				if (!disposed)
					return;
				throw new ObjectDisposedException (nameof (JniTypeManager));
			}

			public JniTypeSignature GetTypeSignature (Type type)
			{
				AssertValid ();

				return GetTypeSignatures (type).FirstOrDefault ();
			}

			public IEnumerable<JniTypeSignature> GetTypeSignatures (Type type)
			{
				AssertValid ();

				if (type == null)
					throw new ArgumentNullException ("type");
				if (type.GetTypeInfo ().ContainsGenericParameters)
					throw new ArgumentException ("Generic type definitions are not supported.", "type");

				return CreateGetTypeSignaturesEnumerator (type);
			}

			IEnumerable<JniTypeSignature> CreateGetTypeSignaturesEnumerator (Type type)
			{
				var originalType    = type;
				int rank            = 0;
				while (type.IsArray) {
					if (type.IsArray && type.GetArrayRank () > 1)
						throw new ArgumentException ("Multidimensional array '" + originalType.FullName + "' is not supported.", "type");
					rank++;
					type    = type.GetElementType ();
				}

				var info    = type.GetTypeInfo ();
				if (info.IsEnum)
					type = Enum.GetUnderlyingType (type);

#if !XA_INTEGRATION
				foreach (var mapping in JniBuiltinTypeNameMappings) {
					if (mapping.Key == type) {
						var r = mapping.Value;
						yield return r.AddArrayRank (rank);
					}
				}

				foreach (var mapping in JniBuiltinArrayMappings) {
					if (mapping.Key == type) {
						var r = mapping.Value;
						yield return r.AddArrayRank (rank);
					}
				}
#endif  // !XA_INTEGRATION

				var name = info.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
				if (name != null) {
					yield return new JniTypeSignature (name.SimpleReference, name.ArrayRank + rank, name.IsKeyword);
				}

				var isGeneric   = info.IsGenericType;
				var genericDef  = isGeneric ? info.GetGenericTypeDefinition () : type;
#if !XA_INTEGRATION
				if (isGeneric) {
					if (genericDef == typeof(JavaArray<>) || genericDef == typeof(JavaObjectArray<>)) {
						var r = GetTypeSignature (info.GenericTypeArguments [0]);
						yield return r.AddArrayRank (rank + 1);
					}
				}
#endif  // !XA_INTEGRATION
				foreach (var simpleRef in GetSimpleReferences (type)) {
					if (simpleRef == null)
						continue;
					yield return new JniTypeSignature (simpleRef, rank, false);
				}

				if (isGeneric) {
					foreach (var simpleRef in GetSimpleReferences (genericDef)) {
						if (simpleRef == null)
							continue;
						yield return new JniTypeSignature (simpleRef, rank, false);
					}
				}
			}

			// `type` will NOT be an array type.
			protected virtual IEnumerable<string> GetSimpleReferences (Type type)
			{
				AssertValid ();

				if (type == null)
					throw new ArgumentNullException ("type");
				if (type.IsArray)
					throw new ArgumentException ("Array type '" + type.FullName + "' is not supported.", "type");
				return EmptyStringArray;
			}

			static  readonly    string[]    EmptyStringArray    = new string [0];
			static  readonly    Type[]      EmptyTypeArray      = new Type [0];


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
				foreach (var type in GetTypesForSimpleReference (typeSignature.SimpleReference)) {
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

				return CreateGetTypesForSimpleReferenceEnumerator (jniSimpleReference);
			}

			IEnumerable<Type> CreateGetTypesForSimpleReferenceEnumerator (string jniSimpleReference)
			{
#if !XA_INTEGRATION
				foreach (var mapping in JniBuiltinTypeNameMappings) {
					if (mapping.Value.SimpleReference == jniSimpleReference)
						yield return mapping.Key;
				}
#endif  // !XA_INTEGRATION
				yield break;
			}

			public virtual void RegisterNativeMembers (JniType nativeClass, Type type, string methods)
			{
				AssertValid ();

				if (!TryLoadJniMarshalMethods (nativeClass, type, methods) &&
						!TryRegisterNativeMembers (nativeClass, type, methods)) {
					throw new NotSupportedException ($"Could not register Java.class={nativeClass.Name} Managed.type={type.FullName}");
				}
			}

			static Type [] registerMethodParameters = new Type [] { typeof (JniNativeMethodRegistrationArguments) };

			static bool TryLoadJniMarshalMethods (JniType nativeClass, Type type, string methods)
			{
				var marshalType = type.GetTypeInfo ()?.GetDeclaredNestedType ("__<$>_jni_marshal_methods")?.AsType ();
				if (marshalType == null || !(marshalType is Type))
					return false;

				var registerMethod = marshalType.GetRuntimeMethod ("__RegisterNativeMembers", registerMethodParameters);

				return TryRegisterNativeMembers (nativeClass, marshalType, methods, registerMethod);
			}

			static List<JniNativeMethodRegistration> sharedRegistrations = new List<JniNativeMethodRegistration> ();

			static bool TryRegisterNativeMembers (JniType nativeClass, Type marshalType, string methods, MethodInfo registerMethod = null)
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

			static bool FindAndCallRegisterMethod (Type marshalType, JniNativeMethodRegistrationArguments arguments)
			{
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

