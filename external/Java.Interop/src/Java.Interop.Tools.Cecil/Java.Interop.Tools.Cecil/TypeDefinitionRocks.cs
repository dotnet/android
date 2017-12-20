using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

namespace Java.Interop.Tools.Cecil {

	public static class TypeDefinitionRocks {

		public static TypeDefinition GetBaseType (this TypeDefinition type)
		{
			var bt = type.BaseType;
			return bt == null ? null : bt.Resolve ();
		}

		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this TypeDefinition type)
		{
			while (type != null) {
				yield return type;
				type = type.GetBaseType ();
			}
		}

		public static IEnumerable<TypeDefinition> GetBaseTypes (this TypeDefinition type)
		{
			while ((type = type.GetBaseType ()) != null) {
				yield return type;
			}
		}

		public static bool IsAssignableFrom (this TypeReference type, TypeReference c)
		{
			if (type.FullName == c.FullName)
				return true;
			var d = c.Resolve ();
			if (d == null)
				return false;
			foreach (var t in d.GetTypeAndBaseTypes ()) {
				if (type.FullName == t.FullName)
					return true;
				if (!t.HasInterfaces)
					continue;
				foreach (var ifaceImpl in t.Interfaces) {
					var i   = ifaceImpl.InterfaceType;
					if (IsAssignableFrom (type, i))
						return true;
				}
			}
			return false;
		}

		public static bool IsSubclassOf (this TypeDefinition type, string typeName)
		{
			return type.GetTypeAndBaseTypes ().Any (t => t.FullName == typeName);
		}

		public static bool ImplementsInterface (this TypeDefinition type, string interfaceName)
		{
			return type.GetTypeAndBaseTypes ().Any (t => t.HasInterfaces &&
					t.Interfaces.Any (i => i.InterfaceType.FullName == interfaceName));
		}

		public static string GetPartialAssemblyQualifiedName (this TypeReference type)
		{
			TypeDefinition def = type.Resolve ();
			return string.Format ("{0}, {1}",
					// Cecil likes to use '/' as the nested type separator, while
					// Reflection uses '+' as the nested type separator. Use Reflection.
					type.FullName.Replace ('/', '+'),
					(def ?? type).Module.Assembly.Name.Name);
		}

		public static string GetAssemblyQualifiedName (this TypeReference type)
		{
			TypeDefinition def = type.Resolve ();
			return string.Format ("{0}, {1}",
					// Cecil likes to use '/' as the nested type separator, while
					// Reflection uses '+' as the nested type separator. Use Reflection.
					type.FullName.Replace ('/', '+'),
					(def ?? type).Module.Assembly.Name.FullName);
		}

		public static TypeDefinition GetNestedType (this TypeDefinition type, string name)
		{
			if (type == null)
				return null;

			foreach (TypeDefinition t in type.NestedTypes)
				if (t.Name == name || t.FullName == name)
					return t;

			return null;
		}

		// Note: this is not recursive, so it will not find nested types.
		public static TypeDefinition FindType (this ModuleDefinition module, string name)
		{
			if (module == null)
				return null;

			foreach (TypeDefinition t in module.Types)
				if (t.Name == name || t.FullName == name)
					return t;

			return null;
		}
	}
}
