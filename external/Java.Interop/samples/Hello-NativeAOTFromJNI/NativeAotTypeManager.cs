using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Hello_NativeAOTFromJNI;

// This sample derives from the reflection-based JniRuntime.ReflectionJniTypeManager, which is
// annotated [RequiresDynamicCode]/[RequiresUnreferencedCode], so the constructor below suppresses
// the resulting IL2026/IL3050 trim/AOT warnings.
//
// Suppressing here is intentional and good enough: these NativeAOT projects are *samples*, not
// product code. .NET for Android (what we actually ship) does not pair ReflectionJniTypeManager
// with NativeAOT, so it isn't worth the effort to make these samples fully trim/AOT-clean right now.
// The reflection paths were always trim/AOT-unsafe: before dotnet/java-interop#1441 the equivalent
// suppressions lived (buried) inside JniTypeManager itself, justified "NotUsedInAndroid"; #1441 just
// moved that responsibility to callers via [RequiresDynamicCode]/[RequiresUnreferencedCode].
class NativeAotTypeManager : JniRuntime.ReflectionJniTypeManager {

	const DynamicallyAccessedMemberTypes MethodsConstructors =
		DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods |
		DynamicallyAccessedMemberTypes.NonPublicNestedTypes |
		DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Sample only (see class comment): this assembly is rooted via TrimmerRootAssembly and the members reflected over during registration are preserved by the [DynamicallyAccessedMembers] annotations on the RegisterNativeMembers(Type) -> FindAndCallRegisterMethod path, so trimming does not remove what reflection needs.")]
	[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Sample only (see class comment): built-in member registration calls CreateDelegate on compile-time-known static methods (no MakeGenericType / expression compilation), so no runtime code generation is required.")]
	public NativeAotTypeManager ()
	{
	}

	// The base ReflectionJniTypeManager resolves built-in types (primitives, java/lang/String,
	// JavaProxyObject, ...) and handles registration and the reverse Type->JNI mapping (via the
	// [JniTypeSignature] attribute) for us. We only need to teach it about this sample's own
	// managed types.
	[return: DynamicallyAccessedMembers (MethodsConstructors)]
	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		if (jniSimpleReference == Example.ManagedType.JniTypeName)
			return typeof (Example.ManagedType);
		return base.GetTypeForSimpleReference (jniSimpleReference);
	}

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		if (jniSimpleReference == Example.ManagedType.JniTypeName)
			yield return typeof (Example.ManagedType);
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference))
			yield return t;
	}
}
