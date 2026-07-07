using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Microsoft.Android.Runtime;

[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Temporary suppression for Java.Interop reflection manager base.")]
[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Temporary suppression for Java.Interop reflection manager base.")]
class ManagedTypeManager : JniRuntime.ReflectionJniTypeManager {

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;
	internal const DynamicallyAccessedMemberTypes MethodsConstructors = MethodsAndPrivateNested | Constructors;

	public ManagedTypeManager ()
	{
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "'Invoker' types are preserved by the MarkJavaObjects trimmer step.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2055", Justification = "'Invoker' types are preserved by the MarkJavaObjects trimmer step.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = "Generic 'Invoker' types are preserved by the MarkJavaObjects trimmer step.")]
	[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Generic 'Invoker' types may not be available in AOT scenarios.")]
	protected override Type? GetInvokerTypeCore (Type type)
	{
		const string suffix = "Invoker";

		Type[] arguments = type.GetGenericArguments ();
		if (arguments.Length == 0)
			return type.Assembly.GetType (type + suffix) ?? base.GetInvokerTypeCore (type);
		Type definition = type.GetGenericTypeDefinition ();
		int bt = definition.FullName!.IndexOf ("`", StringComparison.Ordinal);
		if (bt == -1)
			throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);
		Type? suffixDefinition = definition.Assembly.GetType (
				definition.FullName.Substring (0, bt) + suffix + definition.FullName.Substring (bt));
		if (suffixDefinition == null)
			return base.GetInvokerTypeCore (type);
		return suffixDefinition.MakeGenericType (arguments);
	}

	// NOTE: suppressions below also in `src/Mono.Android/Android.Runtime/AndroidRuntime.cs`
	[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Type.GetType() can never statically know the string value parsed from parameter 'methods'.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Delegate.CreateDelegate() can never statically know the string value parsed from parameter 'methods'.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Delegate.CreateDelegate() can never statically know the string value parsed from parameter 'methods'.")]
	[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "JniNativeMethodRegistration[] registration path will be migrated to the blittable RegisterNatives overload in a future change.")]
	public override void RegisterNativeMembers (
			JniType nativeClass,
			Type type,
			ReadOnlySpan<char> methods)
	{
		if (methods.IsEmpty) {
			base.RegisterNativeMembers (nativeClass, type, methods);
			return;
		}

		int methodCount = CountMethods (methods);
		if (methodCount < 1) {
			base.RegisterNativeMembers (nativeClass, type, methods);
			return;
		}

		JniNativeMethodRegistration [] natives = new JniNativeMethodRegistration [methodCount];
		int nativesIndex = 0;

		ReadOnlySpan<char> methodsSpan = methods;
		bool needToRegisterNatives = false;

		while (!methodsSpan.IsEmpty) {
			int newLineIndex = methodsSpan.IndexOf ('\n');

			ReadOnlySpan<char> methodLine = methodsSpan.Slice (0, newLineIndex != -1 ? newLineIndex : methodsSpan.Length);
			if (!methodLine.IsEmpty) {
				SplitMethodLine (methodLine,
					out ReadOnlySpan<char> name,
					out ReadOnlySpan<char> signature,
					out ReadOnlySpan<char> callbackString,
					out ReadOnlySpan<char> callbackDeclaringTypeString);

				Delegate? callback = null;
				if (callbackString.SequenceEqual ("__export__")) {
					throw new InvalidOperationException (FormattableString.Invariant ($"Methods such as {callbackString.ToString ()} are not implemented!"));
				} else {
					Type callbackDeclaringType = type;
					if (!callbackDeclaringTypeString.IsEmpty) {
						callbackDeclaringType = Type.GetType (callbackDeclaringTypeString.ToString (), throwOnError: true)!;
					}
					while (callbackDeclaringType.ContainsGenericParameters) {
						callbackDeclaringType = callbackDeclaringType.BaseType!;
					}

					GetCallbackHandler connector = (GetCallbackHandler) Delegate.CreateDelegate (typeof (GetCallbackHandler),
						callbackDeclaringType, callbackString.ToString ());
					callback = connector ();
				}

				if (callback != null) {
					needToRegisterNatives = true;
					natives [nativesIndex++] = new JniNativeMethodRegistration (name.ToString (), signature.ToString (), callback);
				}
			}

			methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
		}

		if (needToRegisterNatives) {
			JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives, nativesIndex);
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

	[UnconditionalSuppressMessage ("Trimming", "IL2068", Justification = "Temporary suppression until ManagedTypeMapping type entries carry DAM annotations.")]
	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		var type = base.GetTypeForSimpleReference (jniSimpleReference);
		if (type != null) {
			return type;
		}

		return ManagedTypeMapping.TryGetType (jniSimpleReference, out var target) ? target : null;
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

	static int CountMethods (ReadOnlySpan<char> methodsSpan)
	{
		int count = 0;
		while (!methodsSpan.IsEmpty) {
			count++;

			int newLineIndex = methodsSpan.IndexOf ('\n');
			methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
		}
		return count;
	}

	static void SplitMethodLine (
		ReadOnlySpan<char> methodLine,
		out ReadOnlySpan<char> name,
		out ReadOnlySpan<char> signature,
		out ReadOnlySpan<char> callback,
		out ReadOnlySpan<char> callbackDeclaringType)
	{
		int colonIndex = methodLine.IndexOf (':');
		name = methodLine.Slice (0, colonIndex);
		methodLine = methodLine.Slice (colonIndex + 1);

		colonIndex = methodLine.IndexOf (':');
		signature = methodLine.Slice (0, colonIndex);
		methodLine = methodLine.Slice (colonIndex + 1);

		colonIndex = methodLine.IndexOf (':');
		callback = methodLine.Slice (0, colonIndex != -1 ? colonIndex : methodLine.Length);

		callbackDeclaringType = colonIndex != -1 ? methodLine.Slice (colonIndex + 1) : default;
	}

	delegate Delegate GetCallbackHandler ();
}
