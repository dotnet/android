#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public partial class JniPeerMembers {

		private bool isInterface;

		public JniPeerMembers (string jniPeerTypeName, Type managedPeerType, bool isInterface)
			: this (GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: true, isInterface: isInterface)
		{
		}

		public JniPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: this (GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: true, isInterface: false)
		{
		}

		readonly struct JniPeerTypeNameInfo
		{
			public JniPeerTypeNameInfo (string sourceName, string? targetName)
			{
				SourceName = sourceName;
				TargetName = targetName;
				unsafe {
					// SAFETY: the managed descriptor publishes no borrowed address.
					TargetNameUtf8 = null;
				}
			}

			/// <safety>
			/// A non-null UTF-8 target must be readable through its NUL terminator. Its owner must
			/// retain it unchanged for the runtime's lifetime and all later cached peer reads;
			/// no concurrent writer or freeing alias is permitted. Read-only aliases may overlap.
			/// This descriptor does not copy, root or free the storage, including on failure.
			/// </safety>
			public unsafe JniPeerTypeNameInfo (string sourceName, byte* targetNameUtf8)
			{
				SourceName = sourceName;
				TargetName = null;
				unsafe {
					// SAFETY: retain the address without scanning; its owner supplies the documented cached-read lifetime.
					TargetNameUtf8 = targetNameUtf8;
				}
			}

			public string SourceName { get; }
			public string? TargetName { get; }
			public unsafe byte* TargetNameUtf8 { get; }
		}

		static JniPeerTypeNameInfo GetReplacementType (string jniPeerTypeName)
		{
			if (jniPeerTypeName == null)
				throw new ArgumentNullException (nameof (jniPeerTypeName));
			var typeManager = JniEnvironment.Runtime.TypeManager;
			unsafe {
				// SAFETY: the type-manager producer supplies runtime-lived immutable storage; the descriptor preserves that borrow.
				typeManager.GetReplacementTypeInfo (jniPeerTypeName, out var replacement, out var replacementUtf8);
				return replacementUtf8 == null
					? new JniPeerTypeNameInfo (jniPeerTypeName, replacement)
					: new JniPeerTypeNameInfo (jniPeerTypeName, replacementUtf8);
			}
		}

		JniPeerMembers (JniPeerTypeNameInfo jniPeerTypeName, Type managedPeerType, bool checkManagedPeerType, bool isInterface = false)
		{
			if (jniPeerTypeName.SourceName == null)
				throw new ArgumentNullException (nameof (jniPeerTypeName));

			if (checkManagedPeerType) {
				if (managedPeerType == null)
					throw new ArgumentNullException (nameof (managedPeerType));
				if (!typeof (IJavaPeerable).IsAssignableFrom (managedPeerType))
					throw new ArgumentException ("'managedPeerType' must implement the IJavaPeerable interface.", nameof (managedPeerType));

#if DEBUG
				var signatureFromType   = JniEnvironment.Runtime.TypeManager.GetTypeSignature (managedPeerType);
				if (signatureFromType.SimpleReference != jniPeerTypeName.SourceName) {
					Debug.WriteLine ("WARNING-Java.Interop: ManagedPeerType <=> JniTypeName Mismatch! javaVM.GetJniTypeInfoForType(typeof({0})).JniTypeName=\"{1}\" != \"{2}\"",
							managedPeerType.FullName,
							signatureFromType.SimpleReference,
							jniPeerTypeName.SourceName);
					Debug.WriteLine (new System.Diagnostics.StackTrace (true));
				}
#endif  // DEBUG
			}

			sourceJniPeerTypeName = jniPeerTypeName.SourceName;
			this.jniPeerTypeName = jniPeerTypeName.TargetName;
			unsafe {
				// SAFETY: copy the immutable descriptor's address, not its contents; its owner retains it for cached use.
				jniPeerTypeNameUtf8 = jniPeerTypeName.TargetNameUtf8;
			}
			ManagedPeerType = managedPeerType;

			this.isInterface = isInterface;

			instanceMethods = new JniInstanceMethods (this);
			instanceFields  = new JniInstanceFields (this);
			staticMethods   = new JniStaticMethods (this);
			staticFields    = new JniStaticFields (this);
		}

		static JniPeerMembers CreatePeerMembers (string jniPeerTypeName, Type managedPeerType)
		{
			return new JniPeerMembers (GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: false);
		}

		JniType?            jniPeerType;
		string              sourceJniPeerTypeName;
		string?             jniPeerTypeName;
		unsafe byte*        jniPeerTypeNameUtf8;
		JniInstanceMethods  instanceMethods;
		JniInstanceFields   instanceFields;
		JniStaticMethods    staticMethods;
		JniStaticFields     staticFields;

		public      Type        ManagedPeerType {get; private set;}
		public      string      JniPeerTypeName {
			get {
				unsafe {
					// SAFETY: the descriptor's producer retains NUL-terminated storage; only the managed conversion is cached here.
					return jniPeerTypeNameUtf8 == null
						? jniPeerTypeName ?? sourceJniPeerTypeName
						: jniPeerTypeName ??= GetStringFromNullTerminated (jniPeerTypeNameUtf8);
				}
			}
		}
		public      JniType     JniPeerType {
			get {
				JniType t;
				unsafe {
					// SAFETY: the peer-name borrow outlives lookup, and GetCachedJniType does not retain the name pointer.
					t = jniPeerTypeNameUtf8 == null
						? JniType.GetCachedJniType (ref jniPeerType, jniPeerTypeName ?? sourceJniPeerTypeName)
						: JniType.GetCachedJniType (ref jniPeerType, jniPeerTypeNameUtf8);
				}
				t.RegisterWithRuntime ();
				return t;
			}
		}

		public  JniInstanceMethods  InstanceMethods {
			get {return Assert (instanceMethods);}
		}

		public  JniInstanceFields   InstanceFields {
			get {return Assert (instanceFields);}
		}

		public  JniStaticMethods    StaticMethods {
			get {return Assert (staticMethods);}
		}

		public  JniStaticFields     StaticFields {
			get {return Assert (staticFields);}
		}

		static T Assert<T>(T value)
			where T : class
		{
			if (value == null)
				throw new ObjectDisposedException (nameof (JniPeerMembers));
			return value;
		}

		static ConcurrentDictionary<TKey, TValue> GetOrCreate<TKey, TValue> (ref ConcurrentDictionary<TKey, TValue>? dictionary, int capacity)
			where TKey : notnull
		{
			var value = Volatile.Read (ref dictionary);
			if (value != null)
				return value;

			var candidate = new ConcurrentDictionary<TKey, TValue> (1, capacity);
			return Interlocked.CompareExchange (ref dictionary, candidate, null) ?? candidate;
		}

		static TValue GetOrAdd<TKey, TValue, TState> (
			ConcurrentDictionary<TKey, TValue> dictionary,
			TKey key,
			Func<TKey, TState, TValue> valueFactory,
			TState state,
			Action<TValue> dispose)
			where TKey : notnull
			where TValue : class
		{
			if (dictionary.TryGetValue (key, out var value))
				return value;

			var candidate = valueFactory (key, state);
			value = dictionary.GetOrAdd (key, candidate);
			if (!ReferenceEquals (value, candidate))
				dispose (candidate);
			return value;
		}

		static void Clear<TKey, TValue> (ref ConcurrentDictionary<TKey, TValue>? dictionary, Action<TValue>? dispose = null)
			where TKey : notnull
		{
			var values = Interlocked.Exchange (ref dictionary, null);
			if (values == null)
				return;
			if (dispose != null) {
				foreach (var value in values.Values)
					dispose (value);
			}
			values.Clear ();
		}

		protected virtual void Dispose (bool disposing)
		{
			if (!disposing)
				return;

			instanceMethods.Dispose ();
			instanceFields.Dispose ();
			staticMethods.Dispose ();
			staticFields.Dispose ();
			jniPeerType?.Dispose ();

			jniPeerType     = null;
		}

		public static void Dispose (JniPeerMembers members)
		{
			if (members == null)
				return;
			members.Dispose (true);
		}

		protected virtual bool UsesVirtualDispatch (IJavaPeerable value, Type? declaringType)
		{
			return value.GetType () == declaringType ||
				declaringType == null ||
				value.GetType () == value.JniPeerMembers.ManagedPeerType;
		}

		protected virtual JniPeerMembers GetPeerMembers (IJavaPeerable value)
		{
			return isInterface ? this : value.JniPeerMembers;
		}

		JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (ReadOnlySpan<char> method, ReadOnlySpan<char> signature)
		{
			unsafe {
				// SAFETY: the type-name producer keeps the cached source readable through this synchronous lookup.
				return jniPeerTypeNameUtf8 == null
					? JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (jniPeerTypeName ?? sourceJniPeerTypeName, method, signature)
					: JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (jniPeerTypeNameUtf8, method, signature);
			}
		}

		static JniType CreateTargetType (JniRuntime.ReplacementMethodInfo info, JniPeerMembers fallback)
		{
			return CreateTargetType (info, fallback.JniPeerTypeName);
		}

		static JniType CreateTargetType (JniRuntime.ReplacementMethodInfo info, string fallbackTypeName)
		{
			unsafe {
				// SAFETY: remapping producers retain target names through the runtime's cached uses; class lookup only borrows them.
				if (info.TargetJniTypeUtf8Pointer != null)
					return new JniType (info.TargetJniTypeUtf8Pointer);
			}
			if (info.TargetJniType != null)
				return new JniType (info.TargetJniType);
			return new JniType (fallbackTypeName);
		}

		static bool TryGetInstanceMethod (
			JniType type,
			JniRuntime.ReplacementMethodInfo info,
			ReadOnlySpan<char> fallbackName,
			ReadOnlySpan<char> fallbackSignature,
			[System.Diagnostics.CodeAnalysis.NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: remapping metadata owns no bytes; its producer keeps both borrowed strings immutable through cached reads.
				if (info.TargetJniMethodNameUtf8Pointer != null) {
					if (info.TargetJniMethodSignatureUtf8Pointer != null)
						return type.TryGetInstanceMethod (info.TargetJniMethodNameUtf8Pointer, info.TargetJniMethodSignatureUtf8Pointer, out method);
					var signature = info.TargetJniMethodSignature is string targetSignature ? targetSignature.AsSpan () : fallbackSignature;
					return type.TryGetInstanceMethod (info.TargetJniMethodNameUtf8Pointer, signature, out method);
				}
			}

			var name = info.TargetJniMethodName is string targetName ? targetName.AsSpan () : fallbackName;
			unsafe {
				// SAFETY: the signature borrow survives returned metadata; the managed name is copied in Debug.
				if (info.TargetJniMethodSignatureUtf8Pointer != null)
					return type.TryGetInstanceMethod (name, info.TargetJniMethodSignatureUtf8Pointer, out method);
			}
			var fallback = info.TargetJniMethodSignature is string targetSignatureValue ? targetSignatureValue.AsSpan () : fallbackSignature;
			return type.TryGetInstanceMethod (name, fallback, out method);
		}

		static bool TryGetStaticMethod (
			JniType type,
			JniRuntime.ReplacementMethodInfo info,
			ReadOnlySpan<char> fallbackName,
			ReadOnlySpan<char> fallbackSignature,
			[System.Diagnostics.CodeAnalysis.NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: the producer retains borrowed names/signatures through lookup and later Debug metadata reads.
				if (info.TargetJniMethodNameUtf8Pointer != null) {
					if (info.TargetJniMethodSignatureUtf8Pointer != null)
						return type.TryGetStaticMethod (info.TargetJniMethodNameUtf8Pointer, info.TargetJniMethodSignatureUtf8Pointer, out method);
					var signature = info.TargetJniMethodSignature is string targetSignature ? targetSignature.AsSpan () : fallbackSignature;
					return type.TryGetStaticMethod (info.TargetJniMethodNameUtf8Pointer, signature, out method);
				}
			}

			var name = info.TargetJniMethodName is string targetName ? targetName.AsSpan () : fallbackName;
			unsafe {
				// SAFETY: only the producer-owned signature is borrowed; its lifetime covers later cached diagnostics.
				if (info.TargetJniMethodSignatureUtf8Pointer != null)
					return type.TryGetStaticMethod (name, info.TargetJniMethodSignatureUtf8Pointer, out method);
			}
			var fallback = info.TargetJniMethodSignature is string targetSignatureValue ? targetSignatureValue.AsSpan () : fallbackSignature;
			return type.TryGetStaticMethod (name, fallback, out method);
		}

		/// <safety>
		/// The UTF-8 value must be readable through its NUL terminator and immutable until the call
		/// returns or throws. Read-only aliases may overlap; no alias may write or free it.
		/// The conversion does not retain or free the source storage.
		/// </safety>
		static unsafe string GetStringFromNullTerminated (byte* value)
		{
			unsafe {
				// SAFETY: the caller establishes readability through the terminator for this synchronous managed copy.
				return System.Text.Encoding.UTF8.GetString (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value));
			}
		}

		static string GetTargetTypeNameForDiagnostics (JniRuntime.ReplacementMethodInfo info, JniPeerMembers fallback)
		{
			unsafe {
				// SAFETY: the remapping producer retains the target buffer unchanged while diagnostics scan it.
				if (info.TargetJniTypeUtf8Pointer != null)
					return GetStringFromNullTerminated (info.TargetJniTypeUtf8Pointer);
			}
			if (info.TargetJniType != null)
				return info.TargetJniType;
			return fallback.JniPeerTypeName;
		}

		static string GetTargetMethodNameForDiagnostics (JniRuntime.ReplacementMethodInfo info, ReadOnlySpan<char> fallback)
		{
			unsafe {
				// SAFETY: the remapping producer retains the target method name unchanged through this diagnostic copy.
				if (info.TargetJniMethodNameUtf8Pointer != null)
					return GetStringFromNullTerminated (info.TargetJniMethodNameUtf8Pointer);
			}
			if (info.TargetJniMethodName != null)
				return info.TargetJniMethodName;
			return fallback.ToString ();
		}

		static string GetTargetMethodSignatureForDiagnostics (JniRuntime.ReplacementMethodInfo info, ReadOnlySpan<char> fallback)
		{
			unsafe {
				// SAFETY: the remapping producer retains the target signature unchanged through this diagnostic copy.
				if (info.TargetJniMethodSignatureUtf8Pointer != null)
					return GetStringFromNullTerminated (info.TargetJniMethodSignatureUtf8Pointer);
			}
			if (info.TargetJniMethodSignature != null)
				return info.TargetJniMethodSignature;
			return fallback.ToString ();
		}

		// Member keys use the replaced type name but retain the managed member name and signature.
		internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
			string jniTypeName,
			ReadOnlySpan<char> method,
			ReadOnlySpan<char> signature)
		{
			return JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (jniTypeName, method, signature);
		}

		static string? GetEffectiveBaseTypeName (JniRuntime.JniTypeManager typeManager, Type baseType)
		{
			var baseSignature = typeManager.GetTypeSignature (baseType);
			string? effectiveBaseType = baseSignature.SimpleReference;
			if (effectiveBaseType == null)
				return null;

			var declaredSignature = baseType.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
			if (declaredSignature == null ||
					!string.Equals (declaredSignature.SimpleReference, effectiveBaseType, StringComparison.Ordinal)) {
				return effectiveBaseType;
			}

			return typeManager.GetReplacementType (effectiveBaseType) ?? effectiveBaseType;
		}

		internal static JniRuntime.ReplacementMethodInfo? GetBaseReplacementMethodInfo (
			Type managedPeerType,
			ReadOnlySpan<char> method,
			ReadOnlySpan<char> signature)
		{
			var typeManager = JniEnvironment.Runtime.TypeManager;
			for (Type? baseType = managedPeerType.BaseType; baseType != null; baseType = baseType.BaseType) {
				string? effectiveBaseType = GetEffectiveBaseTypeName (typeManager, baseType);
				if (effectiveBaseType == null)
					continue;
				var info = typeManager.GetReplacementMethodInfo (effectiveBaseType, method, signature);
				if (info != null) {
					return info;
				}
			}
			return null;
		}

		internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (
			string jniTypeName,
			ReadOnlySpan<char> field,
			ReadOnlySpan<char> signature)
		{
			return JniEnvironment.Runtime.TypeManager.GetReplacementFieldInfo (jniTypeName, field, signature);
		}

		internal static JniRuntime.ReplacementFieldInfo? GetBaseReplacementFieldInfo (
			Type managedPeerType,
			ReadOnlySpan<char> field,
			ReadOnlySpan<char> signature)
		{
			var typeManager = JniEnvironment.Runtime.TypeManager;
			for (Type? baseType = managedPeerType.BaseType; baseType != null; baseType = baseType.BaseType) {
				string? effectiveBaseType = GetEffectiveBaseTypeName (typeManager, baseType);
				if (effectiveBaseType == null)
					continue;
				var info = typeManager.GetReplacementFieldInfo (effectiveBaseType, field, signature);
				if (info != null) {
					return info;
				}
			}
			return null;
		}

		internal static void AssertSelf (IJavaPeerable self)
		{
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var peer    = self.PeerReference;
			if (!peer.IsValid)
				throw JniEnvironment.CreateObjectDisposedException (self);

		}

		internal static int GetSignatureSeparatorIndex (string encodedMember)
		{
			if (encodedMember == null)
				throw new ArgumentNullException (nameof (encodedMember));
			int n = encodedMember.IndexOf (".", StringComparison.Ordinal);
			if (n < 0)
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' should be encoded as \"<NAME>.<SIGNATURE>\".",
						nameof (encodedMember));
			if (encodedMember.Length <= (n+1))
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' is missing a JNI signature, and should be in the format \"<NAME>.<SIGNATURE>\".",
						nameof (encodedMember));
			return n;
		}

		internal static void GetNameAndSignature (string encodedMember, out ReadOnlySpan<char> name, out ReadOnlySpan<char> signature)
		{
			int n       = GetSignatureSeparatorIndex (encodedMember);
			name        = encodedMember.AsSpan (0, n);
			signature   = encodedMember.AsSpan (n + 1);
		}
	}
}
