#nullable enable

using Mono.Cecil;
using Mono.Collections.Generic;
using Mono.Linker;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.ILLink
{
	public static class LinkContextExtensions
	{
		public static MethodDefinition GetBaseDefinition (this LinkContext context, MethodDefinition method)
		{
			if (method.IsStatic || method.IsNewSlot || !method.IsVirtual)
				return method;

			foreach (var baseType in context.GetBaseTypes (method.DeclaringType)) {
				foreach (var m in baseType.Methods) {
					if (!m.IsConstructor &&
							m.Name == method.Name &&
							(m.IsVirtual || m.IsAbstract) &&
							context.AreParametersCompatibleWith (m.Parameters, method.Parameters)) {
						return m;
					}
				}
			}
			return method;
		}

		public static bool AreParametersCompatibleWith (this LinkContext context, Collection<ParameterDefinition> a, Collection<ParameterDefinition> b)
		{
			if (a.Count != b.Count)
				return false;

			if (a.Count == 0)
				return true;

			for (int i = 0; i < a.Count; i++)
				if (!context.IsParameterCompatibleWith (a [i].ParameterType, b [i].ParameterType))
					return false;

			return true;
		}

		static bool IsParameterCompatibleWith (this LinkContext context, IModifierType a, IModifierType b)
		{
			if (!context.IsParameterCompatibleWith (a.ModifierType, b.ModifierType))
				return false;

			return context.IsParameterCompatibleWith (a.ElementType, b.ElementType);
		}

		static bool IsParameterCompatibleWith (this LinkContext context, TypeSpecification a, TypeSpecification b)
		{
			if (a is GenericInstanceType)
				return context.IsParameterCompatibleWith ((GenericInstanceType) a, (GenericInstanceType) b);

			if (a is IModifierType)
				return context.IsParameterCompatibleWith ((IModifierType) a, (IModifierType) b);

			return context.IsParameterCompatibleWith (a.ElementType, b.ElementType);
		}

		static bool IsParameterCompatibleWith (this LinkContext context, GenericInstanceType a, GenericInstanceType b)
		{
			if (!context.IsParameterCompatibleWith (a.ElementType, b.ElementType))
				return false;

			if (a.GenericArguments.Count != b.GenericArguments.Count)
				return false;

			if (a.GenericArguments.Count == 0)
				return true;

			for (int i = 0; i < a.GenericArguments.Count; i++)
				if (!context.IsParameterCompatibleWith (a.GenericArguments [i], b.GenericArguments [i]))
					return false;

			return true;
		}

		static bool IsParameterCompatibleWith (this LinkContext context, TypeReference a, TypeReference b)
		{
			if (a is TypeSpecification || b is TypeSpecification) {
				if (a.GetType () != b.GetType ())
					return false;

				return context.IsParameterCompatibleWith ((TypeSpecification) a, (TypeSpecification) b);
			}

			if (a.IsGenericParameter) {
				if (b.IsGenericParameter && a.Name == b.Name)
					return true;
				var gpa = (GenericParameter) a;
				foreach (var c in gpa.Constraints) {
					if (!context.IsAssignableFrom (c.ConstraintType, b))
						return false;
				}
				return true;
			}

			return a.FullName == b.FullName;
		}

		public static TypeDefinition? GetBaseType (this LinkContext context, TypeDefinition type)
		{
			var bt = type.BaseType;
			if (bt == null)
				return null;
			return context.ResolveTypeDefinition (bt);
		}

		public static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (this LinkContext context, TypeDefinition type)
		{
			TypeDefinition? t = type;

			while (t != null) {
				yield return t;
				t = context.GetBaseType (t);
			}
		}

		public static IEnumerable<TypeDefinition> GetBaseTypes (this LinkContext context, TypeDefinition type)
		{
			TypeDefinition? t = type;

			while ((t = context.GetBaseType (t)) != null) {
				yield return t;
			}
		}

		public static bool IsAssignableFrom (this LinkContext context, TypeReference type, TypeReference c)
		{
			if (type.FullName == c.FullName)
				return true;
			var d = context.TryResolveTypeDefinition (c);
			if (d == null)
				return false;
			foreach (var t in context.GetTypeAndBaseTypes (d)) {
				if (type.FullName == t.FullName)
					return true;
				foreach (var ifaceImpl in t.Interfaces) {
					var i   = ifaceImpl.InterfaceType;
					if (context.IsAssignableFrom (type, i))
						return true;
				}
			}
			return false;
		}

		public static bool IsSubclassOf (this LinkContext context, TypeDefinition type, string typeName)
		{
			foreach (var t in context.GetTypeAndBaseTypes (type)) {
				if (t.FullName == typeName) {
					return true;
				}
			}
			return false;
		}
	}
}