using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoDroid.Generation;
using Xamarin.Android.Binder;

namespace generator.SourceWriters
{
	/// <summary>
	/// Describes how a single binding callback is emitted when
	/// <see cref="CodeGenerationOptions.UseUnmanagedCallersOnlyCallbacks" /> is enabled.
	/// </summary>
	public enum UnmanagedCallbackKind
	{
		/// <summary>
		/// The legacy shape: a <c>cb_*</c> delegate cache field, a <c>Get*Handler ()</c> connector
		/// method, and a managed-callable <c>n_*</c> callback.
		/// </summary>
		Legacy,

		/// <summary>
		/// An <c>[UnmanagedCallersOnly]</c> <c>n_*</c> callback which forwards the raw JNI
		/// arguments to <c>Java.Interop.JniMarshal.SafeInvokeAction/SafeInvokeFunc</c>.  Used for
		/// shapes whose marshaling cannot be centralized — strings, arrays and other copy-back
		/// parameters, <c>CharSequence</c> formatting, collections, and so on.
		/// </summary>
		UnmanagedRaw,

		/// <summary>
		/// An <c>[UnmanagedCallersOnly]</c> <c>n_*</c> callback which forwards the raw JNI
		/// arguments to a <c>Java.Interop.JniMarshalTyped.SafeInvokeMarshaled_*</c> helper along
		/// with a function pointer to a method containing only the managed member invocation.
		/// </summary>
		UnmanagedTyped,
	}

	/// <summary>
	/// Classifies binding callbacks for the experimental direct-<c>[UnmanagedCallersOnly]</c>
	/// callback shape, and computes the shared typed-marshaling helper to invoke.
	/// </summary>
	public static class UnmanagedCallbackSupport
	{
		/// <summary>
		/// Version of the callback format emitted when the experimental shape is enabled.  Kept in
		/// sync with <c>Java.Interop.JavaPeerCallbackFormatAttribute.UnmanagedCallersOnlyCallbacks</c>.
		/// </summary>
		public const int UnmanagedCallersOnlyFormatVersion = 2;

		/// <summary>
		/// Version of the callback format emitted by default.  Kept in sync with
		/// <c>Java.Interop.JavaPeerCallbackFormatAttribute.ConnectorDelegates</c>.
		/// </summary>
		public const int ConnectorDelegatesFormatVersion = 1;

		public const string TypedMarshalerType = "global::Java.Interop.JniMarshalTyped";

		// Native (JNI ABI) types which may appear in an [UnmanagedCallersOnly] signature.
		// `bool` and `char` are deliberately absent: they are not blittable, so a callback which
		// declares them must keep the legacy delegate-based shape.
		static readonly HashSet<string> BlittableNativeTypes = new HashSet<string> (StringComparer.Ordinal) {
			"sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong",
			"float", "double", "IntPtr", "UIntPtr", "nint", "nuint",
			"System.IntPtr", "System.UIntPtr",
		};

		// Native types which the typed helpers accept as an unconstrained scalar argument.  This is
		// deliberately narrower than BlittableNativeTypes: an IntPtr scalar would be a disguised
		// object reference, which the helper must not forward without marshaling it.
		static readonly HashSet<string> ScalarNativeTypes = new HashSet<string> (StringComparer.Ordinal) {
			"sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double",
		};

		/// <summary>
		/// Determines how <paramref name="method" /> declared on <paramref name="type" /> should be
		/// emitted.
		/// </summary>
		public static UnmanagedCallbackKind GetCallbackKind (GenBase type, Method method, CodeGenerationOptions opt, string propertyName, bool isFormatted)
		{
			if (!CanUseUnmanagedCallersOnly (type, method, opt))
				return UnmanagedCallbackKind.Legacy;

			return TryGetTypedShape (type, method, opt, propertyName, isFormatted, out _)
				? UnmanagedCallbackKind.UnmanagedTyped
				: UnmanagedCallbackKind.UnmanagedRaw;
		}

		/// <summary>
		/// Returns whether the <c>n_*</c> callback for <paramref name="method" /> can carry
		/// <c>[UnmanagedCallersOnly]</c> at all.
		/// </summary>
		public static bool CanUseUnmanagedCallersOnly (GenBase type, Method method, CodeGenerationOptions opt)
		{
			if (opt == null || !opt.UseUnmanagedCallersOnlyCallbacks)
				return false;

			// The JavaInterop1 callback shape marshals through JniValueManager and has not been
			// evaluated for this experiment.
			if (opt.CodeGenerationTarget != CodeGenerationTarget.XAJavaInterop1)
				return false;

			// [UnmanagedCallersOnly] cannot be applied to a method of a generic type, because the
			// generated entry point would need a type argument that JNI cannot supply.  Generic
			// declaring types therefore keep the legacy shape.
			if (type == null || type.IsGeneric || type.FullName.IndexOf ('<') >= 0)
				return false;

			// Neither `method.IsGeneric` nor `method.Parameters.HasGeneric` is consulted here.  Both
			// report *Java* generics, which are erased to plain object references at the JNI
			// boundary, so the emitted `n_*` entry point is non-generic either way and can carry
			// [UnmanagedCallersOnly].  Such methods do fall out of the typed shape below, because
			// the shared helper cannot know the erased type; they use the raw fallback instead.
			if (method.IsStatic)
				return false;

			// Every value crossing the boundary must be blittable.
			if (!method.IsVoid && !IsBlittableNativeType (method.RetVal.NativeType))
				return false;

			foreach (var p in method.Parameters) {
				if (!IsBlittableNativeType (p.NativeType))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Attempts to compute the shared typed-marshaling helper which can own the JNI transition,
		/// exception reporting, peer lookup and return conversion for <paramref name="method" />.
		/// </summary>
		public static bool TryGetTypedShape (GenBase type, Method method, CodeGenerationOptions opt, string propertyName, bool isFormatted, out TypedCallbackShape shape)
		{
			shape = null;

			if (isFormatted || method.Parameters.HasCharSequence)
				return false;

			// The typed helpers pass `self` as a typed peer; a sender parameter or a parameter
			// requiring post-call cleanup would need code the helper cannot own.
			foreach (var p in method.Parameters) {
				if (p.IsSender)
					return false;
				if (p.GetPostCallback (opt).Length != 0)
					return false;
			}

			var argKinds = new StringBuilder (method.Parameters.Count);
			var typeArguments = new List<string> { opt.GetOutputName (type.FullName) };
			var targetParameters = new List<string> ();
			var forwardedArguments = new List<string> ();

			foreach (var p in method.Parameters) {
				if (TryGetPeerType (p.Symbol, opt, out var peerType)) {
					argKinds.Append ('O');
					typeArguments.Add (peerType);
					targetParameters.Add ($"{peerType}{opt.NullableOperator} {opt.GetSafeIdentifier (p.Name)}");
					forwardedArguments.Add (opt.GetSafeIdentifier (p.UnsafeNativeName));
					continue;
				}

				if (!p.NeedsPrep && ScalarNativeTypes.Contains (p.NativeType)) {
					argKinds.Append ('S');
					typeArguments.Add (p.NativeType);
					targetParameters.Add ($"{p.NativeType} {opt.GetSafeIdentifier (p.UnsafeNativeName)}");
					forwardedArguments.Add (opt.GetSafeIdentifier (p.UnsafeNativeName));
					continue;
				}

				return false;
			}

			char returnKind;
			string targetReturnType;
			if (method.IsVoid) {
				returnKind = 'X';
				targetReturnType = "void";
			} else if (TryGetPeerType (method.RetVal.Symbol, opt, out _)) {
				// Peer returns are not generic in the helper: bound methods frequently have
				// covariant returns, so the target must be typed as IJavaObject. This also
				// removes one type argument from every MethodSpec of this shape.
				returnKind = 'O';
				targetReturnType = "global::Android.Runtime.IJavaObject" + opt.NullableOperator;
			} else if (ScalarNativeTypes.Contains (method.RetVal.NativeType)) {
				returnKind = 'S';
				targetReturnType = method.RetVal.NativeType;
				typeArguments.Add (method.RetVal.NativeType);
			} else {
				return false;
			}

			var pattern = argKinds.ToString ();
			if (!IsSupportedPattern (pattern))
				return false;

			shape = new TypedCallbackShape (
				helperName: $"SafeInvokeMarshaled_{pattern}{returnKind}",
				typeArguments: typeArguments,
				targetParameters: targetParameters,
				forwardedArguments: forwardedArguments,
				targetReturnType: targetReturnType,
				returnsPeer: returnKind == 'O');
			return true;
		}

		/// <summary>
		/// The set of argument patterns for which <c>JniMarshalTyped</c> provides helpers: every
		/// combination for arities 0-3, plus all-scalar shapes up to arity 8.
		/// </summary>
		static bool IsSupportedPattern (string pattern)
		{
			if (pattern.Length <= 3)
				return true;
			if (pattern.Length > 8)
				return false;
			return pattern.All (c => c == 'S');
		}

		static bool IsBlittableNativeType (string nativeType) =>
			nativeType != null && BlittableNativeTypes.Contains (nativeType);

		/// <summary>
		/// Returns the managed peer type for a symbol whose callback marshaling is exactly
		/// <c>Java.Lang.Object.GetObject&lt;T&gt; (handle, JniHandleOwnership.DoNotTransfer)</c>.
		/// </summary>
		/// <remarks>
		/// Symbols which project onto a different managed type than the one they are marshaled as —
		/// notably the collection interfaces, which marshal as <c>JavaList</c>/<c>JavaDictionary</c>
		/// and are then cast — are rejected, because the helper cannot perform that projection.
		/// </remarks>
		static bool TryGetPeerType (ISymbol symbol, CodeGenerationOptions opt, out string peerType)
		{
			peerType = null;

			if (!(symbol is ClassGen || symbol is InterfaceGen))
				return false;

			var gen = (GenBase) symbol;
			if (gen.IsGeneric || gen.FullName.IndexOf ('<') >= 0)
				return false;

			if (symbol is IRequireGenericMarshal rgm && rgm.GetGenericJavaObjectTypeOverride () != null)
				return false;

			peerType = opt.GetOutputName (gen.FullName);
			return true;
		}
	}

	/// <summary>
	/// The shared typed-marshaling helper invocation, and the signature of the method-specific
	/// function pointer target it calls.
	/// </summary>
	public sealed class TypedCallbackShape
	{
		public TypedCallbackShape (string helperName, IList<string> typeArguments, IList<string> targetParameters, IList<string> forwardedArguments, string targetReturnType, bool returnsPeer)
		{
			HelperName = helperName;
			TypeArguments = typeArguments;
			TargetParameters = targetParameters;
			ForwardedArguments = forwardedArguments;
			TargetReturnType = targetReturnType;
			ReturnsPeer = returnsPeer;
		}

		/// <summary>Name of the <c>JniMarshalTyped</c> helper, e.g. <c>SafeInvokeMarshaled_OSX</c>.</summary>
		public string HelperName { get; }

		/// <summary>Type arguments to the helper: the peer type, one per argument, then the return type.</summary>
		public IList<string> TypeArguments { get; }

		/// <summary>Parameters of the method-specific function pointer target, excluding <c>__this</c>.</summary>
		public IList<string> TargetParameters { get; }

		/// <summary>Raw JNI arguments forwarded from the <c>n_*</c> callback to the helper.</summary>
		public IList<string> ForwardedArguments { get; }

		public string TargetReturnType { get; }

		public bool ReturnsPeer { get; }

		public string GetHelperInvocation (string thisType, string targetName, string nullableOperator)
		{
			var typeArgs = string.Join (", ", TypeArguments);
			var args = new StringBuilder ("jnienv, native__this");
			foreach (var a in ForwardedArguments)
				args.Append (", ").Append (a);
			args.Append (", &").Append (targetName);
			return $"{UnmanagedCallbackSupport.TypedMarshalerType}.{HelperName}<{typeArgs}> ({args})";
		}

		public string GetTargetSignature (string thisType, string targetName, string nullableOperator)
		{
			var parameters = new StringBuilder ($"{thisType} __this");
			foreach (var p in TargetParameters)
				parameters.Append (", ").Append (p);
			return $"private static {TargetReturnType} {targetName} ({parameters})";
		}
	}
}
