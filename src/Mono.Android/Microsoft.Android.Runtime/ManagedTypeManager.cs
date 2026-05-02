using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Android.Runtime;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Microsoft.Android.Runtime;

class ManagedTypeManager : JniRuntime.JniTypeManager {

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.AllMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;
	const DynamicallyAccessedMemberTypes MethodsConstructors = MethodsAndPrivateNested | Constructors;

	struct JniRemappingReplacementMethod
	{
		public string target_type;
		public string target_name;
		public bool is_static;
	}

	public ManagedTypeManager ()
	{
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	[RequiresDynamicCode ("Generic invoker type construction may require runtime generic code generation.")]
	[RequiresUnreferencedCode ("Generic invoker type construction may require unreferenced generic parameter annotations.")]
	protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
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

	[return: DynamicallyAccessedMembers (MethodsConstructors)]
	public override Type? GetTypeForNativeRegistration (JniTypeSignature typeSignature)
	{
		var type = base.GetTypeForNativeRegistration (typeSignature);
		if (type != null || !typeSignature.IsValid || typeSignature.SimpleReference == null || typeSignature.ArrayRank != 0) {
			return type;
		}

		if (ManagedTypeMapping.TryGetTypeForNativeRegistration (typeSignature.SimpleReference, out var target)) {
			return target;
		}
		return null;
	}

	// NOTE: suppressions below also in `src/Mono.Android/Android.Runtime/AndroidRuntime.cs`
	[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Type.GetType() can never statically know the string value parsed from parameter 'methods'.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Delegate.CreateDelegate() can never statically know the string value parsed from parameter 'methods'.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Delegate.CreateDelegate() can never statically know the string value parsed from parameter 'methods'.")]
	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.AllMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
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

	[return: DynamicallyAccessedMembers (Constructors)]
	public override Type? GetType (JniTypeSignature typeSignature)
	{
		var type = base.GetType (typeSignature);
		if (type != null || !typeSignature.IsValid || typeSignature.SimpleReference == null || typeSignature.ArrayRank != 0) {
			return type;
		}

		if (ManagedTypeMapping.TryGetType (typeSignature.SimpleReference, out var target)) {
			return GetNonGenericContainerType (target);
		}
		return null;
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	public override Type? GetTypeAssignableTo (
			JniTypeSignature typeSignature,
			[DynamicallyAccessedMembers (Constructors)]
			Type targetType)
	{
		var type = base.GetTypeAssignableTo (typeSignature, targetType);
		if (type != null || !typeSignature.IsValid || typeSignature.SimpleReference == null || typeSignature.ArrayRank != 0) {
			return type;
		}

		if (ManagedTypeMapping.TryGetType (typeSignature.SimpleReference, out var mappedType)) {
			mappedType = GetNonGenericContainerType (mappedType);
		}
		if (mappedType != null && targetType.IsAssignableFrom (mappedType)) {
			return mappedType;
		}
		return null;
	}

	[return: DynamicallyAccessedMembers (Constructors)]
	static Type? GetNonGenericContainerType (
			[DynamicallyAccessedMembers (Constructors)]
			Type? type)
	{
		if (type == typeof (global::Android.Runtime.JavaList<>))
			return typeof (global::Android.Runtime.JavaList);
		if (type == typeof (global::Android.Runtime.JavaCollection<>))
			return typeof (global::Android.Runtime.JavaCollection);
		if (type == typeof (global::Android.Runtime.JavaDictionary<,>))
			return typeof (global::Android.Runtime.JavaDictionary);
		return type;
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
		int slash = jniSimpleReference.LastIndexOf ('/');
		var desugarType = slash > 0
			? $"{jniSimpleReference.Substring (0, slash + 1)}Desugar{jniSimpleReference.Substring (slash + 1)}"
			: $"Desugar{jniSimpleReference}";
		var typeWithPrefix = $"{desugarType}$_CC";
		var typeWithSuffix = $"{jniSimpleReference}$-CC";

		return new[] {
			GetReplacementTypeCore (typeWithPrefix) ?? typeWithPrefix,
			GetReplacementTypeCore (typeWithSuffix) ?? typeWithSuffix,
		};
	}

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
	{
		if (!JNIEnvInit.jniRemappingInUse) {
			return null;
		}

		IntPtr ret = RuntimeNativeMethods._monodroid_lookup_replacement_type (jniSimpleReference);
		if (ret == IntPtr.Zero) {
			return null;
		}

		return Marshal.PtrToStringAnsi (ret);
	}

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
	{
		if (!JNIEnvInit.jniRemappingInUse) {
			return null;
		}

		IntPtr retInfo = RuntimeNativeMethods._monodroid_lookup_replacement_method_info (jniSourceType, jniMethodName, jniMethodSignature);
		if (retInfo == IntPtr.Zero) {
			return null;
		}

		var method = Marshal.PtrToStructure<JniRemappingReplacementMethod> (retInfo);
		var newSignature = jniMethodSignature;

		int? paramCount = null;
		if (method.is_static) {
			paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (jniMethodSignature) + 1;
			newSignature = $"(L{jniSourceType};" + jniMethodSignature.Substring ("(".Length);
		}

		return new JniRuntime.ReplacementMethodInfo {
			SourceJniType = jniSourceType,
			SourceJniMethodName = jniMethodName,
			SourceJniMethodSignature = jniMethodSignature,
			TargetJniType = method.target_type,
			TargetJniMethodName = method.target_name,
			TargetJniMethodSignature = newSignature,
			TargetJniMethodParameterCount = paramCount,
			TargetJniMethodInstanceToStatic = method.is_static,
		};
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
