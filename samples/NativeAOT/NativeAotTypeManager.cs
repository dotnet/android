using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace NativeAOT;

partial class NativeAotTypeManager : JniRuntime.JniTypeManager {

	internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
	internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

	// TODO: list of types specific to this application
	Dictionary<string, Type> typeMappings = new () {
		["android/app/Activity"]                = typeof (Android.App.Activity),
		["android/content/Context"]             = typeof (Android.Content.Context),
		["android/content/ContextWrapper"]      = typeof (Android.Content.ContextWrapper),
		["android/os/BaseBundle"]               = typeof (Android.OS.BaseBundle),
		["android/os/Bundle"]                   = typeof (Android.OS.Bundle),
		["android/view/ContextThemeWrapper"]    = typeof (Android.Views.ContextThemeWrapper),
		["my/MainActivity"]                     = typeof (MainActivity),
	};

	public NativeAotTypeManager ()
	{
		AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp: NativeAotTypeManager()");
	}

	public override void RegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			ReadOnlySpan<char> methods)
	{
		AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp: RegisterNativeMembers: nativeClass={nativeClass} type=`{type}`");
		
		if (methods.IsEmpty) {
			AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", "methods.IsEmpty");
			return;
		}

		int methodCount = CountMethods (methods);
		if (methodCount < 1) {
			AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"methodCount < 1: {methodCount}");
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

					AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp: Delegate.CreateDelegate callbackDeclaringType={callbackDeclaringType}, callbackString={callbackString}");
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

		AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp: needToRegisterNatives={needToRegisterNatives}");

		if (needToRegisterNatives) {
			AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp: RegisterNatives: nativeClass={nativeClass} type=`{type}` natives={natives.Length} nativesIndex={nativesIndex}");
			JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives, nativesIndex);
		}
	}


	protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
	{
		AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp: GetTypesForSimpleReference: jniSimpleReference=`{jniSimpleReference}`");
		if (typeMappings.TryGetValue (jniSimpleReference, out var target)) {
			Console.WriteLine ($"# jonp:   GetTypesForSimpleReference: jniSimpleReference=`{jniSimpleReference}` -> `{target}`");
			yield return target;
		}
		foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference)) {
			AndroidLog.Print (AndroidLogLevel.Info, "NativeAotTypeManager", $"# jonp:   GetTypesForSimpleReference: jniSimpleReference=`{jniSimpleReference}` -> `{t}`");
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
