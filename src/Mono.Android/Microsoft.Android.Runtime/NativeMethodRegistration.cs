#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Shared implementation of the "string-based" JNI native method registration. Parses the
/// <c>methods</c> metadata string that Java passes back to managed code (a Java Callable Wrapper
/// static initializer calling <c>mono.android.Runtime.register(...)</c>, or
/// <c>Java.Interop.ManagedPeer</c>) and registers the native callbacks via
/// <see cref="JniEnvironment.Types.RegisterNatives"/>.
///
/// This relies on <see cref="Type.GetType(string, bool)"/> and
/// <see cref="Delegate.CreateDelegate(Type, Type, string)"/>, so it is not trim- or
/// NativeAOT-friendly. It works on MonoVM and CoreCLR and is shared by
/// <c>AndroidTypeManager</c> (the default llvm-ir/MonoVM path) and, gated behind
/// <see cref="RuntimeFeature.StringBasedJniRegistration"/>, by <c>TrimmableTypeMapTypeManager</c>.
/// </summary>
[RequiresUnreferencedCode ("Parses the 'methods' metadata string and resolves JNI callbacks via reflection (Type.GetType / Delegate.CreateDelegate).")]
[RequiresDynamicCode ("Resolves JNI callbacks via reflection and dynamic delegate creation, which is not compatible with NativeAOT.")]
static class NativeMethodRegistration
{
	const DynamicallyAccessedMemberTypes MethodsAndPrivateNested =
		DynamicallyAccessedMemberTypes.PublicMethods |
		DynamicallyAccessedMemberTypes.NonPublicMethods |
		DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

	static MethodInfo? dynamic_callback_gen;

	// [Export] callback delegates are created dynamically via DynamicCallbackCodeGenerator and are not
	// cached in static fields (unlike non-[Export] connector delegates). Without rooting them here,
	// CoreCLR's GC can collect them between JNI registration and first invocation, causing a crash.
	static readonly Lock prevent_delegate_gc_lock = new Lock ();
	static readonly List<Delegate> prevent_delegate_gc = new List<Delegate> ();

	delegate Delegate GetCallbackHandler ();

	/// <summary>
	/// Parses <paramref name="methods"/> and registers the described native callbacks on
	/// <paramref name="nativeClass"/>.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if <paramref name="methods"/> contained at least one method line (whether
	/// or not any natives were registered); <see langword="false"/> if it was empty, in which case the
	/// caller may fall back to the marshal-methods path.
	/// </returns>
	internal static bool TryRegisterNativeMembers (
			JniType nativeClass,
			[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
			Type type,
			ReadOnlySpan<char> methods)
	{
		if (methods.IsEmpty) {
			return false;
		}

		int methodCount = CountMethods (methods);
		if (methodCount < 1) {
			return false;
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
					lock (prevent_delegate_gc_lock) {
						prevent_delegate_gc.Add (callback);
					}
					needToRegisterNatives = true;
				} else {
					Type callbackDeclaringType = type;
					if (!callbackDeclaringTypeString.IsEmpty) {
						var resolvedType = Type.GetType (callbackDeclaringTypeString.ToString (), throwOnError: true);
						if (resolvedType is null) {
							throw new InvalidOperationException ($"Could not resolve JNI callback declaring type '{callbackDeclaringTypeString.ToString ()}'.");
						}
						callbackDeclaringType = resolvedType;
					}
					while (callbackDeclaringType.ContainsGenericParameters) {
						var baseType = callbackDeclaringType.BaseType;
						if (baseType is null) {
							throw new InvalidOperationException ($"Could not resolve a closed JNI callback declaring type for '{callbackDeclaringType}'.");
						}
						callbackDeclaringType = baseType;
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

		return true;
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
		var callback = dynamic_callback_gen.Invoke (null, [ method ]);
		if (callback is not Delegate result) {
			throw new InvalidOperationException ("The referenced Mono.Android.Export.dll returned an invalid dynamic callback.");
		}
		return result;
	}
}
