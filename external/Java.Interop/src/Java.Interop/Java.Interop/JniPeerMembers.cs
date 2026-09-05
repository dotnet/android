#nullable enable

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

namespace Java.Interop {

	public partial class JniPeerMembers {

		private bool isInterface;

		public JniPeerMembers (string jniPeerTypeName, Type managedPeerType, bool isInterface)
			: this (jniPeerTypeName, GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: true, isInterface: isInterface)
		{
		}

		public JniPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: this (jniPeerTypeName, GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: true, isInterface: false)
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

		JniPeerMembers (string originalJniPeerTypeName, string jniPeerTypeName, Type managedPeerType, bool checkManagedPeerType, bool isInterface = false)
		{
			if (jniPeerTypeName == null)
				throw new ArgumentNullException (nameof (jniPeerTypeName));
			if (originalJniPeerTypeName == null)
				throw new ArgumentNullException (nameof (originalJniPeerTypeName));

			if (checkManagedPeerType) {
				if (managedPeerType == null)
					throw new ArgumentNullException (nameof (managedPeerType));
				if (!typeof (IJavaPeerable).IsAssignableFrom (managedPeerType))
					throw new ArgumentException ("'managedPeerType' must implement the IJavaPeerable interface.", nameof (managedPeerType));

#if DEBUG
				// The managed type still declares its *original* JNI name, so compare against that
				// and not against the (possibly remapped) name used to look the type up.
				var signatureFromType   = JniEnvironment.Runtime.TypeManager.GetTypeSignature (managedPeerType);
				if (signatureFromType.SimpleReference != originalJniPeerTypeName) {
					Debug.WriteLine ("WARNING-Java.Interop: ManagedPeerType <=> JniTypeName Mismatch! javaVM.GetJniTypeInfoForType(typeof({0})).JniTypeName=\"{1}\" != \"{2}\"",
							managedPeerType.FullName,
							signatureFromType.SimpleReference,
							jniPeerTypeName);
					Debug.WriteLine (new System.Diagnostics.StackTrace (true));
				}
#endif  // DEBUG
			}

			JniPeerTypeName = jniPeerTypeName;
			JniPeerOriginalTypeName = originalJniPeerTypeName;
			ManagedPeerType = managedPeerType;

			this.isInterface = isInterface;

			instanceMethods = new JniInstanceMethods (this);
			instanceFields  = new JniInstanceFields (this);
			staticMethods   = new JniStaticMethods (this);
			staticFields    = new JniStaticFields (this);
		}

		static JniPeerMembers CreatePeerMembers (string jniPeerTypeName, Type managedPeerType)
		{
			return new JniPeerMembers (jniPeerTypeName, GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: false);
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

		/// <summary>The JNI type name the managed peer type declares. Member replacements are keyed
		/// by it, because the mapping describes the original names.</summary>
		internal    string      JniPeerOriginalTypeName {get; private set;}
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

		//
		// Member replacements are described in terms of the JNI names the managed code declares, so
		// `sourceJniTypeName` is the natural key. Remapping inputs which predate type renaming being
		// applied to member entries - the Intune/MAM mapping - instead key them by the replaced
		// name, so that is tried as well.
		//
		internal static JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfo (
			string sourceJniTypeName,
			string effectiveJniTypeName,
			Type managedPeerType,
			string method,
			string signature,
			bool searchBaseTypes = true)
		{
			var typeManager = JniEnvironment.Runtime.TypeManager;
			var info        = typeManager.GetReplacementMethodInfo (sourceJniTypeName, method, signature);
			if (info == null && !string.Equals (sourceJniTypeName, effectiveJniTypeName, StringComparison.Ordinal)) {
				info    = typeManager.GetReplacementMethodInfo (effectiveJniTypeName, method, signature);
			}
			if (info == null && searchBaseTypes) {
				for (Type? baseType = managedPeerType.BaseType; baseType != null; baseType = baseType.BaseType) {
					var baseSignature = typeManager.GetTypeSignature (baseType);
					string? effectiveBaseType = baseSignature.SimpleReference;
					if (effectiveBaseType == null) {
						continue;
					}
					string sourceBaseType = typeManager.GetOriginalType (effectiveBaseType) ?? effectiveBaseType;
					info = typeManager.GetReplacementMethodInfo (sourceBaseType, method, signature);
					if (info == null && !string.Equals (sourceBaseType, effectiveBaseType, StringComparison.Ordinal)) {
						info = typeManager.GetReplacementMethodInfo (effectiveBaseType, method, signature);
					}
					if (info != null) {
						break;
					}
				}
			}
			return info;
		}

		internal static JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfo (
			string sourceJniTypeName,
			string effectiveJniTypeName,
			Type managedPeerType,
			string field,
			string signature)
		{
			var typeManager = JniEnvironment.Runtime.TypeManager;
			var info        = typeManager.GetReplacementFieldInfo (sourceJniTypeName, field, signature);
			if (info == null && !string.Equals (sourceJniTypeName, effectiveJniTypeName, StringComparison.Ordinal)) {
				info    = typeManager.GetReplacementFieldInfo (effectiveJniTypeName, field, signature);
			}
			if (info == null) {
				for (Type? baseType = managedPeerType.BaseType; baseType != null; baseType = baseType.BaseType) {
					var baseSignature = typeManager.GetTypeSignature (baseType);
					string? effectiveBaseType = baseSignature.SimpleReference;
					if (effectiveBaseType == null) {
						continue;
					}
					string sourceBaseType = typeManager.GetOriginalType (effectiveBaseType) ?? effectiveBaseType;
					info = typeManager.GetReplacementFieldInfo (sourceBaseType, field, signature);
					if (info == null && !string.Equals (sourceBaseType, effectiveBaseType, StringComparison.Ordinal)) {
						info = typeManager.GetReplacementFieldInfo (effectiveBaseType, field, signature);
					}
					if (info != null) {
						break;
					}
				}
			}
			return info;
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

		internal static void GetNameAndSignature (string encodedMember, out string name, out string signature)
		{
			int n       = GetSignatureSeparatorIndex (encodedMember);
			name        = encodedMember.Substring (0, n);
			signature   = encodedMember.Substring (n + 1);
		}
	}
}
