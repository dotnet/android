#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Type manager for the trimmable typemap path. Delegates type lookups
/// to <see cref="TrimmableTypeMap"/>.
/// </summary>
class TrimmableTypeMapTypeManager : JniRuntime.JniTypeManager
{
	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference)) {
			yield return t;
		}

		if (TrimmableTypeMap.Instance.TryGetTargetType (jniSimpleReference, out var type)) {
			yield return type;
		}
	}

	protected override IEnumerable<string> GetSimpleReferences (Type type)
	{
		// In the trimmable typemap path, JNI names come from generated proxy metadata only.
		// Managed-only subclasses can still inherit the JNI name of a mapped base type.
		for (var currentType = type; currentType is not null; currentType = currentType.BaseType) {
			if (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (currentType, out var jniName)) {
				yield return jniName;
				yield break;
			}
		}
	}

	[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
	protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type type)
	{
		var invokerType = TrimmableTypeMap.Instance.GetInvokerType (type);
		if (invokerType != null) {
			return invokerType;
		}

		return base.GetInvokerTypeCore (type);
	}

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
	{
		int slash = jniSimpleReference.LastIndexOf ('/');
		var desugarType = slash > 0
			? $"{jniSimpleReference.Substring (0, slash + 1)}Desugar{jniSimpleReference.Substring (slash + 1)}"
			: $"Desugar{jniSimpleReference}";
		return new[] {
			$"{desugarType}$_CC",
			$"{jniSimpleReference}$-CC",
		};
	}

	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
			Type type,
			ReadOnlySpan<char> methods)
	{
		throw new UnreachableException (
			$"RegisterNativeMembers should not be called in the trimmable typemap path. " +
			$"Native methods for '{type.FullName}' should be registered by JCW static initializer blocks.");
	}
}
