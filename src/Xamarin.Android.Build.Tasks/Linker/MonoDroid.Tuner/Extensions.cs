using System;
using System.Collections.Generic;

using Mono.Cecil;

using Mono.Linker;

using Mono.Tuner;

namespace MonoDroid.Tuner {

	static class Extensions {

		const string JavaObject = "Java.Lang.Object";
		const string IJavaObject = "Android.Runtime.IJavaObject";
		const string JavaThrowable = "Java.Lang.Throwable";

		public static bool IsJavaObject (this TypeDefinition type)
		{
			return type.Inherits (JavaObject);
		}

		public static bool IsJavaException (this TypeDefinition type)
		{
			return type.Inherits (JavaThrowable);
		}

		public static bool ImplementsIJavaObject (this TypeDefinition type)
		{
			return type.Implements (IJavaObject);
		}

		public static object GetSettableValue (this CustomAttributeArgument arg)
		{
			TypeReference tr = arg.Value as TypeReference;
			TypeDefinition td = tr != null ? tr.Resolve () : null;
			return td != null ? td.FullName + "," + td.Module.Assembly.FullName : arg.Value;
		}

		public static bool Implements (this TypeReference self, string interfaceName)
		{
			if (interfaceName == null)
				throw new ArgumentNullException ("interfaceName");
			if (self == null)
				return false;

			TypeDefinition type = self.Resolve ();
			if (type == null)
				return false;	// not enough information available

			// special case, check if we implement ourselves
			if (type.IsInterface && (type.FullName == interfaceName))
				return true;

			return Implements (type, interfaceName, (interfaceName.IndexOf ('`') >= 0));
		}

		public static bool Implements (TypeDefinition type, string interfaceName, bool generic)
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
						if (Implements (iface.Resolve (), interfaceName, generic))
							return true;
					}
				}

				type = type.BaseType != null ? type.BaseType.Resolve () : null;
			}
			return false;
		}

		public static bool Inherits (this TypeReference self, string className)
		{
			if (className == null)
				throw new ArgumentNullException ("className");
			if (self == null)
				return false;

			TypeReference current = self.Resolve ();
			while (current != null) {
				string fullname = current.FullName;
				if (fullname == className)
					return true;
				if (fullname == "System.Object")
					return false;

				TypeDefinition td = current.Resolve ();
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

		public static bool TryGetRegisterAttribute (this MethodDefinition method, out CustomAttribute register)
		{
			register = null;

			if (!method.HasCustomAttributes)
				return false;

			foreach (CustomAttribute attribute in method.CustomAttributes) {
				if (!IsRegisterAttribute (attribute))
					continue;

				register = attribute;
				return true;
			}

			return false;
		}

		public static bool TryGetRegisterMember (this MethodDefinition md, out string method)
		{
			CustomAttribute register;
			method = null;

			if (!md.TryGetRegisterAttribute (out register))
				return false;

			if (register.ConstructorArguments.Count != 3)
				return false;

			method = (string)register.ConstructorArguments [2].Value;

			if (string.IsNullOrEmpty (method))
				return false;

			return true;
		}
	}
}
