using System;
using System.Collections.Generic;

using Mono.Cecil;

namespace Java.Interop.Tools.Cecil {

	public static class TypeDefinitionRocks {

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static TypeDefinition? GetBaseType (this TypeDefinition type) =>
			GetBaseType (type, resolver: null);

		public static TypeDefinition? GetBaseType (this TypeDefinition type, TypeDefinitionCache? cache) =>
			GetBaseType (type, (IMetadataResolver?) cache);

		public static TypeDefinition? GetBaseType (this TypeDefinition type, IMetadataResolver? resolver)
		{
			var bt = type.BaseType;
			if (bt == null)
				return null;
			if (resolver != null)
				return resolver.Resolve (bt);
			return bt.Resolve ();
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this TypeDefinition type) =>
			GetTypeAndBaseTypes (type, resolver: null);

		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this TypeDefinition type, TypeDefinitionCache? cache) =>
			GetTypeAndBaseTypes (type, (IMetadataResolver?) cache);

		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this TypeDefinition type, IMetadataResolver? resolver)
		{
			TypeDefinition? t = type;

			while (t != null) {
				yield return t;
				t = t.GetBaseType (resolver);
			}
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static IEnumerable<TypeDefinition> GetBaseTypes (this TypeDefinition type) =>
			GetBaseTypes (type, resolver: null);

		public static IEnumerable<TypeDefinition> GetBaseTypes (this TypeDefinition type, TypeDefinitionCache? cache) =>
			GetBaseTypes (type, (IMetadataResolver?) cache);

		public static IEnumerable<TypeDefinition> GetBaseTypes (this TypeDefinition type, IMetadataResolver? resolver)
		{
			TypeDefinition? t = type;

			while ((t = t.GetBaseType (resolver)) != null) {
				yield return t;
			}
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool IsAssignableFrom (this TypeReference type, TypeReference c) =>
			IsAssignableFrom (type, c, resolver: null);

		public static bool IsAssignableFrom (this TypeReference type, TypeReference c, TypeDefinitionCache? cache) =>
			IsAssignableFrom (type, c, (IMetadataResolver?) cache);

		public static bool IsAssignableFrom (this TypeReference type, TypeReference c, IMetadataResolver? resolver)
		{
			if (type.FullName == c.FullName)
				return true;
			var d = (resolver?.Resolve (c)) ?? c.Resolve ();
			if (d == null)
				return false;
			foreach (var t in d.GetTypeAndBaseTypes (resolver)) {
				if (type.FullName == t.FullName)
					return true;
				foreach (var ifaceImpl in t.Interfaces) {
					var i   = ifaceImpl.InterfaceType;
					if (IsAssignableFrom (type, i, resolver))
						return true;
				}
			}
			return false;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool IsSubclassOf (this TypeDefinition type, string typeName) =>
			IsSubclassOf (type, typeName, resolver: null);

		public static bool IsSubclassOf (this TypeDefinition type, string typeName, TypeDefinitionCache? cache) =>
			IsSubclassOf (type, typeName, (IMetadataResolver?) cache);
		public static bool IsSubclassOf (this TypeDefinition type, string typeName, IMetadataResolver? resolver)
		{
			foreach (var t in type.GetTypeAndBaseTypes (resolver)) {
				if (t.FullName == typeName) {
					return true;
				}
			}
			return false;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool ImplementsInterface (this TypeDefinition type, string interfaceName) =>
			ImplementsInterface (type, interfaceName, resolver: null);

		public static bool ImplementsInterface (this TypeDefinition type, string interfaceName, TypeDefinitionCache? cache) =>
			ImplementsInterface (type, interfaceName, (IMetadataResolver?) cache);

		public static bool ImplementsInterface (this TypeDefinition type, string interfaceName, IMetadataResolver? resolver)
		{
			foreach (var t in type.GetTypeAndBaseTypes (resolver)) {
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
			GetPartialAssemblyName (type, resolver: null);

		public static string GetPartialAssemblyName (this TypeReference type, TypeDefinitionCache? cache) =>
			GetPartialAssemblyName (type, (IMetadataResolver?) cache);

		public static string GetPartialAssemblyName (this TypeReference type, IMetadataResolver? resolver)
		{
			TypeDefinition? def = (resolver?.Resolve (type)) ?? type.Resolve ();
			return (def ?? type).Module.Assembly.Name.Name;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static string GetPartialAssemblyQualifiedName (this TypeReference type) =>
			GetPartialAssemblyQualifiedName (type, resolver: null);

		public static string GetPartialAssemblyQualifiedName (this TypeReference type, TypeDefinitionCache? cache) =>
			GetPartialAssemblyQualifiedName (type, (IMetadataResolver?) cache);

		public static string GetPartialAssemblyQualifiedName (this TypeReference type, IMetadataResolver? resolver)
		{
			return string.Format ("{0}, {1}",
					// Cecil likes to use '/' as the nested type separator, while
					// Reflection uses '+' as the nested type separator. Use Reflection.
					type.FullName.Replace ('/', '+'),
					type.GetPartialAssemblyName (resolver));
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static string GetAssemblyQualifiedName (this TypeReference type) =>
			GetAssemblyQualifiedName (type, resolver: null);

		public static string GetAssemblyQualifiedName (this TypeReference type, TypeDefinitionCache? cache) =>
			GetAssemblyQualifiedName (type, (IMetadataResolver?) cache);

		public static string GetAssemblyQualifiedName (this TypeReference type, IMetadataResolver? resolver)
		{
			TypeDefinition? def = (resolver?.Resolve (type)) ?? type.Resolve ();
			return string.Format ("{0}, {1}",
					// Cecil likes to use '/' as the nested type separator, while
					// Reflection uses '+' as the nested type separator. Use Reflection.
					type.FullName.Replace ('/', '+'),
					(def ?? type).Module.Assembly.Name.FullName);
		}

		public static TypeDefinition? GetNestedType (this TypeDefinition type, string name)
		{
			if (type == null)
				return null;

			foreach (TypeDefinition t in type.NestedTypes)
				if (t.Name == name || t.FullName == name)
					return t;

			return null;
		}

		// Note: this is not recursive, so it will not find nested types.
		public static TypeDefinition? FindType (this ModuleDefinition module, string name)
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
