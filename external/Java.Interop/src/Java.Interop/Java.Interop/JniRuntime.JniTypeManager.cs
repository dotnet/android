#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace Java.Interop {

	public partial class JniRuntime {

		static unsafe string GetUtf8String (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return "";

			return Encoding.UTF8.GetString (MemoryMarshal.CreateReadOnlySpanFromNullTerminated ((byte*)value));
		}

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
			/// <summary>
			/// Gets or sets a pointer to a NUL-terminated UTF-8 JNI type name.
			/// </summary>
			/// <remarks>
			/// Java.Interop does not own or free this memory. A non-zero pointer must remain valid
			/// and unchanged for the lifetime of the associated <see cref="JniRuntime"/>, because
			/// cached JNI type and method metadata may retain and dereference it.
			/// </remarks>
			public  IntPtr  TargetJniTypeUtf8               {get; set;}
			/// <summary>
			/// Gets or sets a pointer to a NUL-terminated UTF-8 JNI method name.
			/// </summary>
			/// <remarks>
			/// Java.Interop does not own or free this memory. A non-zero pointer must remain valid
			/// and unchanged for the lifetime of the associated <see cref="JniRuntime"/>, because
			/// cached JNI type and method metadata may retain and dereference it.
			/// </remarks>
			public  IntPtr  TargetJniMethodNameUtf8         {get; set;}
			/// <summary>
			/// Gets or sets a pointer to a NUL-terminated UTF-8 JNI method signature.
			/// </summary>
			/// <remarks>
			/// Java.Interop does not own or free this memory. A non-zero pointer must remain valid
			/// and unchanged for the lifetime of the associated <see cref="JniRuntime"/>, because
			/// cached JNI type and method metadata may retain and dereference it.
			/// </remarks>
			public  IntPtr  TargetJniMethodSignatureUtf8    {get; set;}
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
					TargetJniTypeUtf8 == other.TargetJniTypeUtf8 &&
					TargetJniMethodNameUtf8 == other.TargetJniMethodNameUtf8 &&
					TargetJniMethodSignatureUtf8 == other.TargetJniMethodSignatureUtf8 &&
					TargetJniMethodParameterCount == other.TargetJniMethodParameterCount &&
					TargetJniMethodInstanceToStatic == other.TargetJniMethodInstanceToStatic;
			}

			public override int GetHashCode ()
			{
				return HashCode.Combine (
					SourceJniType,
					SourceJniMethodName,
					SourceJniMethodSignature,
					TargetJniType,
					TargetJniMethodName,
					TargetJniMethodSignature,
					HashCode.Combine (
						TargetJniTypeUtf8,
						TargetJniMethodNameUtf8,
						TargetJniMethodSignatureUtf8,
						TargetJniMethodParameterCount,
						TargetJniMethodInstanceToStatic
					)
				);
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
					$", {nameof (TargetJniTypeUtf8)} = \"{GetUtf8String (TargetJniTypeUtf8)}\"" +
					$", {nameof (TargetJniMethodNameUtf8)} = \"{GetUtf8String (TargetJniMethodNameUtf8)}\"" +
					$", {nameof (TargetJniMethodSignatureUtf8)} = \"{GetUtf8String (TargetJniMethodSignatureUtf8)}\"" +
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
			internal const DynamicallyAccessedMemberTypes MethodsConstructors = Methods | Constructors;

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
			protected virtual Type? GetTypeForSimpleReference (string jniSimpleReference) => null;
			public virtual IEnumerable<Type> GetTypes (JniTypeSignature typeSignature) => [];

			protected virtual IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference) => [];

			static JniTypeSignature GetBuiltInTypeSignature (Type type)
			{
				if (type == typeof (JavaProxyObject))
					return new JniTypeSignature (JavaProxyObject.JniTypeName, 0, false);
				if (type == typeof (JavaProxyThrowable))
					return new JniTypeSignature (JavaProxyThrowable.JniTypeName, 0, false);
				return default;
			}

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
			public Type? GetInvokerType (Type type)
			{
				if (type.IsAbstract || type.IsInterface) {
					return GetInvokerTypeCore (type);
				}
				return null;
			}

			protected virtual Type? GetInvokerTypeCore (Type type) => null;

			protected virtual IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimple) => null;

			public string? GetReplacementType (string jniSimpleReference)
			{
				GetReplacementTypeInfo (jniSimpleReference, out var replacement, out var replacementUtf8);
				return replacementUtf8 != IntPtr.Zero ? GetUtf8String (replacementUtf8) : replacement;
			}

			protected virtual string? GetReplacementTypeCore (string jniSimpleReference) => null;

			internal void GetReplacementTypeInfo (string jniSimpleReference, out string? replacement, out IntPtr replacementUtf8)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));

				GetReplacementTypeInfoCore (jniSimpleReference, out replacement, out replacementUtf8);
				if (replacementUtf8 != IntPtr.Zero)
					replacement = null;
			}

			/// <summary>
			/// Resolves a replacement JNI type as either a managed string or stable NUL-terminated UTF-8 memory.
			/// </summary>
			/// <remarks>
			/// The default implementation preserves compatibility with string-based type managers by
			/// calling <see cref="GetReplacementTypeCore(string)"/> once and setting
			/// <paramref name="replacementUtf8"/> to zero. Overrides are authoritative and must return
			/// results equivalent to <see cref="GetReplacementTypeCore(string)"/> for every reference.
			/// A non-zero <paramref name="replacementUtf8"/> takes precedence over
			/// <paramref name="replacement"/>.
			/// Java.Interop does not own or free non-zero UTF-8 memory, which must remain valid and
			/// unchanged for the lifetime of the associated <see cref="JniRuntime"/>.
			/// </remarks>
			protected virtual void GetReplacementTypeInfoCore (string jniSimpleReference, out string? replacement, out IntPtr replacementUtf8)
			{
				replacement = GetReplacementTypeCore (jniSimpleReference);
				replacementUtf8 = IntPtr.Zero;
			}

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

			internal ReplacementMethodInfo? GetReplacementMethodInfo (string jniSimpleReference, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));
				if (jniMethodName.IsEmpty)
					throw new ArgumentNullException (nameof (jniMethodName));
				if (jniMethodSignature.IsEmpty)
					throw new ArgumentNullException (nameof (jniMethodSignature));

				return GetReplacementMethodInfoCore (jniSimpleReference, jniMethodName, jniMethodSignature);
			}

			internal ReplacementMethodInfo? GetReplacementMethodInfo (IntPtr jniSimpleReferenceUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
			{
				AssertValid ();
				if (jniSimpleReferenceUtf8 == IntPtr.Zero)
					throw new ArgumentNullException (nameof (jniSimpleReferenceUtf8));
				if (jniMethodName.IsEmpty)
					throw new ArgumentNullException (nameof (jniMethodName));
				if (jniMethodSignature.IsEmpty)
					throw new ArgumentNullException (nameof (jniMethodSignature));

				return GetReplacementMethodInfoCore (jniSimpleReferenceUtf8, jniMethodName, jniMethodSignature);
			}

			/// <summary>
			/// Resolves member remapping without requiring name and signature strings.
			/// The default implementation preserves dispatch to the string overload.
			/// </summary>
			protected virtual ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSimpleReference, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
				=> GetReplacementMethodInfoCore (jniSimpleReference, jniMethodName.ToString (), jniMethodSignature.ToString ());

			/// <summary>
			/// Resolves member remapping with a source JNI type in stable NUL-terminated UTF-8 memory.
			/// </summary>
			protected virtual ReplacementMethodInfo? GetReplacementMethodInfoCore (IntPtr jniSimpleReferenceUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
				=> GetReplacementMethodInfoCore (GetUtf8String (jniSimpleReferenceUtf8), jniMethodName, jniMethodSignature);

			// Default implementation is a no-op. Derived classes (e.g. `ReflectionJniTypeManager`)
			// provide reflection-based registration. Override to provide custom registration.
			public virtual void RegisterNativeMembers (JniType nativeClass, Type type, ReadOnlySpan<char> methods)
			{
			}

			[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>)")]
			public virtual void RegisterNativeMembers (JniType nativeClass, Type type, string? methods)
			{
			}
		}
	}
}
