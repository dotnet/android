using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Microsoft.Android.Runtime;

[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Temporary suppression for Java.Interop reflection manager base.")]
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
	public override unsafe void RegisterNativeMembers (
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

		// Marshal directly into blittable JniNativeMethod values and dispatch to the blittable
		// RegisterNatives overload, instead of the JniNativeMethodRegistration[] overload (which
		// is annotated [RequiresDynamicCode] and works around a crossgen2 miscompilation by
		// performing the same marshaling internally). See dotnet/java-interop#1474.
		const int MaxStackAllocatedNativeMethods = 32;
		bool useStackAllocatedBuffers = methodCount <= MaxStackAllocatedNativeMethods;
		Span<JniNativeMethod> natives = useStackAllocatedBuffers
			? stackalloc JniNativeMethod [methodCount]
			: new JniNativeMethod [methodCount];
		Span<IntPtr> unmanagedStrings = useStackAllocatedBuffers
			? stackalloc IntPtr [methodCount * 2]
			: new IntPtr [methodCount * 2];
		unmanagedStrings.Clear ();
		// Keep marshaler delegates alive at least until JNI has consumed the function pointers.
		Delegate? [] callbacks = new Delegate? [methodCount];
		int nativesIndex = 0;

		try {
			ReadOnlySpan<char> methodsSpan = methods;
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
						IntPtr namePtr = Marshal.StringToCoTaskMemUTF8 (name.ToString ());
						unmanagedStrings [nativesIndex * 2] = namePtr;
						IntPtr sigPtr = Marshal.StringToCoTaskMemUTF8 (signature.ToString ());
						unmanagedStrings [nativesIndex * 2 + 1] = sigPtr;
						callbacks [nativesIndex] = callback;
						natives [nativesIndex] = new JniNativeMethod ((byte*) namePtr, (byte*) sigPtr, Marshal.GetFunctionPointerForDelegate (callback));
						nativesIndex++;
					}
				}

				methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
			}

			if (nativesIndex > 0) {
				JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives.Slice (0, nativesIndex));
				GC.KeepAlive (callbacks);
			}
		} finally {
			for (int i = 0; i < unmanagedStrings.Length; ++i) {
				if (unmanagedStrings [i] != IntPtr.Zero)
					Marshal.ZeroFreeCoTaskMemUTF8 (unmanagedStrings [i]);
			}
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
