using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;
using Mono.Collections.Generic;

namespace Java.Interop.Tools.Cecil {

	public static class MethodDefinitionRocks
	{
		public static MethodDefinition GetBaseDefinition (this MethodDefinition method)
		{
			if (method.IsStatic || method.IsNewSlot || !method.IsVirtual)
				return method;

			foreach (var baseType in method.DeclaringType.GetBaseTypes ()) {
				foreach (var m in baseType.Methods) {
					if (!m.IsConstructor &&
							m.Name == method.Name &&
							(m.IsVirtual || m.IsAbstract) &&
							AreParametersCompatibleWith (m.Parameters, method.Parameters)) {
						return m;
					}
				}
			}
			return method;
		}

		public static IEnumerable<MethodDefinition> GetOverriddenMethods (MethodDefinition method, bool inherit)
		{
			yield return method;
			if (inherit) {
				MethodDefinition baseMethod = method;
				while ((baseMethod = method.GetBaseDefinition ()) != null && baseMethod != method) {
					yield return method;
					method = baseMethod;
				}
			}
		}

		public static bool AreParametersCompatibleWith (this Collection<ParameterDefinition> a, Collection<ParameterDefinition> b)
		{
			if (a.Count != b.Count)
				return false;

			if (a.Count == 0)
				return true;

			for (int i = 0; i < a.Count; i++)
				if (!IsParameterCompatibleWith (a [i].ParameterType, b [i].ParameterType))
					return false;

			return true;
		}

		static bool IsParameterCompatibleWith (IModifierType a, IModifierType b)
		{
			if (!IsParameterCompatibleWith (a.ModifierType, b.ModifierType))
				return false;

			return IsParameterCompatibleWith (a.ElementType, b.ElementType);
		}

		static bool IsParameterCompatibleWith (TypeSpecification a, TypeSpecification b)
		{
			if (a is GenericInstanceType)
				return IsParameterCompatibleWith ((GenericInstanceType) a, (GenericInstanceType) b);

			if (a is IModifierType)
				return IsParameterCompatibleWith ((IModifierType) a, (IModifierType) b);

			return IsParameterCompatibleWith (a.ElementType, b.ElementType);
		}

		static bool IsParameterCompatibleWith (GenericInstanceType a, GenericInstanceType b)
		{
			if (!IsParameterCompatibleWith (a.ElementType, b.ElementType))
				return false;

			if (a.GenericArguments.Count != b.GenericArguments.Count)
				return false;

			if (a.GenericArguments.Count == 0)
				return true;

			for (int i = 0; i < a.GenericArguments.Count; i++)
				if (!IsParameterCompatibleWith (a.GenericArguments [i], b.GenericArguments [i]))
					return false;

			return true;
		}

		static bool IsParameterCompatibleWith (GenericParameter a, GenericParameter b)
		{
			return a.Position == b.Position;
		}

		static bool IsParameterCompatibleWith (TypeReference a, TypeReference b)
		{
			if (a is TypeSpecification || b is TypeSpecification) {
				if (a.GetType () != b.GetType ())
					return false;

				return IsParameterCompatibleWith ((TypeSpecification) a, (TypeSpecification) b);
			}

			if (a.IsGenericParameter) {
				if (b.IsGenericParameter && a.Name == b.Name)
					return true;
				var gpa = (GenericParameter) a;
				foreach (var c in gpa.Constraints) {
					if (!c.ConstraintType.IsAssignableFrom (b))
						return false;
				}
				return true;
			}

			return a.FullName == b.FullName;
		}
	}
}

