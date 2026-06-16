#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Java.Interop {

	public partial class JniRuntime {

		[SuppressMessage ("Design", "CA1034:Nested types should not be visible",
			Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`; see 045b8af7.")]
		public struct ReplacementMethodInfo : IEquatable<ReplacementMethodInfo>
		{
			public  string? SourceJniType                   {get; set;}
			public  string? SourceJniMethodName             {get; set;}
			public  string? SourceJniMethodSignature        {get; set;}
			public  string? TargetJniType                   {get; set;}
			public  string? TargetJniMethodName             {get; set;}
			public  string? TargetJniMethodSignature        {get; set;}
			public  int?    TargetJniMethodParameterCount   {get; set;}
			public  bool    TargetJniMethodInstanceToStatic {get; set;}

			public override bool Equals (object? obj)
			{
				if (obj is ReplacementMethodInfo o) {
					return Equals (o);
				}
				return false;
			}

			public bool Equals (ReplacementMethodInfo other)
			{
				return string.Equals (SourceJniType, other.SourceJniType) &&
					string.Equals (SourceJniMethodName, other.SourceJniMethodName) &&
					string.Equals (SourceJniMethodSignature, other.SourceJniMethodSignature) &&
					string.Equals (TargetJniType, other.TargetJniType) &&
					string.Equals (TargetJniMethodName, other.TargetJniMethodName) &&
					string.Equals (TargetJniMethodSignature, other.TargetJniMethodSignature) &&
					TargetJniMethodParameterCount == other.TargetJniMethodParameterCount &&
					TargetJniMethodInstanceToStatic == other.TargetJniMethodInstanceToStatic;
			}

			public override int GetHashCode ()
			{
				return (SourceJniType?.GetHashCode () ?? 0) ^
					(SourceJniMethodName?.GetHashCode () ?? 0) ^
					(SourceJniMethodSignature?.GetHashCode () ?? 0) ^
					(TargetJniType?.GetHashCode () ?? 0) ^
					(TargetJniMethodName?.GetHashCode () ?? 0) ^
					(TargetJniMethodSignature?.GetHashCode () ?? 0) ^
					(TargetJniMethodParameterCount?.GetHashCode () ?? 0) ^
					TargetJniMethodInstanceToStatic.GetHashCode ();
			}

			public override string ToString ()
			{
				return $"{nameof (ReplacementMethodInfo)} {{ " +
					$"{nameof (SourceJniType)} = \"{SourceJniType}\"" +
					$", {nameof (SourceJniMethodName)} = \"{SourceJniMethodName}\"" +
					$", {nameof (SourceJniMethodSignature)} = \"{SourceJniMethodSignature}\"" +
					$", {nameof (TargetJniType)} = \"{TargetJniType}\"" +
					$", {nameof (TargetJniMethodName)} = \"{TargetJniMethodName}\"" +
					$", {nameof (TargetJniMethodSignature)} = \"{TargetJniMethodSignature}\"" +
					$", {nameof (TargetJniMethodParameterCount)} = {TargetJniMethodParameterCount?.ToString () ?? "null"}" +
					$", {nameof (TargetJniMethodInstanceToStatic)} = {TargetJniMethodInstanceToStatic}" +
					$"}}";
			}

			public static bool operator==(ReplacementMethodInfo a, ReplacementMethodInfo b) => a.Equals (b);
			public static bool operator!=(ReplacementMethodInfo a, ReplacementMethodInfo b) => !a.Equals (b);
		}

		/// <include file="../Documentation/Java.Interop/JniRuntime.JniTypeManager.xml" path="/docs/member[@name='T:JniTypeManager']/*" />
		public partial class JniTypeManager : IDisposable, ISetRuntime {

			internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
			internal const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
			internal const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;
			internal const DynamicallyAccessedMemberTypes MethodsConstructors = MethodsAndPrivateNested | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

			JniRuntime?             runtime;
			bool                    disposed;


			public      JniRuntime  Runtime {
				get => runtime ?? throw new NotSupportedException ();
			}

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				AssertValid ();
				this.runtime = runtime;
			}

			public void Dispose ()
			{
				Dispose (false);
			}

			protected virtual void Dispose (bool disposing)
			{
				disposed    = true;
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			private protected void AssertValid ()
			{
				if (!disposed)
					return;
				throw new ObjectDisposedException (nameof (JniTypeManager));
			}

			internal static void AssertSimpleReference (string jniSimpleReference, string argumentName = "jniSimpleReference")
			{
				if (string.IsNullOrEmpty (jniSimpleReference))
					throw new ArgumentNullException (argumentName);
				if (jniSimpleReference.IndexOf ('.') >= 0)
					throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", argumentName);
				switch (jniSimpleReference [0]) {
					case '[':
						throw new ArgumentException ("Arrays cannot be present in simplified type references.", argumentName);
					case 'L':
						if (jniSimpleReference [jniSimpleReference.Length - 1] == ';')
							throw new ArgumentException ("JNI type references are not supported.", argumentName);
						break;
					default:
						break;
				}
			}

			// NOTE: This method needs to be kept in sync with GetTypeSignatures()
			// This version of the method has removed IEnumerable for performance reasons.
			public JniTypeSignature GetTypeSignature (Type type)
			{
				AssertValid ();

				if (type == null)
 					throw new ArgumentNullException (nameof (type));

				var builtIn = GetBuiltInTypeSignature (type);
				return builtIn.IsValid ? builtIn : GetTypeSignatureCore (type);
			}

			protected virtual JniTypeSignature GetTypeSignatureCore (Type type) => default;

			// NOTE: This method needs to be kept in sync with GetTypeSignature()
			public IEnumerable<JniTypeSignature> GetTypeSignatures (Type type)
			{
				AssertValid ();

				if (type == null)
					return [];

				var builtIn = GetBuiltInTypeSignature (type);
				if (builtIn.IsValid)
					return new [] { builtIn };

				return GetTypeSignaturesCore (type);
			}

			protected virtual IEnumerable<JniTypeSignature> GetTypeSignaturesCore (Type type) => [];

			[return: DynamicallyAccessedMembers (MethodsConstructors)]
			public  Type?    GetType (JniTypeSignature typeSignature)
			{
				AssertValid ();

				if (!typeSignature.IsValid || typeSignature.SimpleReference == null)
					return null;

				var builtIn = GetBuiltInType (typeSignature);
				if (builtIn != null)
					return builtIn;

				var type = GetTypeForSimpleReference (typeSignature.SimpleReference);
				if (type == null)
					return null;
				if (typeSignature.ArrayRank == 0)
					return type;
				throw new NotSupportedException ($"DAM-annotated type lookup for array signature `{typeSignature}` is not supported. Use {nameof (GetTypes)} instead.");
			}

			protected virtual string? GetSimpleReference (Type type) => null;
			protected virtual IEnumerable<string> GetSimpleReferences (Type type) => [];
			[return: DynamicallyAccessedMembers (MethodsConstructors)]
			protected virtual Type? GetTypeForSimpleReference (string jniSimpleReference) => null;
			public virtual IEnumerable<Type> GetTypes (JniTypeSignature typeSignature) => [];

			public virtual IEnumerable<ReflectionConstructibleType> GetReflectionConstructibleTypes (JniTypeSignature typeSignature) => [];

			public class ReflectionConstructibleType
			{
				public ReflectionConstructibleType (
						[DynamicallyAccessedMembers (Constructors)]
						Type type)
				{
					Type = type;
				}

				[DynamicallyAccessedMembers (Constructors)]
				public Type Type { get; }
			}

			protected virtual IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference) => [];

			static JniTypeSignature GetBuiltInTypeSignature (Type type)
			{
				if (type == typeof (JavaProxyObject))
					return new JniTypeSignature (JavaProxyObject.JniTypeName, 0, false);
				if (type == typeof (JavaProxyThrowable))
					return new JniTypeSignature (JavaProxyThrowable.JniTypeName, 0, false);
				return default;
			}

			// IL2026/IL2111: The MethodsConstructors DAM annotation on the return type causes ILLink to analyze
			// JavaProxyObject/JavaProxyThrowable/ManagedPeer's delegate-typed nested members, whose base
			// constructors (Delegate.Delegate(Object,String)) are marked RequiresUnreferencedCode.
			// These warnings are false positives: the delegate constructors are invoked with
			// compile-time-known static method references, not via string-based reflection.
			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Delegate constructors in JavaProxyObject/JavaProxyThrowable/ManagedPeer are invoked with compile-time-known method references, not via reflection.")]
			[UnconditionalSuppressMessage ("Trimming", "IL2111", Justification = "Delegate constructors in JavaProxyObject/JavaProxyThrowable/ManagedPeer are invoked with compile-time-known method references, not via reflection.")]
			[return: DynamicallyAccessedMembers (MethodsConstructors)]
			static Type? GetBuiltInType (JniTypeSignature typeSignature)
			{
				if (typeSignature.ArrayRank != 0)
					return null;
				if (!typeSignature.IsKeyword) {
					return typeSignature.SimpleReference switch {
						JavaProxyObject.JniTypeName     => typeof (JavaProxyObject),
						JavaProxyThrowable.JniTypeName  => typeof (JavaProxyThrowable),
						ManagedPeer.JniTypeName         => typeof (ManagedPeer),
						_                               => null,
					};
				}
				return typeSignature.SimpleReference switch {
					"V" => typeof (void),
					"Z" => typeof (bool),
					"B" => typeof (sbyte),
					"C" => typeof (char),
					"S" => typeof (short),
					"I" => typeof (int),
					"J" => typeof (long),
					"F" => typeof (float),
					"D" => typeof (double),
					_   => null,
				};
			}

			/// <include file="../Documentation/Java.Interop/JniRuntime.JniTypeManager.xml" path="/docs/member[@name='M:GetInvokerType']/*" />
			[return: DynamicallyAccessedMembers (Constructors)]
			public Type? GetInvokerType (
					[DynamicallyAccessedMembers (Constructors)]
					Type type)
			{
				if (type.IsAbstract || type.IsInterface) {
					return GetInvokerTypeCore (type);
				}
				return null;
			}
			
			[return: DynamicallyAccessedMembers (Constructors)]
			protected virtual Type? GetInvokerTypeCore ([DynamicallyAccessedMembers (Constructors)] Type type) => null;

			protected virtual IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimple) => null;

			public string? GetReplacementType (string jniSimpleReference)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));

				return GetReplacementTypeCore (jniSimpleReference);
			}

			protected virtual string? GetReplacementTypeCore (string jniSimpleReference) => null;

			public IReadOnlyList<string>? GetStaticMethodFallbackTypes (string jniSimpleReference)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));

				return GetStaticMethodFallbackTypesCore (jniSimpleReference);
			}

			public ReplacementMethodInfo? GetReplacementMethodInfo (string jniSimpleReference, string jniMethodName, string jniMethodSignature)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));
				if (string.IsNullOrEmpty (jniMethodName)) {
					throw new ArgumentNullException (nameof (jniMethodName));
				}
				if (string.IsNullOrEmpty (jniMethodSignature)) {
					throw new ArgumentNullException (nameof (jniMethodSignature));
				}

				return GetReplacementMethodInfoCore (jniSimpleReference, jniMethodName, jniMethodSignature);
			}

			protected virtual ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSimpleReference, string jniMethodName, string jniMethodSignature) => null;

			// Default implementation is a no-op. Derived classes (e.g. `ReflectionJniTypeManager`)
			// provide reflection-based registration. Override to provide custom registration.
			public virtual void RegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
					Type type,
					ReadOnlySpan<char> methods)
			{
			}

			[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
			public virtual void RegisterNativeMembers (
					JniType nativeClass,
					[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
					Type type,
					string? methods)
			{
			}
		}
	}
}
