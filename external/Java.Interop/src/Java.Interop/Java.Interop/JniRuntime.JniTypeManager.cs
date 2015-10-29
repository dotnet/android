using System;
using System.Collections.Generic;
using System.Linq;

namespace Java.Interop {

	public partial class JniRuntime {

		public class JniTypeManager : ISetRuntime {

			protected   JniRuntime  Runtime { get; private set; }

			void ISetRuntime.SetRuntime (JniRuntime runtime)
			{
				Runtime = runtime;
			}

			public JniTypeSignature GetTypeSignature (Type type)
			{
				return GetTypeSignatures (type).FirstOrDefault ();
			}

			public IEnumerable<JniTypeSignature> GetTypeSignatures (Type type)
			{
				if (type == null)
					throw new ArgumentNullException ("type");
				if (type.ContainsGenericParameters)
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

				if (type.IsEnum)
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

				var names = (JniTypeSignatureAttribute[]) type.GetCustomAttributes (typeof (JniTypeSignatureAttribute), inherit:false);
				for (int i = 0; i < names.Length; ++i) {
					yield return new JniTypeSignature (names [i].SimpleReference, names [i].ArrayRank + rank, names [i].IsKeyword);
				}

#if !XA_INTEGRATION
				if (type.IsGenericType) {
					var def = type.GetGenericTypeDefinition ();
					if (def == typeof(JavaArray<>) || def == typeof(JavaObjectArray<>)) {
						var r = GetTypeSignature (type.GetGenericArguments () [0]);
						yield return r.AddArrayRank (rank + 1);
					}
				}
#endif  // !XA_INTEGRATION
				foreach (var simpleRef in GetSimpleReferences (type)) {
					yield return new JniTypeSignature (simpleRef, rank, false);
				}
			}

			// `type` will NOT be an array type.
			protected virtual IEnumerable<string> GetSimpleReferences (Type type)
			{
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
				return GetTypes (typeSignature).FirstOrDefault ();
			}

			public virtual IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
			{
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

			public JniTypeSignature GetTypeSignature (string jniTypeReference)
			{
				if (jniTypeReference == null)
					throw new ArgumentNullException (nameof (jniTypeReference));
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
								nameof (jniTypeReference));
					if (i == 0) {
						n   = s > i
							? jniTypeReference.Substring (i + 1, s - i - 1)
							: jniTypeReference;
					} else {
						if (s < i)
							throw new ArgumentException (
									string.Format ("Malformed JNI type reference; no terminating ';' for type ref: '{0}'.", jniTypeReference.Substring (i)),
									nameof (jniTypeReference));
						if (s != jniTypeReference.Length - 1)
							throw new ArgumentException (
									string.Format ("Malformed jNI type reference: invalid trailing text: '{0}'.", jniTypeReference.Substring (i)),
									nameof (jniTypeReference));
						n   = jniTypeReference.Substring (i + 1, s - i - 1);
					}
					break;
				default:
					if (i != 0)
						throw new ArgumentException (
								string.Format ("Malformed JNI type reference: found unrecognized char '{0}' in '{1}'.",
									jniTypeReference [i], jniTypeReference),
								nameof (jniTypeReference));
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
		}
	}
}

