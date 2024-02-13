using System;
using System.Collections.Generic;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Mono.Linker;

using Mono.Tuner;

using Java.Interop;
using Java.Interop.Tools.Cecil;

namespace MonoDroid.Tuner {

	static class Extensions {

		const string JavaObject = "Java.Lang.Object";
		const string IJavaObject = "Android.Runtime.IJavaObject";
		const string IJavaPeerable = "Java.Interop.IJavaPeerable";
		const string JavaThrowable = "Java.Lang.Throwable";

		public static bool IsJavaObject (this TypeDefinition type, IMetadataResolver resolver)
		{
			return type.Inherits (JavaObject, resolver);
		}

		public static bool IsJavaException (this TypeDefinition type, IMetadataResolver resolver)
		{
			return type.Inherits (JavaThrowable, resolver);
		}

		public static bool ImplementsIJavaObject (this TypeDefinition type, IMetadataResolver resolver)
		{
			return type.Implements (IJavaObject, resolver);
		}

		public static bool ImplementsIJavaPeerable (this TypeDefinition type, IMetadataResolver resolver)
		{
			return type.Implements (IJavaPeerable, resolver);
		}

		public static object GetSettableValue (this CustomAttributeArgument arg, IMetadataResolver cache)
		{
			TypeReference tr = arg.Value as TypeReference;
			TypeDefinition td = tr != null ? cache.Resolve (tr) : null;
			return td != null ? td.FullName + "," + td.Module.Assembly.FullName : arg.Value;
		}

		public static AssemblyDefinition GetAssembly (this LinkContext context, string assemblyName)
		{
			AssemblyDefinition ad;
#if !NETCOREAPP
			context.TryGetLinkedAssembly (assemblyName, out ad);
#else
			ad = context.GetLoadedAssembly (assemblyName);
#endif
			return ad;
		}

		public static TypeDefinition GetType (this LinkContext context, string assemblyName, string typeName)
		{
			AssemblyDefinition ad = context.GetAssembly (assemblyName);
			return ad == null ? null : GetType (ad, typeName);
		}

		public static MethodDefinition GetMethod (this LinkContext context, string assemblyName, string typeName, string name, string [] parameters)
		{
			var type = context.GetType (assemblyName, typeName);
			if (type == null)
				return null;

			return GetMethod (type, name, parameters);
		}

		public static MethodDefinition GetMethod (TypeDefinition td, string name)
		{
			MethodDefinition method = null;
			foreach (var md in td.Methods) {
				if (md.Name == name) {
					method = md;
					break;
				}
			}

			return method;
		}

		public static MethodDefinition GetMethod (TypeDefinition type, string name, string [] parameters)
		{
			MethodDefinition method = null;
			foreach (var md in type.Methods) {
				if (md.Name != name)
					continue;

				if (md.Parameters.Count != parameters.Length)
					continue;

				var equal = true;
				for (int i = 0; i < parameters.Length; i++) {
					if (md.Parameters [i].ParameterType.FullName != parameters [i]) {
						equal = false;
						break;
					}
				}

				if (!equal)
					continue;

				method = md;
				break;
			}

			return method;
		}

		public static TypeDefinition GetType (AssemblyDefinition assembly, string typeName)
		{
			return assembly.MainModule.GetType (typeName);
		}

		public static bool Implements (this TypeReference self, string interfaceName, IMetadataResolver resolver)
		{
			if (interfaceName == null)
				throw new ArgumentNullException ("interfaceName");
			if (self == null)
				return false;

			TypeDefinition type = resolver.Resolve (self);
			if (type == null)
				return false;	// not enough information available

			// special case, check if we implement ourselves
			if (type.IsInterface && (type.FullName == interfaceName))
				return true;

			return Implements (type, interfaceName, (interfaceName.IndexOf ('`') >= 0), resolver);
		}

		public static bool Implements (TypeDefinition type, string interfaceName, bool generic, IMetadataResolver resolver)
		{
			while (type != null) {
				// does the type implements it itself
				if (type.HasInterfaces) {
					foreach (var ifaceInfo in type.Interfaces) {
						var iface       = ifaceInfo.InterfaceType;
						string fullname = (generic) ? iface.GetElementType ().FullName : iface.FullName;
						if (fullname == interfaceName)
							return true;
						//if not, then maybe one of its parent interfaces does
						if (Implements (resolver.Resolve (iface), interfaceName, generic, resolver))
							return true;
					}
				}

				if (type.BaseType != null) {
					type = resolver.Resolve (type.BaseType);
				} else {
					type = null;
				}
			}
			return false;
		}

		public static bool Inherits (this TypeReference self, string className, IMetadataResolver resolver)
		{
			if (className == null)
				throw new ArgumentNullException ("className");
			if (self == null)
				return false;

			TypeReference current = resolver.Resolve (self);
			while (current != null) {
				string fullname = current.FullName;
				if (fullname == className)
					return true;
				if (fullname == "System.Object")
					return false;

				TypeDefinition td = resolver.Resolve (current);
				if (td == null)
					return false;		// could not resolve type
				current = td.BaseType;
			}
			return false;
		}

		const string RegisterAttribute = "Android.Runtime.RegisterAttribute";

		private static bool IsRegisterAttribute (CustomAttribute attribute)
		{
			var constructor = attribute.Constructor;

			if (constructor.DeclaringType.FullName != RegisterAttribute)
				return false;

			if (!constructor.HasParameters)
				return false;

			if (constructor.Parameters.Count != 3)
				return false;

			return true;
		}

		static bool TryGetRegisterAttribute (ICustomAttributeProvider provider, out CustomAttribute register)
		{
			register = null;

			if (!provider.HasCustomAttributes)
				return false;

			foreach (CustomAttribute attribute in provider.CustomAttributes) {
				if (!IsRegisterAttribute (attribute))
					continue;

				register = attribute;
				return true;
			}

			return false;
		}

		public static bool TryGetRegisterMember (this MethodDefinition md, out string method)
		{
			return TryGetRegisterMember (md, out method, out _, out _);
		}

		public static bool TryGetRegisterMember (this MethodDefinition md, out string method, out string nativeMethod, out string signature)
		{
			CustomAttribute register;
			method = null;
			nativeMethod = null;
			signature = null;

			if (!TryGetRegisterAttribute (md, out register))
				return false;

			if (register.ConstructorArguments.Count != 3)
				return false;

			nativeMethod = (string)register.ConstructorArguments [0].Value;
			signature = (string)register.ConstructorArguments [1].Value;
			method = (string)register.ConstructorArguments [2].Value;

			if (string.IsNullOrEmpty (method))
				return false;

			return true;
		}

		public static TypeDefinition GetMarshalMethodsType (this TypeDefinition type)
		{
			foreach (var nt in type.NestedTypes) {
				if (nt.Name == "__<$>_jni_marshal_methods")
					return nt;
			}

			return null;
		}

		public static bool TryGetBaseOrInterfaceRegisterMember (this MethodDefinition method, IMetadataResolver resolver, out string member, out string nativeMethod, out string signature)
		{
			var type = method.DeclaringType;

			member = nativeMethod = signature = null;

			if (method.IsConstructor || type == null || !type.HasNestedTypes)
				return false;

			var m = method.GetBaseDefinition (resolver);

			while (m != null) {
				if (m == method)
					break;

				method = m;

				if (m.TryGetRegisterMember (out member, out nativeMethod, out signature))
					return true;

				m = m.GetBaseDefinition (resolver);
			}

			if (!method.DeclaringType.HasInterfaces || !method.IsNewSlot)
				return false;

			foreach (var iface in method.DeclaringType.Interfaces) {
				if (iface.InterfaceType.IsGenericInstance)
					continue;

				var itype = resolver.Resolve (iface.InterfaceType);
				if (itype == null || !itype.HasMethods)
					continue;

				foreach (var im in itype.Methods)
					if (im.IsEqual (method, resolver))
						return im.TryGetRegisterMember (out member, out nativeMethod, out signature);
				}

			return false;
		}

		public static bool IsEqual (this MethodDefinition m1, MethodDefinition m2, IMetadataResolver resolver)
		{
			if (m1.Name != m2.Name || m1.ReturnType.Name != m2.ReturnType.Name)
				return false;

			return m1.Parameters.AreParametersCompatibleWith (m2.Parameters, resolver);
		}

		public static bool TryGetMarshalMethod (this MethodDefinition method, string nativeMethod, string signature, out MethodDefinition marshalMethod)
		{
			marshalMethod = null;
			var type = method.DeclaringType;
			if (!type.HasNestedTypes)
				return false;

			TypeDefinition marshalType = type.GetMarshalMethodsType ();
			if (marshalType == null || !marshalType.HasMethods)
				return false;

			var marshalMethodName = GetMarshalMethodName (nativeMethod, signature);
			if (marshalMethodName == null)
				return false;

			foreach (var m in marshalType.Methods)
				if (m.Name == marshalMethodName) {
					marshalMethod = m;
					return true;
				}

			return false;
		}

		// Keep in sync with: https://github.com/xamarin/java.interop/blob/8ccb8374d242490d8d1b032f2c8ca7a813fd40f3/src/Java.Interop.Export/Java.Interop/MarshalMemberBuilder.cs#L405-L421
		public static string GetMarshalMethodName (string name, string signature)
		{
			if (name == null)
				throw new ArgumentNullException (nameof(name));

			if (signature == null)
				throw new ArgumentNullException (nameof(signature));

			var idx1 = signature.IndexOf ('(');
			var idx2 = signature.IndexOf (')');
			var arguments = signature;

			if (idx1 >= 0 && idx2 >= idx1)
				arguments = arguments.Substring (idx1 + 1, idx2 - idx1 - 1);

			return $"n_{name}{(string.IsNullOrEmpty (arguments) ? "" : "_")}{arguments?.Replace ('/', '_')?.Replace (';', '_')}";
		}

		public static Instruction CreateLoadArraySizeOrOffsetInstruction (int intValue)
		{
			if (intValue < 0)
				throw new ArgumentException ($"{nameof (intValue)} cannot be negative");

			if (intValue < 9) {
				switch (intValue) {
				case 0:
					return Instruction.Create (OpCodes.Ldc_I4_0);
				case 1:
					return Instruction.Create (OpCodes.Ldc_I4_1);
				case 2:
					return Instruction.Create (OpCodes.Ldc_I4_2);
				case 3:
					return Instruction.Create (OpCodes.Ldc_I4_3);
				case 4:
					return Instruction.Create (OpCodes.Ldc_I4_4);
				case 5:
					return Instruction.Create (OpCodes.Ldc_I4_5);
				case 6:
					return Instruction.Create (OpCodes.Ldc_I4_6);
				case 7:
					return Instruction.Create (OpCodes.Ldc_I4_7);
				case 8:
					return Instruction.Create (OpCodes.Ldc_I4_8);
				}
			}

			if (intValue < 128)
				return Instruction.Create (OpCodes.Ldc_I4_S, (sbyte)intValue);

			return Instruction.Create (OpCodes.Ldc_I4, intValue);
		}
	}
}
