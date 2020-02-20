using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;
using Mono.Collections.Generic;

namespace Java.Interop.Tools.Cecil {

	public static class MethodDefinitionRocks
	{
		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static MethodDefinition GetBaseDefinition (this MethodDefinition method) =>
			GetBaseDefinition (method, cache: null);

		public static MethodDefinition GetBaseDefinition (this MethodDefinition method, TypeDefinitionCache cache)
		{
			if (method.IsStatic || method.IsNewSlot || !method.IsVirtual)
				return method;

			foreach (var baseType in method.DeclaringType.GetBaseTypes (cache)) {
				foreach (var m in baseType.Methods) {
					if (!m.IsConstructor &&
							m.Name == method.Name &&
							(m.IsVirtual || m.IsAbstract) &&
							AreParametersCompatibleWith (m.Parameters, method.Parameters, cache)) {
						return m;
					}
				}
			}
			return method;
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static IEnumerable<MethodDefinition> GetOverriddenMethods (MethodDefinition method, bool inherit) =>
			GetOverriddenMethods (method, inherit, cache: null);

		public static IEnumerable<MethodDefinition> GetOverriddenMethods (MethodDefinition method, bool inherit, TypeDefinitionCache cache)
		{
			yield return method;
			if (inherit) {
				MethodDefinition baseMethod = method;
				while ((baseMethod = method.GetBaseDefinition (cache)) != null && baseMethod != method) {
					yield return method;
					method = baseMethod;
				}
			}
		}

		[Obsolete ("Use the TypeDefinitionCache overload for better performance.")]
		public static bool AreParametersCompatibleWith (this Collection<ParameterDefinition> a, Collection<ParameterDefinition> b) =>
			AreParametersCompatibleWith (a, b, cache: null);

		public static bool AreParametersCompatibleWith (this Collection<ParameterDefinition> a, Collection<ParameterDefinition> b, TypeDefinitionCache cache)
		{
			if (a.Count != b.Count)
				return false;

			if (a.Count == 0)
				return true;

			for (int i = 0; i < a.Count; i++)
				if (!IsParameterCompatibleWith (a [i].ParameterType, b [i].ParameterType, cache))
					return false;

			return true;
		}

		static bool IsParameterCompatibleWith (IModifierType a, IModifierType b, TypeDefinitionCache cache)
		{
			if (!IsParameterCompatibleWith (a.ModifierType, b.ModifierType, cache))
				return false;

			return IsParameterCompatibleWith (a.ElementType, b.ElementType, cache);
		}

		static bool IsParameterCompatibleWith (TypeSpecification a, TypeSpecification b, TypeDefinitionCache cache)
		{
			if (a is GenericInstanceType)
				return IsParameterCompatibleWith ((GenericInstanceType) a, (GenericInstanceType) b, cache);

			if (a is IModifierType)
				return IsParameterCompatibleWith ((IModifierType) a, (IModifierType) b, cache);

			return IsParameterCompatibleWith (a.ElementType, b.ElementType, cache);
		}

		static bool IsParameterCompatibleWith (GenericInstanceType a, GenericInstanceType b, TypeDefinitionCache cache)
		{
			if (!IsParameterCompatibleWith (a.ElementType, b.ElementType, cache))
				return false;

			if (a.GenericArguments.Count != b.GenericArguments.Count)
				return false;

			if (a.GenericArguments.Count == 0)
				return true;

			for (int i = 0; i < a.GenericArguments.Count; i++)
				if (!IsParameterCompatibleWith (a.GenericArguments [i], b.GenericArguments [i], cache))
					return false;

			return true;
		}

		static bool IsParameterCompatibleWith (TypeReference a, TypeReference b, TypeDefinitionCache cache)
		{
			if (a is TypeSpecification || b is TypeSpecification) {
				if (a.GetType () != b.GetType ())
					return false;

				return IsParameterCompatibleWith ((TypeSpecification) a, (TypeSpecification) b, cache);
			}

			if (a.IsGenericParameter) {
				if (b.IsGenericParameter && a.Name == b.Name)
					return true;
				var gpa = (GenericParameter) a;
				foreach (var c in gpa.Constraints) {
					if (!c.ConstraintType.IsAssignableFrom (b, cache))
						return false;
				}
				return true;
			}

			return a.FullName == b.FullName;
		}
	}
}

