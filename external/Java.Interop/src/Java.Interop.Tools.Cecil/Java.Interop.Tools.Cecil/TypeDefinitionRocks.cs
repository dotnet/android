using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

namespace Java.Interop.Tools.Cecil {

	public static class TypeDefinitionRocks {

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static TypeDefinition GetBaseType (this TypeDefinition type) =>
			GetBaseType (type, cache: null);

		public static TypeDefinition GetBaseType (this TypeDefinition type, TypeDefinitionCache cache)
		{
			var bt = type.BaseType;
			if (bt == null)
				return null;
			if (cache != null)
				return cache.Resolve (bt);
			return bt.Resolve ();
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this TypeDefinition type) =>
			GetTypeAndBaseTypes (type, cache: null);

		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this TypeDefinition type, TypeDefinitionCache cache)
		{
			while (type != null) {
				yield return type;
				type = type.GetBaseType (cache);
			}
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static IEnumerable<TypeDefinition> GetBaseTypes (this TypeDefinition type) =>
			GetBaseTypes (type, cache: null);

		public static IEnumerable<TypeDefinition> GetBaseTypes (this TypeDefinition type, TypeDefinitionCache cache)
		{
			while ((type = type.GetBaseType (cache)) != null) {
				yield return type;
			}
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool IsAssignableFrom (this TypeReference type, TypeReference c) =>
			IsAssignableFrom (type, c, cache: null);

		public static bool IsAssignableFrom (this TypeReference type, TypeReference c, TypeDefinitionCache cache)
		{
			if (type.FullName == c.FullName)
				return true;
			var d = c.Resolve ();
			if (d == null)
				return false;
			foreach (var t in d.GetTypeAndBaseTypes (cache)) {
				if (type.FullName == t.FullName)
					return true;
				foreach (var ifaceImpl in t.Interfaces) {
					var i   = ifaceImpl.InterfaceType;
					if (IsAssignableFrom (type, i, cache))
						return true;
				}
			}
			return false;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool IsSubclassOf (this TypeDefinition type, string typeName) =>
			IsSubclassOf (type, typeName, cache: null);

		public static bool IsSubclassOf (this TypeDefinition type, string typeName, TypeDefinitionCache cache)
		{
			return type.GetTypeAndBaseTypes (cache).Any (t => t.FullName == typeName);
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool ImplementsInterface (this TypeDefinition type, string interfaceName) =>
			ImplementsInterface (type, interfaceName, cache: null);

		public static bool ImplementsInterface (this TypeDefinition type, string interfaceName, TypeDefinitionCache cache)
		{
			foreach (var t in type.GetTypeAndBaseTypes (cache)) {
				foreach (var i in t.Interfaces) {
					if (i.InterfaceType.FullName == interfaceName) {
						return true;
					}
				}
			}
			return false;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static string GetPartialAssemblyName (this TypeReference type) =>
			GetPartialAssemblyName (type, cache: null);

		public static string GetPartialAssemblyName (this TypeReference type, TypeDefinitionCache cache)
		{
			TypeDefinition def = cache != null ? cache.Resolve (type) : type.Resolve ();
			return (def ?? type).Module.Assembly.Name.Name;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static string GetPartialAssemblyQualifiedName (this TypeReference type) =>
			GetPartialAssemblyQualifiedName (type, cache: null);

		public static string GetPartialAssemblyQualifiedName (this TypeReference type, TypeDefinitionCache cache)
		{
			return string.Format ("{0}, {1}",
					// Cecil likes to use '/' as the nested type separator, while
					// Reflection uses '+' as the nested type separator. Use Reflection.
					type.FullName.Replace ('/', '+'),
					type.GetPartialAssemblyName (cache));
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static string GetAssemblyQualifiedName (this TypeReference type) =>
			GetAssemblyQualifiedName (type, cache: null);

		public static string GetAssemblyQualifiedName (this TypeReference type, TypeDefinitionCache cache)
		{
			TypeDefinition def = cache != null ? cache.Resolve (type) : type.Resolve ();
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
