#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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

		static string GetReplacementType (string jniPeerTypeName)
		{
			if (jniPeerTypeName == null)
				throw new ArgumentNullException (nameof (jniPeerTypeName));
			var replacement = JniEnvironment.Runtime.TypeManager.GetReplacementType (jniPeerTypeName);
			if (replacement != null)
				return replacement;
			return jniPeerTypeName;
		}

		JniPeerMembers (string jniPeerTypeName, Type managedPeerType, bool checkManagedPeerType, bool isInterface = false)
		{
			if (jniPeerTypeName == null)
				throw new ArgumentNullException (nameof (jniPeerTypeName));

			if (checkManagedPeerType) {
				if (managedPeerType == null)
					throw new ArgumentNullException (nameof (managedPeerType));
				if (!typeof (IJavaPeerable).IsAssignableFrom (managedPeerType))
					throw new ArgumentException ("'managedPeerType' must implement the IJavaPeerable interface.", nameof (managedPeerType));

#if DEBUG
				var signatureFromType   = JniEnvironment.Runtime.TypeManager.GetTypeSignature (managedPeerType);
				if (signatureFromType.SimpleReference != jniPeerTypeName) {
					Debug.WriteLine ("WARNING-Java.Interop: ManagedPeerType <=> JniTypeName Mismatch! javaVM.GetJniTypeInfoForType(typeof({0})).JniTypeName=\"{1}\" != \"{2}\"",
							managedPeerType.FullName,
							signatureFromType.SimpleReference,
							jniPeerTypeName);
					Debug.WriteLine (new System.Diagnostics.StackTrace (true));
				}
#endif  // DEBUG
			}

			JniPeerTypeName = jniPeerTypeName;
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
		JniInstanceMethods  instanceMethods;
		JniInstanceFields   instanceFields;
		JniStaticMethods    staticMethods;
		JniStaticFields     staticFields;

		public      Type        ManagedPeerType {get; private set;}

		/// <summary>The JNI type name used to look the peer type up at runtime. This is the
		/// remapped name when the type was renamed in the packaged application.</summary>
		public      string      JniPeerTypeName {get; private set;}

		public      JniType     JniPeerType {
			get {
				var t = JniType.GetCachedJniType (ref jniPeerType, JniPeerTypeName);
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

		// Member keys use the replaced type name but retain the managed member name and signature.
		internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
			string jniTypeName,
			Type managedPeerType,
			ReadOnlySpan<char> method,
			ReadOnlySpan<char> signature,
			bool searchBaseTypes = true)
		{
			var typeManager = JniEnvironment.Runtime.TypeManager;
			var info        = typeManager.GetReplacementMethodInfo (jniTypeName, method, signature);
			if (info == null && searchBaseTypes) {
				for (Type? baseType = managedPeerType.BaseType; baseType != null; baseType = baseType.BaseType) {
					var baseSignature = typeManager.GetTypeSignature (baseType);
					string? effectiveBaseType = baseSignature.SimpleReference;
					if (effectiveBaseType == null) {
						continue;
					}
					info = typeManager.GetReplacementMethodInfo (effectiveBaseType, method, signature);
					if (info != null) {
						break;
					}
				}
			}
			return info;
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
