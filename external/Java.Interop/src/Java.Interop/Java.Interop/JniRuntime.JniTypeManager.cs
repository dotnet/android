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

		/// <safety>
		/// A non-null UTF-8 value must be readable through its NUL terminator and unchanged until
		/// return or exception. Read-only aliases may overlap; no alias may write or free it
		/// during the scan. Null converts to an empty string. No source storage is retained.
		/// </safety>
		static unsafe string GetUtf8String (byte* value)
		{
			if (value == null)
				return "";

			unsafe {
				// SAFETY: the caller supplies immutable readable bytes through the terminator for this synchronous copy.
				return Encoding.UTF8.GetString (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value));
			}
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
			/// Gets or sets the legacy native-int address of a NUL-terminated UTF-8 JNI type name.
			/// </summary>
			/// <safety>
			/// A nonzero address has the same backing-storage and lifetime requirements as
			/// <see cref="TargetJniTypeUtf8Pointer"/>. This compatibility setter does not enforce unsafe.
			/// </safety>
			[Obsolete ("Use TargetJniTypeUtf8Pointer and explicitly audit the borrowed storage lifetime.")]
			public IntPtr TargetJniTypeUtf8 {
				get {
					unsafe {
						// SAFETY: the legacy getter copies an address only; it never reads the pointed-to bytes.
						return (IntPtr) TargetJniTypeUtf8Pointer;
					}
				}
				set {
					unsafe {
						// SAFETY: this released compatibility shim stores an address only; callers still owe the typed setter's lifetime.
						TargetJniTypeUtf8Pointer = (byte*) value;
					}
				}
			}

			/// <summary>
			/// Gets or sets the legacy native-int address of a NUL-terminated UTF-8 JNI method name.
			/// </summary>
			/// <safety>
			/// A nonzero address has the same backing-storage and lifetime requirements as
			/// <see cref="TargetJniMethodNameUtf8Pointer"/>. This compatibility setter does not enforce unsafe.
			/// </safety>
			[Obsolete ("Use TargetJniMethodNameUtf8Pointer and explicitly audit the borrowed storage lifetime.")]
			public IntPtr TargetJniMethodNameUtf8 {
				get {
					unsafe {
						// SAFETY: the legacy getter returns the address without scanning borrowed method-name storage.
						return (IntPtr) TargetJniMethodNameUtf8Pointer;
					}
				}
				set {
					unsafe {
						// SAFETY: the released setter preserves the address; it neither validates nor takes ownership of the buffer.
						TargetJniMethodNameUtf8Pointer = (byte*) value;
					}
				}
			}

			/// <summary>
			/// Gets or sets the legacy native-int address of a NUL-terminated UTF-8 JNI method signature.
			/// </summary>
			/// <safety>
			/// A nonzero address has the same backing-storage and lifetime requirements as
			/// <see cref="TargetJniMethodSignatureUtf8Pointer"/>. This compatibility setter does not enforce unsafe.
			/// </safety>
			[Obsolete ("Use TargetJniMethodSignatureUtf8Pointer and explicitly audit the borrowed storage lifetime.")]
			public IntPtr TargetJniMethodSignatureUtf8 {
				get {
					unsafe {
						// SAFETY: return only the stored signature address; no borrowed bytes are read.
						return (IntPtr) TargetJniMethodSignatureUtf8Pointer;
					}
				}
				set {
					unsafe {
						// SAFETY: the released safe-call shim retains no ownership; its caller must satisfy the typed buffer contract.
						TargetJniMethodSignatureUtf8Pointer = (byte*) value;
					}
				}
			}

			/// <summary>
			/// Gets or sets a pointer to a NUL-terminated UTF-8 JNI type name.
			/// </summary>
			/// <safety>
			/// When setting a non-null pointer, the caller must supply readable NUL-terminated UTF-8
			/// memory and keep it alive and unchanged for the associated runtime's lifetime and
			/// every subsequent cached metadata read, including error diagnostics. The readable
			/// range includes the terminator; no concurrent writer or freeing alias is permitted.
			/// Read-only aliases may overlap. Java.Interop neither roots nor frees this memory,
			/// including if a later lookup fails; failure does not invalidate existing cached borrows.
			/// Reading this property only returns the address, without accessing the storage.
			/// </safety>
			public unsafe byte* TargetJniTypeUtf8Pointer {get; set;}
			/// <summary>
			/// Gets or sets a pointer to a NUL-terminated UTF-8 JNI method name.
			/// </summary>
			/// <safety>
			/// When setting a non-null pointer, the caller must supply readable NUL-terminated UTF-8
			/// memory and keep it alive and unchanged for the associated runtime's lifetime and
			/// every subsequent cached metadata read, including error diagnostics. The readable
			/// range includes the terminator; no concurrent writer or freeing alias is permitted.
			/// Read-only aliases may overlap. Java.Interop neither roots nor frees this memory,
			/// including if a later lookup fails; failure does not invalidate existing cached borrows.
			/// Reading this property only returns the address, without accessing the storage.
			/// </safety>
			public unsafe byte* TargetJniMethodNameUtf8Pointer {get; set;}
			/// <summary>
			/// Gets or sets a pointer to a NUL-terminated UTF-8 JNI method signature.
			/// </summary>
			/// <safety>
			/// When setting a non-null pointer, the caller must supply readable NUL-terminated UTF-8
			/// memory and keep it alive and unchanged for the associated runtime's lifetime and
			/// every subsequent cached metadata read, including error diagnostics. The readable
			/// range includes the terminator; no concurrent writer or freeing alias is permitted.
			/// Read-only aliases may overlap. Java.Interop neither roots nor frees this memory,
			/// including if a later lookup fails; failure does not invalidate existing cached borrows.
			/// Reading this property only returns the address, without accessing the storage.
			/// </safety>
			public unsafe byte* TargetJniMethodSignatureUtf8Pointer {get; set;}
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
				unsafe {
					// SAFETY: equality compares pointer values only; neither operand's borrowed storage is accessed.
					return string.Equals (SourceJniType, other.SourceJniType) &&
						string.Equals (SourceJniMethodName, other.SourceJniMethodName) &&
						string.Equals (SourceJniMethodSignature, other.SourceJniMethodSignature) &&
						string.Equals (TargetJniType, other.TargetJniType) &&
						string.Equals (TargetJniMethodName, other.TargetJniMethodName) &&
						string.Equals (TargetJniMethodSignature, other.TargetJniMethodSignature) &&
						TargetJniTypeUtf8Pointer == other.TargetJniTypeUtf8Pointer &&
						TargetJniMethodNameUtf8Pointer == other.TargetJniMethodNameUtf8Pointer &&
						TargetJniMethodSignatureUtf8Pointer == other.TargetJniMethodSignatureUtf8Pointer &&
						TargetJniMethodParameterCount == other.TargetJniMethodParameterCount &&
						TargetJniMethodInstanceToStatic == other.TargetJniMethodInstanceToStatic;
				}
			}

			public override int GetHashCode ()
			{
				unsafe {
					// SAFETY: hashing converts addresses to native integers without reading the addressed memory.
					return HashCode.Combine (
						SourceJniType,
						SourceJniMethodName,
						SourceJniMethodSignature,
						TargetJniType,
						TargetJniMethodName,
						TargetJniMethodSignature,
						HashCode.Combine (
							(IntPtr) TargetJniTypeUtf8Pointer,
							(IntPtr) TargetJniMethodNameUtf8Pointer,
							(IntPtr) TargetJniMethodSignatureUtf8Pointer,
							TargetJniMethodParameterCount,
							TargetJniMethodInstanceToStatic
						)
					);
				}
			}

			public override string ToString ()
			{
				unsafe {
					// SAFETY: diagnostics format numeric addresses only, even when the stored pointer is not readable.
					return $"{nameof (ReplacementMethodInfo)} {{ " +
						$"{nameof (SourceJniType)} = \"{SourceJniType}\"" +
						$", {nameof (SourceJniMethodName)} = \"{SourceJniMethodName}\"" +
						$", {nameof (SourceJniMethodSignature)} = \"{SourceJniMethodSignature}\"" +
						$", {nameof (TargetJniType)} = \"{TargetJniType}\"" +
						$", {nameof (TargetJniMethodName)} = \"{TargetJniMethodName}\"" +
						$", {nameof (TargetJniMethodSignature)} = \"{TargetJniMethodSignature}\"" +
						$", TargetJniTypeUtf8 = \"0x{(IntPtr) TargetJniTypeUtf8Pointer:x}\"" +
						$", TargetJniMethodNameUtf8 = \"0x{(IntPtr) TargetJniMethodNameUtf8Pointer:x}\"" +
						$", TargetJniMethodSignatureUtf8 = \"0x{(IntPtr) TargetJniMethodSignatureUtf8Pointer:x}\"" +
						$", {nameof (TargetJniMethodParameterCount)} = {TargetJniMethodParameterCount?.ToString () ?? "null"}" +
						$", {nameof (TargetJniMethodInstanceToStatic)} = {TargetJniMethodInstanceToStatic}" +
						$"}}";
				}
			}

			public static bool operator==(ReplacementMethodInfo a, ReplacementMethodInfo b) => a.Equals (b);
			public static bool operator!=(ReplacementMethodInfo a, ReplacementMethodInfo b) => !a.Equals (b);
		}

		[SuppressMessage ("Design", "CA1034:Nested types should not be visible",
			Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`; see 045b8af7.")]
		public struct ReplacementFieldInfo : IEquatable<ReplacementFieldInfo>
		{
			public  string? SourceJniType               {get; set;}
			public  string? SourceJniFieldName          {get; set;}
			public  string? SourceJniFieldSignature     {get; set;}
			public  string? TargetJniType               {get; set;}
			public  string? TargetJniFieldName          {get; set;}
			public  string? TargetJniFieldSignature     {get; set;}

			public override bool Equals (object? obj)
			{
				if (obj is ReplacementFieldInfo o) {
					return Equals (o);
				}
				return false;
			}

			public bool Equals (ReplacementFieldInfo other)
			{
				return string.Equals (SourceJniType, other.SourceJniType) &&
					string.Equals (SourceJniFieldName, other.SourceJniFieldName) &&
					string.Equals (SourceJniFieldSignature, other.SourceJniFieldSignature) &&
					string.Equals (TargetJniType, other.TargetJniType) &&
					string.Equals (TargetJniFieldName, other.TargetJniFieldName) &&
					string.Equals (TargetJniFieldSignature, other.TargetJniFieldSignature);
			}

			public override int GetHashCode ()
			{
				return (SourceJniType?.GetHashCode () ?? 0) ^
					(SourceJniFieldName?.GetHashCode () ?? 0) ^
					(SourceJniFieldSignature?.GetHashCode () ?? 0) ^
					(TargetJniType?.GetHashCode () ?? 0) ^
					(TargetJniFieldName?.GetHashCode () ?? 0) ^
					(TargetJniFieldSignature?.GetHashCode () ?? 0);
			}

			public override string ToString ()
			{
				return $"{nameof (ReplacementFieldInfo)} {{ " +
					$"{nameof (SourceJniType)} = \"{SourceJniType}\"" +
					$", {nameof (SourceJniFieldName)} = \"{SourceJniFieldName}\"" +
					$", {nameof (SourceJniFieldSignature)} = \"{SourceJniFieldSignature}\"" +
					$", {nameof (TargetJniType)} = \"{TargetJniType}\"" +
					$", {nameof (TargetJniFieldName)} = \"{TargetJniFieldName}\"" +
					$", {nameof (TargetJniFieldSignature)} = \"{TargetJniFieldSignature}\"" +
					$"}}";
			}

			public static bool operator==(ReplacementFieldInfo a, ReplacementFieldInfo b) => a.Equals (b);
			public static bool operator!=(ReplacementFieldInfo a, ReplacementFieldInfo b) => !a.Equals (b);
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

				return GetTypeSignatureCore (type);
			}

			protected virtual JniTypeSignature GetTypeSignatureCore (Type type) => default;

			// NOTE: This method needs to be kept in sync with GetTypeSignature()
			public IEnumerable<JniTypeSignature> GetTypeSignatures (Type type)
			{
				AssertValid ();

				if (type == null)
					return [];

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

			static Type? GetBuiltInType (JniTypeSignature typeSignature)
			{
				if (typeSignature.ArrayRank != 0)
					return null;
				if (!typeSignature.IsKeyword)
					return null;
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
				unsafe {
					// SAFETY: the type-manager producer retains the returned UTF-8 borrow while this wrapper copies its contents.
					GetReplacementTypeInfo (jniSimpleReference, out var replacement, out var replacementUtf8);
					return replacementUtf8 != null ? GetUtf8String (replacementUtf8) : replacement;
				}
			}

			protected virtual string? GetReplacementTypeCore (string jniSimpleReference) => null;

			/// <safety>
			/// Keep the owning runtime alive during lookup; do not race its disposal with the call.
			/// A non-null UTF-8 result is borrowed from the type manager, readable through its NUL
			/// terminator and immutable for the runtime's lifetime and every later cached read.
			/// The producer owns the storage; copying the pointer neither roots nor transfers it.
			/// Lookup failure does not release buffers retained by earlier successful lookups.
			/// </safety>
			internal unsafe void GetReplacementTypeInfo (string jniSimpleReference, out string? replacement, out byte* replacementUtf8)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));

				if (!RuntimeFeature.JniRemapping) {
					replacement = null;
					replacementUtf8 = null;
					return;
				}

				unsafe {
					// SAFETY: the virtual producer's contract retains immutable result bytes for the runtime and all cached readers.
					GetReplacementTypeInfoUnsafe (jniSimpleReference, out replacement, out replacementUtf8);
				}
				if (replacementUtf8 != null)
					replacement = null;
			}

			/// <summary>
			/// Resolves a replacement JNI type as either a managed string or stable NUL-terminated UTF-8 memory.
			/// </summary>
			/// <remarks>
			/// The default implementation dispatches to the legacy native-int hook to preserve existing
			/// custom overrides. Overrides are authoritative and must return
			/// results equivalent to <see cref="GetReplacementTypeCore(string)"/> for every reference.
			/// A non-null <paramref name="replacementUtf8"/> takes precedence over
			/// <paramref name="replacement"/>.
			/// </remarks>
			/// <safety>
			/// Overrides that return a non-null <paramref name="replacementUtf8"/> must supply readable
			/// NUL-terminated UTF-8 memory and keep it alive and unchanged for the associated runtime's
			/// lifetime and every subsequent cached metadata read, including failure diagnostics.
			/// The readable range includes the terminator. Read-only aliases may overlap, but no
			/// alias may write or free storage while it can still be read. Keep the runtime alive
			/// during calls and do not race disposal. Java.Interop neither roots nor frees this
			/// memory; failure must not invalidate buffers already returned to cached consumers.
			/// </safety>
			protected virtual unsafe void GetReplacementTypeInfoUnsafe (string jniSimpleReference, out string? replacement, out byte* replacementUtf8)
			{
				// SAFETY: preserve the released virtual slot; its override owns returned storage, and this bridge only converts the address.
#pragma warning disable CS0618 // Preserve dispatch to existing native-int overrides.
				GetReplacementTypeInfoCore (jniSimpleReference, out replacement, out var legacyUtf8);
#pragma warning restore CS0618
				replacementUtf8 = (byte*) legacyUtf8;
			}

			/// <summary>
			/// Resolves a replacement JNI type using the legacy native-int output contract.
			/// </summary>
			/// <safety>
			/// A nonzero <paramref name="replacementUtf8"/> must meet the backing-storage and lifetime
			/// requirements of <see cref="GetReplacementTypeInfoUnsafe"/>. This compatibility
			/// hook does not enforce unsafe on existing callers or overrides. The default returns
			/// no native storage; overrides remain responsible for their previously published borrows
			/// even if a later lookup throws.
			/// </safety>
			[Obsolete ("Override GetReplacementTypeInfoUnsafe and explicitly audit the borrowed storage lifetime.")]
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

				if (!RuntimeFeature.JniRemapping)
					return null;

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

				if (!RuntimeFeature.JniRemapping)
					return null;

				return GetReplacementMethodInfoCore (jniSimpleReference, jniMethodName, jniMethodSignature);
			}

			/// <safety>
			/// The non-null source type must be readable through its NUL terminator and unchanged
			/// until return or exception; no alias may write or free it during lookup. Read-only
			/// aliases may overlap. Keep the owning runtime alive and do not race its disposal.
			/// The call does not take ownership of source storage; returned target pointers carry
			/// their own runtime/cache lifetime contracts.
			/// </safety>
			internal unsafe ReplacementMethodInfo? GetReplacementMethodInfo (byte* jniSimpleReferenceUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
			{
				AssertValid ();
				if (jniSimpleReferenceUtf8 == null)
					throw new ArgumentNullException (nameof (jniSimpleReferenceUtf8));
				if (jniMethodName.IsEmpty)
					throw new ArgumentNullException (nameof (jniMethodName));
				if (jniMethodSignature.IsEmpty)
					throw new ArgumentNullException (nameof (jniMethodSignature));

				if (!RuntimeFeature.JniRemapping)
					return null;

				unsafe {
					// SAFETY: the caller retains the source bytes through dispatch; returned targets have the producer's cache lifetime.
					return GetReplacementMethodInfoUnsafe (jniSimpleReferenceUtf8, jniMethodName, jniMethodSignature);
				}
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
			/// <safety>
			/// A non-null source type must be readable through its NUL terminator and unchanged
			/// until return or exception. Read-only aliases may overlap; no alias may write or free
			/// it during lookup. Keep the runtime alive and do not race its disposal. The default
			/// implementation converts the source synchronously without retaining it. An override
			/// that publishes source storage as target metadata must also satisfy that target's
			/// runtime/cache lifetime; returning or throwing does not transfer ownership.
			/// </safety>
			protected virtual unsafe ReplacementMethodInfo? GetReplacementMethodInfoUnsafe (byte* jniSimpleReferenceUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
			{
				// SAFETY: forwarding changes only address representation; the caller's source lifetime also covers legacy override dispatch.
#pragma warning disable CS0618 // Preserve dispatch to existing native-int overrides.
				return GetReplacementMethodInfoCore ((IntPtr) jniSimpleReferenceUtf8, jniMethodName, jniMethodSignature);
#pragma warning restore CS0618
			}

			/// <summary>
			/// Resolves member remapping using the legacy native-int source type contract.
			/// </summary>
			/// <safety>
			/// A nonzero source type must be readable through its NUL terminator and immutable until
			/// return or exception; no alias may write or free it during lookup. No ownership is
			/// transferred. This compatibility hook does not enforce unsafe on existing callers or
			/// overrides; published target metadata retains its separate runtime/cache obligations.
			/// </safety>
			[Obsolete ("Use GetReplacementMethodInfoUnsafe and explicitly audit the borrowed storage lifetime.")]
			protected virtual ReplacementMethodInfo? GetReplacementMethodInfoCore (IntPtr jniSimpleReferenceUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
			{
				unsafe {
					// SAFETY: the released hook's caller retains source bytes while they are copied; virtual string dispatch is preserved.
					return GetReplacementMethodInfoCore (GetUtf8String ((byte*) jniSimpleReferenceUtf8), jniMethodName, jniMethodSignature);
				}
			}

			public ReplacementFieldInfo? GetReplacementFieldInfo (string jniSimpleReference, string jniFieldName, string jniFieldSignature)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));
				if (string.IsNullOrEmpty (jniFieldName)) {
					throw new ArgumentNullException (nameof (jniFieldName));
				}
				if (string.IsNullOrEmpty (jniFieldSignature)) {
					throw new ArgumentNullException (nameof (jniFieldSignature));
				}

				if (!RuntimeFeature.JniRemapping)
					return null;

				return GetReplacementFieldInfoCore (jniSimpleReference, jniFieldName, jniFieldSignature);
			}

			protected virtual ReplacementFieldInfo? GetReplacementFieldInfoCore (string jniSimpleReference, string jniFieldName, string jniFieldSignature) => null;

			internal ReplacementFieldInfo? GetReplacementFieldInfo (string jniSimpleReference, ReadOnlySpan<char> jniFieldName, ReadOnlySpan<char> jniFieldSignature)
			{
				AssertValid ();
				AssertSimpleReference (jniSimpleReference, nameof (jniSimpleReference));
				if (jniFieldName.IsEmpty)
					throw new ArgumentNullException (nameof (jniFieldName));
				if (jniFieldSignature.IsEmpty)
					throw new ArgumentNullException (nameof (jniFieldSignature));

				if (!RuntimeFeature.JniRemapping)
					return null;

				return GetReplacementFieldInfoCore (jniSimpleReference, jniFieldName, jniFieldSignature);
			}

			/// <summary>
			/// Resolves field remapping without requiring name and signature strings.
			/// The default implementation preserves dispatch to the string overload.
			/// </summary>
			protected virtual ReplacementFieldInfo? GetReplacementFieldInfoCore (string jniSimpleReference, ReadOnlySpan<char> jniFieldName, ReadOnlySpan<char> jniFieldSignature)
				=> GetReplacementFieldInfoCore (jniSimpleReference, jniFieldName.ToString (), jniFieldSignature.ToString ());

			// Default implementation is a no-op. Override to provide custom registration.
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
