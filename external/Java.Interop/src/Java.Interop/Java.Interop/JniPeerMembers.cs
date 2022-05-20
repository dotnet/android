#nullable enable

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

namespace Java.Interop {

	public partial class JniPeerMembers {

		private bool isInterface;

		public JniPeerMembers (string jniPeerTypeName, Type managedPeerType, bool isInterface)
			: this (jniPeerTypeName, managedPeerType, checkManagedPeerType: true, isInterface: isInterface)
		{
		}

		public JniPeerMembers (string jniPeerTypeName, Type managedPeerType)
			: this (jniPeerTypeName = GetReplacementType (jniPeerTypeName), managedPeerType, checkManagedPeerType: true, isInterface: false)
		{
		}

		static string GetReplacementType (string jniPeerTypeName)
		{
#if NET
			var replacement = JniEnvironment.Runtime.TypeManager.GetReplacementType (jniPeerTypeName);
			if (replacement != null)
				return replacement;
#endif  // NET
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
			return new JniPeerMembers (jniPeerTypeName, managedPeerType, checkManagedPeerType: false);
		}

		JniType?            jniPeerType;
		JniInstanceMethods  instanceMethods;
		JniInstanceFields   instanceFields;
		JniStaticMethods    staticMethods;
		JniStaticFields     staticFields;

		public      Type        ManagedPeerType {get; private set;}
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

		internal static void AssertSelf (IJavaPeerable self)
		{
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var peer    = self.PeerReference;
			if (!peer.IsValid)
				throw new ObjectDisposedException (self.GetType ().FullName);

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			var lref    = peer.SafeHandle as JniLocalReference;
			if (lref != null && !JniEnvironment.IsHandleValid (lref)) {
				var t = self.GetType ().FullName;
				throw new NotSupportedException (
						"You've created a " + t + " in one thread and are using it " +
						"from another thread without calling IJavaPeerable.Register(). " +
						"Passing JNI local references between threads is not supported; " +
						"call IJavaObject.RegisterWithVM() if sharing between threads is required.");
			}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
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

