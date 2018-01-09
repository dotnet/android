using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Android.Runtime;

#if HAVE_CECIL
using Mono.Cecil;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
#endif  // HAVE_CECIL

namespace Java.Interop.Tools.TypeNameMappings
{

#if HAVE_CECIL
	public
#endif
	enum PackageNamingPolicy {
		LowercaseHash,
		Lowercase,
		LowercaseWithAssemblyName,
	}

#if HAVE_CECIL
	public
#endif
	class JniTypeName
	{
		public string Type { get; internal set; }
		public bool IsKeyword { get; internal set; }
	}

#if HAVE_CECIL
	public
#endif
	static class JavaNativeTypeManager {

		public static PackageNamingPolicy PackageNamingPolicy { get; set; }

		public static string ApplicationJavaClass { get; set; }

		public static JniTypeName Parse (string jniType)
		{
			int _ = 0;
			return ExtractType (jniType, ref _);
		}

		public static IEnumerable<JniTypeName> FromSignature (string signature)
		{
			if (signature.StartsWith ("(")) {
				int e = signature.IndexOf (")");
				signature = signature.Substring (1, e >= 0 ? e-1 : signature.Length-1);
			}
			int i = 0;
			JniTypeName t;
			while ((t = ExtractType (signature, ref i)) != null)
				yield return t;
		}

		public static JniTypeName ReturnTypeFromSignature (string signature)
		{
			int idx = signature.LastIndexOf (')') + 1;
			return ExtractType (signature, ref idx);
		}

		// as per: http://java.sun.com/j2se/1.5.0/docs/guide/jni/spec/types.html
		static JniTypeName ExtractType (string signature, ref int index)
		{
			if (index >= signature.Length)
				return null;
			var i = index++;
			switch (signature [i]) {
				case '[': {
					++i;
					if (i >= signature.Length)
						throw new InvalidOperationException ("Missing array type after '[' at index " + i + " in: " + signature);
					JniTypeName r = ExtractType (signature, ref index);
					return new JniTypeName { Type = r.Type + "[]", IsKeyword = r.IsKeyword };
				}
				case 'B':
					return new JniTypeName { Type = "byte", IsKeyword = true };
				case 'C':
					return new JniTypeName { Type = "char", IsKeyword = true };
				case 'D':
					return new JniTypeName { Type = "double", IsKeyword = true };
				case 'F':
					return new JniTypeName { Type = "float", IsKeyword = true };
				case 'I':
					return new JniTypeName { Type = "int", IsKeyword = true };
				case 'J':
					return new JniTypeName { Type = "long", IsKeyword = true };
				case 'L': {
					var e = signature.IndexOf (";", index);
					if (e <= 0)
						throw new InvalidOperationException ("Missing reference type after 'L' at index " + i + "in: " + signature);
					var s = index;
					index = e + 1;
					return new JniTypeName {
						Type      = signature.Substring (s, e - s).Replace ("/", ".").Replace ("$", "."),
						IsKeyword = false,
					};
				}
				case 'S':
					return new JniTypeName { Type = "short", IsKeyword = true };
				case 'V':
					return new JniTypeName { Type = "void", IsKeyword = true };
				case 'Z':
					return new JniTypeName { Type = "boolean", IsKeyword = true };
				default:
					throw new InvalidOperationException ("Unknown JNI Type '" + signature [i] + "' within: " + signature);
			}
		}

		public static string ToCliType (string jniType)
		{
			if (string.IsNullOrEmpty (jniType))
				return jniType;
			string[] parts = jniType.Split ('/');
			for (int i = 0; i < parts.Length; ++i) {
				parts [i] = ToCliTypePart (parts [i]);
			}
			return string.Join (".", parts);
		}

		static string ToCliTypePart (string part)
		{
			if (part.IndexOf ('$') < 0)
				return ToPascalCase (part, 2);
			string[] parts = part.Split ('$');
			for (int i = 0; i < parts.Length; ++i) {
				parts [i] = ToPascalCase (parts [i], 1);
			}
			return string.Join ("/", parts);
		}

		static string ToPascalCase (string value, int minLength)
		{
			return value.Length <= minLength
				? value.ToUpperInvariant ()
				: char.ToUpperInvariant (value [0]) + value.Substring (1);
		}

		// Keep in sync with ToJniName(TypeDefinition)
		public static string ToJniName (Type type)
		{
			return ToJniName (type, ExportParameterKind.Unspecified) ??
				"java/lang/Object";
		}

		static string ToJniName (Type type, ExportParameterKind exportKind)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			if (type.IsValueType)
				return GetPrimitiveClass (type);

			if (type == typeof (string))
				return "java/lang/String";


			if (!type.GetInterfaces ().Any (t => t.FullName == "Android.Runtime.IJavaObject"))
				return GetSpecialExportJniType (type.FullName, exportKind);

			return ToJniName (type, t => t.DeclaringType, t => t.Name, GetPackageName, t => {
				return ToJniNameFromAttributes (t);
			}, _ => false);
		}

		public static string ToJniName (string jniType, int rank)
		{
			if (rank == 0)
				return jniType;

			if (jniType.Length > 1)
				jniType = "L" + jniType + ";";
			return new string ('[', rank) + jniType;
		}

		static bool IsPackageNamePreservedForAssembly (string assemblyName)
		{
			return assemblyName == "Mono.Android";
		}

		public static string GetPackageName (Type type)
		{
			string assemblyName = type.Assembly.GetName ().Name;
			if (IsPackageNamePreservedForAssembly (assemblyName))
				return type.Namespace.ToLowerInvariant ();
			switch (PackageNamingPolicy) {
			case PackageNamingPolicy.Lowercase:
				return type.Namespace.ToLowerInvariant ();
			case PackageNamingPolicy.LowercaseWithAssemblyName:
				return "assembly_" + (assemblyName.Replace ('.', '_') + "." + type.Namespace).ToLowerInvariant ();
			default:
				return "md5" + ToMd5 (type.Namespace + ":" + assemblyName);
			}
		}

		public static int GetArrayInfo (Type type, out Type elementType)
		{
			elementType = type;
			int rank = 0;
			while (type.IsArray) {
				rank++;
				elementType = type = type.GetElementType ();
			}
			return rank;
		}

		static string GetPrimitiveClass (Type type)
		{
			if (type.IsEnum)
				return GetPrimitiveClass (Enum.GetUnderlyingType (type));
			if (type == typeof (byte))
				return "B";
			if (type == typeof (char))
				return "C";
			if (type == typeof (double))
				return "D";
			if (type == typeof (float))
				return "F";
			if (type == typeof (int))
				return "I";
			if (type == typeof (long))
				return "J";
			if (type == typeof (short))
				return "S";
			if (type == typeof (bool))
				return "Z";
			return null;
		}

		static string GetSpecialExportJniType (string typeName, ExportParameterKind exportKind)
		{
			switch (exportKind) {
			case ExportParameterKind.InputStream:
				if (typeName != "System.IO.Stream")
					throw new ArgumentException ("ExportParameterKind.InputStream is valid only for System.IO.Stream parameter type");
				return "java/io/InputStream";
			case ExportParameterKind.OutputStream:
				if (typeName != "System.IO.Stream")
					throw new ArgumentException ("ExportParameterKind.OutputStream is valid only for System.IO.Stream parameter type");
				return "java/io/OutputStream";
			case ExportParameterKind.XmlPullParser:
				if (typeName != "System.Xml.XmlReader")
					throw new ArgumentException ("ExportParameterKind.XmlPullParser is valid only for System.Xml.XmlReader parameter type");
				return "org/xmlpull/v1/XmlPullParser";
			case ExportParameterKind.XmlResourceParser:
				if (typeName != "System.Xml.XmlReader")
					throw new ArgumentException ("ExportParameterKind.XmlResourceParser is valid only for System.Xml.XmlReader parameter type");
				return "android/content/res/XmlResourceParser";
			}
			// FIXME: this *must* error out here, instead of returning null.
			// Either Droidinator must be fixed to not reach here, or a global flag that skips this error check must be added.
			return null;
		}

		// Keep in sync with ToJniNameFromAttributes(TypeDefinition)
		public static string ToJniNameFromAttributes (Type type)
		{
			var aa = (IJniNameProviderAttribute []) type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			return aa.Length > 0 && !string.IsNullOrEmpty (aa [0].Name) ? aa [0].Name.Replace ('.', '/') : null;
		}

		/*
		 * Semantics: return `null` on "failure", DO NOT throw an exception.
		 *
		 * Why? tools/msbuild/Generator/JavaTypeInfo.cs!AddConstructors() attempts
		 * to generate (non-[Export]) constructors, and to determine whether or
		 * not the constructor CAN be declared it calls
		 * JniType.GetJniSignature(MethodDefinition). If GetJniSignature() returns
		 * null, it can't be exported, and the method is skipped.
		 *
		 * Callers of GetJniSignature() MUST check for `null` and behave
		 * appropriately.
		 */
		static string GetJniSignature<T,P> (IEnumerable<P> parameters, Func<P,T> getParameterType, Func<P,ExportParameterKind> getExportKind, T returnType, ExportParameterKind returnExportKind, Func<T,ExportParameterKind,string> getJniTypeName, bool isConstructor)
		{
			StringBuilder sb = new StringBuilder ().Append ("(");
			foreach (P p in parameters) {
				string jniType = getJniTypeName (getParameterType (p), getExportKind (p));
				if (jniType == null)
					return null;
				sb.Append (jniType);
			}
			sb.Append (')');
			if (isConstructor)
				sb.Append ("V");
			else {
				string jniType = getJniTypeName (returnType, returnExportKind);
				if (jniType == null)
					return null;
				sb.Append (jniType);
			}
			return sb.ToString ();
		}

		static string GetJniTypeName<TR,TD> (TR typeRef, ExportParameterKind exportKind, Func<TR,TD> resolve, Func<TR,KeyValuePair<int,TR>> getArrayInfo, Func<TD,string> getFullName, Func<TD,ExportParameterKind,string> toJniName)
		{
			TD ptype = resolve (typeRef);
			var p = getArrayInfo (typeRef);
			int rank = p.Key;
			TR etype = p.Value;
			ptype = resolve (etype);
			if (ptype == null) {
				// Likely caused by generic parameters, which we probably can't bind anyway.
				return null;
			}
			if (getFullName (ptype) == "System.Void")
				return "V";
			if (getFullName (ptype) == "System.IntPtr")
				// Probably a (IntPtr, JniHandleOwnership) parameter; skip
				return null;

			string pJniName = toJniName (ptype, exportKind);
			if (pJniName == null) {
				return null;
			}
			return rank == 0 && pJniName.Length > 1 ? "L" + pJniName + ";" : ToJniName (pJniName, rank);
		}

		static ExportParameterKind GetExportKind (System.Reflection.ICustomAttributeProvider p)
		{
			foreach (ExportParameterAttribute a in p.GetCustomAttributes (typeof (ExportParameterAttribute), false))
				return a.Kind;
			return ExportParameterKind.Unspecified;
		}

		public static string GetJniSignature (MethodInfo method)
		{
			return GetJniSignature<Type,ParameterInfo> (method.GetParameters (),
				p => p.ParameterType,
				p => GetExportKind (p),
				method.ReturnType,
				GetExportKind (method.ReturnParameter),
				(t, k) => GetJniTypeName (t, k),
				method.IsConstructor);
		}

		public static string GetJniTypeName (Type typeRef)
		{
			return GetJniTypeName (typeRef, ExportParameterKind.Unspecified);
		}

		internal static string GetJniTypeName (Type typeRef, ExportParameterKind exportKind)
		{
			return GetJniTypeName<Type,Type> (typeRef, exportKind, t => t, t => {
				Type etype;
				int rank = GetArrayInfo (t, out etype);
				return new KeyValuePair<int,Type> (rank, etype);
			}, t => t.FullName, (t, k) => ToJniNameWhichShouldReplaceExistingToJniName (t, k));
		}

		static string ToJniNameWhichShouldReplaceExistingToJniName (Type type, ExportParameterKind exportKind)
		{
			// we need some method that exactly does the same as ToJniName(TypeDefinition)
			var ret = ToJniNameFromAttributes (type);
			return ret ?? ToJniName (type, exportKind);
		}

#if HAVE_CECIL

		internal static ExportParameterKind GetExportKind (Mono.Cecil.ICustomAttributeProvider p)
		{
			foreach (CustomAttribute a in p.GetCustomAttributes (typeof (ExportParameterAttribute)))
				return ToExportParameterAttribute (a).Kind;
			return ExportParameterKind.Unspecified;
		}

		internal static ExportParameterAttribute ToExportParameterAttribute (CustomAttribute attr)
		{
			return new ExportParameterAttribute ((ExportParameterKind)attr.ConstructorArguments [0].Value);
		}

		public static bool IsApplication (TypeDefinition type)
		{
			return type.GetBaseTypes ().Any (b => b.FullName == "Android.App.Application");
		}

		public static bool IsInstrumentation (TypeDefinition type)
		{
			return type.GetBaseTypes ().Any (b => b.FullName == "Android.App.Instrumentation");
		}

		// moved from JavaTypeInfo
		public static string GetJniSignature (MethodDefinition method)
		{
			return GetJniSignature<TypeReference,ParameterDefinition> (
				method.Parameters,
				p => p.ParameterType,
				p => GetExportKind (p),
				method.ReturnType,
				GetExportKind (method.MethodReturnType),
				(t, k) => GetJniTypeName (t, k),
				method.IsConstructor);
		}

		// moved from JavaTypeInfo
		public static string GetJniTypeName (TypeReference typeRef)
		{
			return GetJniTypeName (typeRef, ExportParameterKind.Unspecified);
		}

		internal static string GetJniTypeName (TypeReference typeRef, ExportParameterKind exportKind)
		{
			return GetJniTypeName<TypeReference, TypeDefinition> (typeRef, exportKind, t => t.Resolve (), t => {
				TypeReference etype;
				int rank = GetArrayInfo (typeRef, out etype);
				return new KeyValuePair<int,TypeReference> (rank,etype);
				}, t => t.FullName, (t, k) => ToJniName (t, k));
		}

		public static string ToCompatJniName (Mono.Cecil.TypeDefinition type)
		{
			return ToJniName (type, t => t.DeclaringType, t => t.Name, ToCompatPackageName, ToJniNameFromAttributes, t => IsNonStaticInnerClass (t as TypeDefinition));
		}

		static string ToCompatPackageName (Mono.Cecil.TypeDefinition type)
		{
			return type.Namespace;
		}

		// Keep in sync with ToJniNameFromAttributes(Type) and ToJniName(Type)
		public static string ToJniName (Mono.Cecil.TypeDefinition type)
		{
			return ToJniName (type, ExportParameterKind.Unspecified) ??
				"java/lang/Object";
		}

		static string ToJniName (TypeDefinition type, ExportParameterKind exportKind)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			if (type.IsValueType)
				return GetPrimitiveClass (type);

			if (type.FullName == "System.String")
				return "java/lang/String";

			if (!type.ImplementsInterface ("Android.Runtime.IJavaObject")) {
				return GetSpecialExportJniType (type.FullName, exportKind);
			}

			return ToJniName (type, t => t.DeclaringType, t => t.Name, GetPackageName, ToJniNameFromAttributes, t => IsNonStaticInnerClass (t as TypeDefinition));
		}

		static string ToJniNameFromAttributes (TypeDefinition type)
		{
			#region CustomAttribute alternate name support
			var attrs = type.CustomAttributes.Where (a => a.AttributeType.Resolve ().Interfaces.Any (it => it.InterfaceType.FullName == typeof (IJniNameProviderAttribute).FullName));
			return attrs.Select (attr => {
				var ap = attr.Properties.FirstOrDefault (p => p.Name == "Name");
				string name = null;
				if (ap.Name == null) {
					var ca = attr.ConstructorArguments.FirstOrDefault ();
					if (ca.Type == null || ca.Type.FullName != "System.String")
						return null;
					name = (string) ca.Value;
				} else
					name = (string) ap.Argument.Value;
				if (!string.IsNullOrEmpty (name))
					return name.Replace ('.', '/');
				else
					return null;
				})
				.FirstOrDefault (s => s != null);
			#endregion
		}

		public static int GetArrayInfo (Mono.Cecil.TypeReference type, out Mono.Cecil.TypeReference elementType)
		{
			elementType = type;
			int rank = 0;
			while (type.IsArray) {
				rank++;
				elementType = type = type.GetElementType ();
			}
			return rank;
		}

		static string GetPrimitiveClass (Mono.Cecil.TypeDefinition type)
		{
			if (type.IsEnum)
				return GetPrimitiveClass (type.Fields.First (f => f.IsSpecialName).FieldType.Resolve ());
			if (type.FullName == "System.Byte")
				return "B";
			if (type.FullName == "System.Char")
				return "C";
			if (type.FullName == "System.Double")
				return "D";
			if (type.FullName == "System.Single")
				return "F";
			if (type.FullName == "System.Int32")
				return "I";
			if (type.FullName == "System.Int64")
				return "J";
			if (type.FullName == "System.Int16")
				return "S";
			if (type.FullName == "System.Boolean")
				return "Z";
			return null;
		}

		public static string GetPackageName (TypeDefinition type)
		{
			if (IsPackageNamePreservedForAssembly (type.Module.Assembly.Name.Name))
				return type.Namespace.ToLowerInvariant ();
			switch (PackageNamingPolicy) {
			case PackageNamingPolicy.Lowercase:
				return type.Namespace.ToLowerInvariant ();
			case PackageNamingPolicy.LowercaseWithAssemblyName:
				return "assembly_" + (type.Module.Assembly.Name.Name.Replace ('.', '_') + "." + type.Namespace).ToLowerInvariant ();
			default:
				return "md5" + ToMd5 (type.Namespace + ":" + type.Module.Assembly.Name.Name);
			}
		}
#endif

		static string ToJniName<T> (T type, Func<T, T> decl, Func<T, string> name, Func<T, string> ns, Func<T, string> overrideName, Func<T,bool> shouldUpdateName)
			where T : class
		{
			var nameParts   = new List<string> ();
			var typeName    = (string) null;
			var nsType      = type;

			for (var declType = type ; declType != null; declType = decl (declType)) {
				nsType      = declType;
				typeName    = overrideName (declType);
				if (typeName != null) {
					break;
				}
				var n   = name (declType).Replace ('`', '_');
				if (shouldUpdateName (declType)) {
					n = "$" + name (decl (declType)) + "_" + n;
				}
				nameParts.Add (n);
			}

			if (nameParts.Count == 0 && typeName != null)
				return typeName;

			nameParts.Reverse ();

			var nestedSuffix    = string.Join ("_", nameParts.ToArray ()).Replace ("_$", "$");
			if (typeName != null)
				return (typeName + "_" + nestedSuffix).Replace ("_$", "$");;

			// Results in namespace/parts/OuterType_InnerType
			// We do this to simplify monodroid type generation
			typeName = nestedSuffix;
			var _ns = ToLowerCase (ns (nsType));
			return string.IsNullOrEmpty (_ns)
				? typeName
				: _ns.Replace ('.', '/') + "/" + typeName;
		}

#if HAVE_CECIL
		internal static bool IsNonStaticInnerClass (TypeDefinition type)
		{
			if (type == null)
				return false;
			if (!type.IsNested)
				return false;

			if (!type.DeclaringType.IsSubclassOf ("Java.Lang.Object"))
				return false;

			return GetBaseConstructors (type)
				.Any (ctor => ctor.Parameters.Any (p => p.Name == "__self"));
		}

		static IEnumerable<MethodDefinition> GetBaseConstructors (TypeDefinition type)
		{
			var baseType = type.GetBaseTypes ().FirstOrDefault (t => t.GetCustomAttributes (typeof (RegisterAttribute)).Any ());
			if (baseType != null)
				return baseType.Methods.Where (m => m.IsConstructor && !m.IsStatic);
			return Enumerable.Empty<MethodDefinition> ();
		}
#endif  // HAVE_CECIL

		static string ToMd5 (string value)
		{
			var data = Encoding.UTF8.GetBytes (value);
			using (var md5 = MD5.Create ()) {
				var hash    = md5.ComputeHash (data);
				var buf     = new StringBuilder (hash.Length * 2);
				foreach (var b in hash)
					buf.AppendFormat ("{0:x2}", b);
				return buf.ToString ();
			}
		}

		static string ToLowerCase (string value)
		{
			if (string.IsNullOrEmpty (value))
				return value;
			string[] parts = value.Split ('.');
			for (int i = 0; i < parts.Length; ++i) {
				parts [i] = parts [i].ToLowerInvariant ();
			}
			return string.Join (".", parts);
		}
	}
}


