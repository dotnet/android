using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Java.Interop.Samples.NativeAotFromAndroid;

partial class NativeAotTypeManager : JniRuntime.JniTypeManager {

	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;
	internal const DynamicallyAccessedMemberTypes MethodsConstructors = MethodsAndPrivateNested | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	Dictionary<string, Type> typeMappings = new () {
		["android/app/Activity"]                = typeof (Android.App.Activity),
		["android/content/Context"]             = typeof (Android.Content.Context),
		["android/content/ContextWrapper"]      = typeof (Android.Content.ContextWrapper),
		["android/os/BaseBundle"]               = typeof (Android.OS.BaseBundle),
		["android/os/Bundle"]                   = typeof (Android.OS.Bundle),
		["android/view/ContextThemeWrapper"]    = typeof (Android.View.ContextThemeWrapper),
		["my/MainActivity"]                     = typeof (MainActivity),
	};

	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			ReadOnlySpan<char> methods)
	{
		if (TryRegisterBuiltInNativeMembers (nativeClass, nativeClass.Name, methods))
			return;
		if (!methods.IsEmpty)
			throw new NotSupportedException ($"Could not register native members for type '{type.FullName}'.");
	}

	[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			string? methods)
	{
		RegisterNativeMembers (nativeClass, type, methods.AsSpan ());
	}

	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		var target = GetTypeForSimpleReference (jniSimpleReference);
		if (target != null)
			yield return target;
	}

	protected override string? GetSimpleReference (Type type)
	{
		return GetSimpleReferences (type).FirstOrDefault ();
	}

	[return: DynamicallyAccessedMembers (MethodsConstructors)]
	protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
	{
		return jniSimpleReference switch {
			"V"                                  => typeof (void),
			"Z"                                  => typeof (bool),
			"java/lang/Boolean"                  => typeof (bool?),
			"B"                                  => typeof (sbyte),
			"java/lang/Byte"                     => typeof (sbyte?),
			"C"                                  => typeof (char),
			"java/lang/Character"                => typeof (char?),
			"S"                                  => typeof (short),
			"java/lang/Short"                    => typeof (short?),
			"I"                                  => typeof (int),
			"java/lang/Integer"                  => typeof (int?),
			"J"                                  => typeof (long),
			"java/lang/Long"                     => typeof (long?),
			"F"                                  => typeof (float),
			"java/lang/Float"                    => typeof (float?),
			"D"                                  => typeof (double),
			"java/lang/Double"                   => typeof (double?),
			"android/app/Activity"                => typeof (Android.App.Activity),
			"android/content/Context"             => typeof (Android.Content.Context),
			"android/content/ContextWrapper"      => typeof (Android.Content.ContextWrapper),
			"android/os/BaseBundle"               => typeof (Android.OS.BaseBundle),
			"android/os/Bundle"                   => typeof (Android.OS.Bundle),
			"android/view/ContextThemeWrapper"    => typeof (Android.View.ContextThemeWrapper),
			"my/MainActivity"                     => typeof (MainActivity),
			_                                     => null,
		};
	}

	protected override IEnumerable<string> GetSimpleReferences (Type type)
	{
		return CreateSimpleReferencesEnumerator (type);
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

	public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
	{
		if (!typeSignature.IsValid || typeSignature.ArrayRank != 0 || typeSignature.SimpleReference == null)
			return [];
		return GetTypesForSimpleReference (typeSignature.SimpleReference);
	}

	public override IEnumerable<JniRuntime.JniTypeManager.ReflectionConstructibleType> GetReflectionConstructibleTypes (JniTypeSignature typeSignature)
	{
		if (!typeSignature.IsValid || typeSignature.ArrayRank != 0 || typeSignature.SimpleReference == null)
			yield break;
		var target = GetTypeForSimpleReference (typeSignature.SimpleReference);
		if (target != null)
			yield return new JniRuntime.JniTypeManager.ReflectionConstructibleType (target);
	}

	protected override JniTypeSignature GetTypeSignatureCore (Type type)
	{
		var simpleReference = GetSimpleReferences (type).FirstOrDefault ();
		return simpleReference == null ? default : new JniTypeSignature (simpleReference, 0, false);
	}

	protected override IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type)
	{
		var signature = GetTypeSignatureCore (type);
		if (signature.IsValid)
			yield return signature;
	}

	[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
	protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type type)
	{
		return null;
	}

	protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
	{
		return null;
	}

	protected override string? GetReplacementTypeCore (string jniSimpleReference)
	{
		return null;
	}

	protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
	{
		return null;
	}
}
