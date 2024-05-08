using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Java.Interop.Samples.NativeAotFromAndroid;

partial class NativeAotTypeManager : JniRuntime.JniTypeManager {

	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

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
		Console.WriteLine ($"# jonp: RegisterNativeMembers: nativeClass={nativeClass} type=`{type}`");
		base.RegisterNativeMembers (nativeClass, type, methods);
	}


	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		Console.WriteLine ($"# jonp: GetTypesForSimpleReference: jniSimpleReference=`{jniSimpleReference}`");
		if (typeMappings.TryGetValue (jniSimpleReference, out var target)) {
			Console.WriteLine ($"# jonp:   GetTypesForSimpleReference: jniSimpleReference=`{jniSimpleReference}` -> `{target}`");
			yield return target;
		}
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference)) {
			Console.WriteLine ($"# jonp:   GetTypesForSimpleReference: jniSimpleReference=`{jniSimpleReference}` -> `{t}`");
			yield return t;
		}
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
}
