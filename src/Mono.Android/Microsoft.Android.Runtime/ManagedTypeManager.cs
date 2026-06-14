using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Microsoft.Android.Runtime;

class ManagedTypeManager : JniRuntime.JniTypeManager {

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

	public ManagedTypeManager ()
	{
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
	{
		const string suffix = "Invoker";

		// https://github.com/xamarin/xamarin-android/blob/5472eec991cc075e4b0c09cd98a2331fb93aa0f3/src/Microsoft.Android.Sdk.ILLink/MarkJavaObjects.cs#L176-L186
		const string assemblyGetTypeMessage = "'Invoker' types are preserved by the MarkJavaObjects trimmer step.";
		const string makeGenericTypeMessage = "Generic 'Invoker' types are preserved by the MarkJavaObjects trimmer step.";

		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = assemblyGetTypeMessage)]
		[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = assemblyGetTypeMessage)]
		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? AssemblyGetType (Assembly assembly, string typeName) =>
			assembly.GetType (typeName);

		[UnconditionalSuppressMessage ("Trimming", "IL2055", Justification = makeGenericTypeMessage)]
		[return: DynamicallyAccessedMembers (Constructors)]
		static Type MakeGenericType (
				[DynamicallyAccessedMembers (Constructors)]
				Type type,
				Type [] arguments) =>
			// FIXME: https://github.com/dotnet/java-interop/issues/1192
			#pragma warning disable IL3050
			type.MakeGenericType (arguments);
			#pragma warning restore IL3050

		Type[] arguments = type.GetGenericArguments ();
		if (arguments.Length == 0)
			return AssemblyGetType (type.Assembly, type + suffix) ?? base.GetInvokerTypeCore (type);
		Type definition = type.GetGenericTypeDefinition ();
		int bt = definition.FullName!.IndexOf ("`", StringComparison.Ordinal);
		if (bt == -1)
			throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);
		Type? suffixDefinition = AssemblyGetType (definition.Assembly,
				definition.FullName.Substring (0, bt) + suffix + definition.FullName.Substring (bt));
		if (suffixDefinition == null)
			return base.GetInvokerTypeCore (type);
		return MakeGenericType (suffixDefinition, arguments);
	}

	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			ReadOnlySpan<char> methods)
	{
		if (!NativeMethodRegistrar.TryRegisterNativeMembers (nativeClass, type, methods)) {
			base.RegisterNativeMembers (nativeClass, type, methods);
		}
	}

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		// Base class contains built-in mappings (e.g. java/lang/String → System.String)
		// which must take priority over ManagedTypeMapping (which would return Java.Lang.String).
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference)) {
			yield return t;
		}
		if (ManagedTypeMapping.TryGetType (jniSimpleReference, out var target)) {
			yield return target;
		}
	}

	protected override IEnumerable<string> GetSimpleReferences (Type type)
	{
		foreach (var r in base.GetSimpleReferences (type)) {
			yield return r;
		}

		if (ManagedTypeMapping.TryGetJniName (type, out var jniName)) {
			yield return jniName;
		}
	}

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
	{
		return JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: false);
	}
}
