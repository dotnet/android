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
			public JniPeerTypeNameInfo (string sourceName, string? targetName, IntPtr targetNameUtf8)
			{
				SourceName = sourceName;
				TargetName = targetName;
				TargetNameUtf8 = targetNameUtf8;
			}

			public string SourceName { get; }
			public string? TargetName { get; }
			public IntPtr TargetNameUtf8 { get; }
		}

		static JniPeerTypeNameInfo GetReplacementType (string jniPeerTypeName)
		{
			if (jniPeerTypeName == null)
				throw new ArgumentNullException (nameof (jniPeerTypeName));
			var typeManager = JniEnvironment.Runtime.TypeManager;
			typeManager.GetReplacementTypeInfo (jniPeerTypeName, out var replacement, out var replacementUtf8);
			return new JniPeerTypeNameInfo (jniPeerTypeName, replacement, replacementUtf8);
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
			jniPeerTypeNameUtf8 = jniPeerTypeName.TargetNameUtf8;
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
		IntPtr               jniPeerTypeNameUtf8;
		JniInstanceMethods  instanceMethods;
		JniInstanceFields   instanceFields;
		JniStaticMethods    staticMethods;
		JniStaticFields     staticFields;

		public      Type        ManagedPeerType {get; private set;}
		public      string      JniPeerTypeName => jniPeerTypeNameUtf8 == IntPtr.Zero
			? jniPeerTypeName ?? sourceJniPeerTypeName
			: jniPeerTypeName ??= GetUtf8String (jniPeerTypeNameUtf8);
		public      JniType     JniPeerType {
			get {
				var t = jniPeerTypeNameUtf8 == IntPtr.Zero
					? JniType.GetCachedJniType (ref jniPeerType, jniPeerTypeName ?? sourceJniPeerTypeName)
					: JniType.GetCachedJniType (ref jniPeerType, jniPeerTypeNameUtf8);
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
			if (!disposing || jniPeerType == null)
				return;

			instanceMethods.Dispose ();
			instanceFields.Dispose ();
			staticMethods.Dispose ();
			staticFields.Dispose ();
			jniPeerType.Dispose ();

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
			return jniPeerTypeNameUtf8 == IntPtr.Zero
				? JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (jniPeerTypeName ?? sourceJniPeerTypeName, method, signature)
				: JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (jniPeerTypeNameUtf8, method, signature);
		}

		static JniType CreateTargetType (JniRuntime.ReplacementMethodInfo info, JniPeerMembers fallback)
		{
			return CreateTargetType (info, fallback.JniPeerTypeName);
		}

		static JniType CreateTargetType (JniRuntime.ReplacementMethodInfo info, string fallbackTypeName)
		{
			if (info.TargetJniTypeUtf8 != IntPtr.Zero)
				return new JniType (info.TargetJniTypeUtf8);
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
			if (info.TargetJniMethodNameUtf8 != IntPtr.Zero) {
				if (info.TargetJniMethodSignatureUtf8 != IntPtr.Zero)
					return type.TryGetInstanceMethod (info.TargetJniMethodNameUtf8, info.TargetJniMethodSignatureUtf8, out method);
				var signature = info.TargetJniMethodSignature is string targetSignature ? targetSignature.AsSpan () : fallbackSignature;
				return type.TryGetInstanceMethod (info.TargetJniMethodNameUtf8, signature, out method);
			}

			var name = info.TargetJniMethodName is string targetName ? targetName.AsSpan () : fallbackName;
			if (info.TargetJniMethodSignatureUtf8 != IntPtr.Zero)
				return type.TryGetInstanceMethod (name, info.TargetJniMethodSignatureUtf8, out method);
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
			if (info.TargetJniMethodNameUtf8 != IntPtr.Zero) {
				if (info.TargetJniMethodSignatureUtf8 != IntPtr.Zero)
					return type.TryGetStaticMethod (info.TargetJniMethodNameUtf8, info.TargetJniMethodSignatureUtf8, out method);
				var signature = info.TargetJniMethodSignature is string targetSignature ? targetSignature.AsSpan () : fallbackSignature;
				return type.TryGetStaticMethod (info.TargetJniMethodNameUtf8, signature, out method);
			}

			var name = info.TargetJniMethodName is string targetName ? targetName.AsSpan () : fallbackName;
			if (info.TargetJniMethodSignatureUtf8 != IntPtr.Zero)
				return type.TryGetStaticMethod (name, info.TargetJniMethodSignatureUtf8, out method);
			var fallback = info.TargetJniMethodSignature is string targetSignatureValue ? targetSignatureValue.AsSpan () : fallbackSignature;
			return type.TryGetStaticMethod (name, fallback, out method);
		}

		static unsafe string GetUtf8String (IntPtr value)
		{
			return System.Text.Encoding.UTF8.GetString (MemoryMarshal.CreateReadOnlySpanFromNullTerminated ((byte*)value));
		}

		static string GetTargetTypeNameForDiagnostics (JniRuntime.ReplacementMethodInfo info, JniPeerMembers fallback)
		{
			if (info.TargetJniTypeUtf8 != IntPtr.Zero)
				return GetUtf8String (info.TargetJniTypeUtf8);
			if (info.TargetJniType != null)
				return info.TargetJniType;
			return fallback.JniPeerTypeName;
		}

		static string GetTargetMethodNameForDiagnostics (JniRuntime.ReplacementMethodInfo info, ReadOnlySpan<char> fallback)
		{
			if (info.TargetJniMethodNameUtf8 != IntPtr.Zero)
				return GetUtf8String (info.TargetJniMethodNameUtf8);
			if (info.TargetJniMethodName != null)
				return info.TargetJniMethodName;
			return fallback.ToString ();
		}

		static string GetTargetMethodSignatureForDiagnostics (JniRuntime.ReplacementMethodInfo info, ReadOnlySpan<char> fallback)
		{
			if (info.TargetJniMethodSignatureUtf8 != IntPtr.Zero)
				return GetUtf8String (info.TargetJniMethodSignatureUtf8);
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

		internal static JniRuntime.ReplacementMethodInfo? GetBaseReplacementMethodInfo (
			Type managedPeerType,
			ReadOnlySpan<char> method,
			ReadOnlySpan<char> signature)
		{
			var typeManager = JniEnvironment.Runtime.TypeManager;
			for (Type? baseType = managedPeerType.BaseType; baseType != null; baseType = baseType.BaseType) {
				var baseSignature = typeManager.GetTypeSignature (baseType);
				string? effectiveBaseType = baseSignature.SimpleReference;
				if (effectiveBaseType == null) {
					continue;
				}
				effectiveBaseType = typeManager.GetReplacementType (effectiveBaseType) ?? effectiveBaseType;
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
				var baseSignature = typeManager.GetTypeSignature (baseType);
				string? effectiveBaseType = baseSignature.SimpleReference;
				if (effectiveBaseType == null) {
					continue;
				}
				effectiveBaseType = typeManager.GetReplacementType (effectiveBaseType) ?? effectiveBaseType;
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
