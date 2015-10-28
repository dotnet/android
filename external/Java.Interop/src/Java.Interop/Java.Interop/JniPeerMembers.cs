using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Java.Interop {

	public sealed partial class JniPeerMembers {

		public JniPeerMembers (string jniPeerType, Type managedPeerType)
			: this (jniPeerType, managedPeerType, checkManagedPeerType: true)
		{
			if (managedPeerType == null)
				throw new ArgumentNullException ("managedPeerType");
			if (!typeof (IJavaPeerable).IsAssignableFrom (managedPeerType))
				throw new ArgumentException ("'managedPeerType' must implement the IJavaPeerable interface.", "managedPeerType");

#if !XA_INTEGRATION
			Debug.Assert (
					JniEnvironment.Runtime.GetJniTypeInfoForType (managedPeerType).SimpleReference == jniPeerType,
					string.Format ("ManagedPeerType <=> JniTypeName Mismatch! javaVM.GetJniTypeInfoForType(typeof({0})).JniTypeName=\"{1}\" != \"{2}\"",
						managedPeerType.FullName,
						JniEnvironment.Runtime.GetJniTypeInfoForType (managedPeerType).SimpleReference,
						jniPeerType));
#endif  // !XA_INTEGRATION

			ManagedPeerType = managedPeerType;
		}

		JniPeerMembers (string jniPeerType, Type managedPeerType, bool checkManagedPeerType)
		{
			if (jniPeerType == null)
				throw new ArgumentNullException ("jniPeerType");

			if (checkManagedPeerType) {
				if (managedPeerType == null)
					throw new ArgumentNullException ("managedPeerType");
				if (!typeof (IJavaPeerable).IsAssignableFrom (managedPeerType))
					throw new ArgumentException ("'managedPeerType' must implement the IJavaPeerable interface.", "managedPeerType");

#if !XA_INTEGRATION
				Debug.Assert (
					JniEnvironment.Runtime.GetJniTypeInfoForType (managedPeerType).SimpleReference == jniPeerType,
					string.Format ("ManagedPeerType <=> JniTypeName Mismatch! javaVM.GetJniTypeInfoForType(typeof({0})).JniTypeName=\"{1}\" != \"{2}\"",
						managedPeerType.FullName,
						JniEnvironment.Runtime.GetJniTypeInfoForType (managedPeerType).SimpleReference,
						jniPeerType));
#endif  // !XA_INTEGRATION
			}

			JniPeerTypeName = jniPeerType;
			ManagedPeerType = managedPeerType;

			instanceMethods = new JniPeerInstanceMethods (this);
			instanceFields  = new JniPeerInstanceFields (this);
			staticMethods   = new JniPeerStaticMethods (this);
			staticFields    = new JniPeerStaticFields (this);
		}

		static JniPeerMembers CreatePeerMembers (string jniPeerType, Type managedPeerType)
		{
			return new JniPeerMembers (jniPeerType, managedPeerType, checkManagedPeerType: false);
		}

		JniType     jniPeerType;

		JniPeerInstanceMethods  instanceMethods;
		JniPeerInstanceFields   instanceFields;
		JniPeerStaticMethods    staticMethods;
		JniPeerStaticFields     staticFields;

		public      Type        ManagedPeerType {get; private set;}
		public      string      JniPeerTypeName {get; private set;}
		public      JniType     JniPeerType {
			get {
				var t = JniType.GetCachedJniType (ref jniPeerType, JniPeerTypeName);
				t.RegisterWithVM ();
				return t;
			}
		}

		public  JniPeerInstanceMethods  InstanceMethods {
			get {return Assert (instanceMethods);}
		}

		public  JniPeerInstanceFields   InstanceFields {
			get {return Assert (instanceFields);}
		}

		public  JniPeerStaticMethods    StaticMethods {
			get {return Assert (staticMethods);}
		}

		public  JniPeerStaticFields     StaticFields {
			get {return Assert (staticFields);}
		}

		static T Assert<T>(T value)
			where T : class
		{
			if (value == null)
				throw new ObjectDisposedException (nameof (JniPeerMembers));
			return value;
		}

		public static void Dispose (JniPeerMembers members)
		{
			if (members.jniPeerType == null)
				return;

			members.instanceMethods.Dispose ();
			members.instanceMethods = null;

			members.instanceFields.Dispose ();
			members.instanceFields  = null;

			members.staticMethods.Dispose ();
			members.staticMethods   = null;

			members.staticFields.Dispose ();
			members.staticFields    = null;

			members.jniPeerType.Dispose ();
			members.jniPeerType     = null;
		}

		internal static void AssertSelf (IJavaPeerable self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

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
				throw new ArgumentNullException ("encodedMember");
			int n = encodedMember.IndexOf ('\u0000');
			if (n < 0)
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' should be encoded as \"<NAME>\\u0000<SIGNATURE>\".",
						"encodedMember");
			if (encodedMember.Length <= (n+1))
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' is missing a JNI signature, and should be in the format \"<NAME>\\u0000<SIGNATURE>\".",
						"encodedMember");
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

