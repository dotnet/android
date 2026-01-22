using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop.Tools.TypeNameMappings;

namespace Java.Interop
{
	/// <summary>
	/// Handles dynamic registration of native JNI methods for types that use [Export] attributes.
	/// </summary>
	/// <remarks>
	/// This class was extracted from AndroidTypeManager to separate the concerns of type mapping
	/// from native method registration. The registration logic here handles dynamically generated
	/// callbacks for methods marked with [Export] or [JavaCallable] attributes.
	///
	/// Note: The MagicRegistrationMap and FastRegisterNativeMembers functionality that previously
	/// existed here was removed because the MonoDroidMarkStep linker step that populated it was
	/// deleted in PR #8572 (commit c60a621). The linker step was responsible for generating IL
	/// to fill the typesMap dictionary, but since that step no longer exists, the MagicRegistrationMap
	/// was dead code that could never be populated.
	/// </remarks>
	[RequiresUnreferencedCode ("Dynamic native member registration requires unreferenced code for Type.GetType and Delegate.CreateDelegate calls.")]
	static class DynamicNativeMembersRegistration
	{
		const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
		const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

		delegate Delegate GetCallbackHandler ();

		static MethodInfo? dynamic_callback_gen;

		// See ExportAttribute.cs
		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Mono.Android.Export.dll is preserved when [Export] is used via [DynamicDependency].")]
		[UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Mono.Android.Export.dll is preserved when [Export] is used via [DynamicDependency].")]
		static Delegate CreateDynamicCallback (MethodInfo method)
		{
			if (dynamic_callback_gen == null) {
				var assembly = Assembly.Load ("Mono.Android.Export");
				if (assembly == null)
					throw new InvalidOperationException ("To use methods marked with ExportAttribute, Mono.Android.Export.dll needs to be referenced in the application");
				var type = assembly.GetType ("Java.Interop.DynamicCallbackCodeGenerator");
				if (type == null)
					throw new InvalidOperationException ("The referenced Mono.Android.Export.dll does not match the expected version. The required type was not found.");
				dynamic_callback_gen = type.GetMethod ("Create");
				if (dynamic_callback_gen == null)
					throw new InvalidOperationException ("The referenced Mono.Android.Export.dll does not match the expected version. The required method was not found.");
			}
			return (Delegate) dynamic_callback_gen.Invoke (null, new object [] { method })!;
		}

		public static void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)] Type type,
				ReadOnlySpan<char> methods)
		{
			// Disable dynamic registration on CoreCLR as we use TypeMaps
			// EXCEPT for Java.Interop types which don't have static stubs yet (e.g. [Export])
			if (Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime && type.Namespace != "Java.Interop")
				return;

			int methodCount = CountMethods (methods);
			if (methodCount < 1) {
				return;
			}

			JniNativeMethodRegistration [] natives = new JniNativeMethodRegistration [methodCount];
			int nativesIndex = 0;
			MethodInfo []? typeMethods = null;

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
						var mname = name.Slice (2);
						MethodInfo? minfo = null;
						typeMethods ??= type.GetMethods (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
						foreach (var mi in typeMethods)
							if (mname.SequenceEqual (mi.Name) && signature.SequenceEqual (JavaNativeTypeManager.GetJniSignature (mi))) {
								minfo = mi;
								break;
							}

						if (minfo == null)
							throw new InvalidOperationException (FormattableString.Invariant ($"Specified managed method '{mname.ToString ()}' was not found. Signature: {signature.ToString ()}"));
						callback = CreateDynamicCallback (minfo);
						needToRegisterNatives = true;
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
						Android.Runtime.Logger.Log (Android.Runtime.LogLevel.Info, "monodroid", $"DynamicNativeMembersRegistration: registered {name.ToString ()} for {type.FullName}");
					}
				}

				methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
			}

			if (needToRegisterNatives) {
				Android.Runtime.Logger.Log (Android.Runtime.LogLevel.Info, "monodroid", $"DynamicNativeMembersRegistration: calling RegisterNatives with {nativesIndex} methods");
				JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives, nativesIndex);
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
	}
}
