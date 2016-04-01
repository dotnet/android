using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public static class CecilExtensions
	{
		public static string GetCorrectName (this TypeReference t)
		{
			return (t.DeclaringType != null ? t.DeclaringType.GetCorrectName () + "." + t.Name : t.Name).Replace ('/', '.');
		}

		public static string GetCorrectNamespace (this TypeReference t)
		{
			return t.DeclaringType != null ? t.DeclaringType.GetCorrectNamespace () : t.Namespace;
		}

		public static IEnumerable<TypeDefinition> FlattenTypes (this TypeDefinition t)
		{
			yield return t;
			foreach (var x in t.NestedTypes.SelectMany (nt => nt.FlattenTypes ()))
				yield return x;
		}

		public static IEnumerable<TypeDefinition> GetSelfAndAncestors (this TypeDefinition t)
		{
			yield return t;
			if (t.BaseType != null)
				foreach (var a in t.BaseType.Resolve ().GetSelfAndAncestors ())
					yield return t;
		}

		public static IEnumerable<MethodDefinition> GetMethods (this TypeDefinition t)
		{
			foreach (var m in t.Methods)
				yield return m;
			foreach (var p in t.Properties) {
				if (p.GetMethod != null)
					yield return p.GetMethod;
				if (p.SetMethod != null)
					yield return p.SetMethod;
			}
			if (t.BaseType != null)
				foreach (var m in t.BaseType.Resolve ().GetMethods ())
					yield return m;
			if (t.IsInterface)
				foreach (var it in t.Interfaces)
					foreach (var m in it.Resolve ().GetMethods ())
						yield return m;
		}

		public static IEnumerable<PropertyDefinition> GetProperties (this TypeDefinition t)
		{
			return t.BaseType == null ? t.Properties : t.Properties.Concat (t.BaseType.Resolve ().GetProperties ());
		}

		#region conversion between general interfaces

		public static TypeDefinition Value (this ManagedTypeFinder.IType t)
		{
			return ((ManagedTypeFinderCecil.TType) t).Value;
		}
		public static PropertyDefinition Value (this ManagedTypeFinder.IProperty p)
		{
			return ((ManagedTypeFinderCecil.TProperty) p).Value;
		}
		public static FieldDefinition Value (this ManagedTypeFinder.IDefinition t)
		{
			return ((ManagedTypeFinderCecil.TDefinition) t).Value;
		}
		public static MethodDefinition Value (this ManagedTypeFinder.IMethodBase t)
		{
			return ((ManagedTypeFinderCecil.TMethodBase) t).Value;
		}

		public static ManagedTypeFinder.IType Wrap (this TypeDefinition t)
		{
			return t == null ? null : new ManagedTypeFinderCecil.TType () { Value = t };
		}
		public static ManagedTypeFinder.IProperty WrapAsProperty (this PropertyDefinition t)
		{
			return t == null ? null : new ManagedTypeFinderCecil.TProperty () { Value = t };
		}
		public static ManagedTypeFinder.IDefinition WrapAsDefinition (this FieldDefinition t)
		{
			return t == null ? null : new ManagedTypeFinderCecil.TDefinition () { Value = t };
		}
		public static ManagedTypeFinder.IMethodBase Wrap (this MethodDefinition t)
		{
			return t == null ? null : new ManagedTypeFinderCecil.TMethodBase () { Value = t };
		}

		#endregion

		#region Utility methods for public

		public static ManagedApiQuery AsQuery (this IMemberDefinition member, int parameterIndex = -1)
		{
			if (member is TypeDefinition)
				return new ManagedApiQuery { TypeName = GetCorrectName ((TypeDefinition) member) };
			var type = member.DeclaringType;
			var typeName = GetCorrectName (type);
			if (member is FieldDefinition || member is PropertyDefinition)
				return new ManagedApiQuery { TypeName = typeName, MemberName = member.Name };
			var method = (MethodDefinition) member;
			return new ManagedApiQuery {
				TypeName = typeName,
				MemberName = member.Name,
				Arguments = method.Parameters.Select (p => GetCorrectName (p.ParameterType)).ToArray (),
				ParameterIndex = parameterIndex
			};
		}

		#endregion
	}
}

