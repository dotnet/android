
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;


namespace Java.Interop {

	[RequiresDynamicCode ("JreTypeManager uses ReflectionJniTypeManager reflection-based behavior and is not compatible with Native AOT.")]
	[RequiresUnreferencedCode ("JreTypeManager uses ReflectionJniTypeManager reflection-based behavior and is not trimming-compatible.")]
	public class JreTypeManager : JniRuntime.ReflectionJniTypeManager {

		IDictionary<string, Type>? typeMappings;

		public JreTypeManager ()
			: this (null)
		{
		}

		public JreTypeManager (IDictionary<string, Type>? typeMappings)
		{
			this.typeMappings = typeMappings;
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return t;
			if (typeMappings == null)
				yield break;
			if (typeMappings.TryGetValue (jniSimpleReference, out var target))
				yield return target;
		}

		[return: DynamicallyAccessedMembers (JniRuntime.JniTypeManager.MethodsConstructors)]
		protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
		{
			var type = base.GetTypeForSimpleReference (jniSimpleReference);
			if (type != null)
				return type;
			if (typeMappings == null)
				return null;
			return typeMappings.TryGetValue (jniSimpleReference, out var target) ? target : null;
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			return base.GetSimpleReferences (type)
				.Concat (CreateSimpleReferencesEnumerator (type));
		}

		IEnumerable<string> CreateSimpleReferencesEnumerator (Type type)
		{
			if (typeMappings == null)
				yield break;
			foreach (var e in typeMappings) {
				if (e.Value == type)
					yield return e.Key;
			}
		}

		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
				Type type,
				ReadOnlySpan<char> methods)
		{
			if (base.TryRegisterNativeMembers (nativeClass, type, methods)) {
				return;
			}

			if (methods.IsEmpty) {
				return;
			}

			throw new NotSupportedException (
				$"Could not register native members for type '{type.FullName}'. " +
				"Ensure that the type has the appropriate [JniAddNativeMethodRegistration] attribute and static registration method.");
		}
	}
}
