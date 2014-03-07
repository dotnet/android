using System;
using System.Collections.Generic;

namespace Java.Interop {

	public sealed partial class JniPeerMembers {

		public JniPeerMembers (string jniPeerType, Type managedPeerType)
		{
			if (jniPeerType == null)
				throw new ArgumentNullException ("jniPeerType");
			if (managedPeerType == null)
				throw new ArgumentNullException ("managedPeerType");
			if (!typeof (IJavaObject).IsAssignableFrom (managedPeerType))
				throw new ArgumentException ("'managedPeerType' must implement the IJavaObject interface.", "managedPeerType");

			JniPeerTypeName = jniPeerType;
			ManagedPeerType = managedPeerType;
			instanceMethods = new JniPeerInstanceMethods (this);
			instanceFields  = new JniPeerInstanceFields (this);
		}

		JniType     jniPeerType;

		readonly    JniPeerInstanceMethods  instanceMethods;
		readonly    JniPeerInstanceFields   instanceFields;

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
			get {return instanceMethods;}
		}

		public  JniPeerInstanceFields   InstanceFields {
			get {return instanceFields;}
		}

		internal static void AssertSelf (IJavaObject self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (self.SafeHandle == null || self.SafeHandle.IsInvalid)
				throw new ObjectDisposedException (self.GetType ().FullName);
			if (self.SafeHandle.ReferenceType == JniReferenceType.Invalid) {
				var t = self.GetType ().FullName;
				throw new NotSupportedException (
						"You've created a " + t + " in one thread and are using it " +
						"from another thread without calling IJavaObject.Register(). " +
						"Passing JNI local references between threads is not supported; " +
						"call IJavaObject.RegisterWithVM() if sharing between threads is required.");
			}
		}

		internal static void GetNameAndSignature (string encodedMember, out string name, out string signature)
		{
			if (encodedMember == null)
				throw new ArgumentNullException ("encodedMember");
			int n = encodedMember.IndexOf ('\u0000');
			if (n < 0)
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' should be encoded as \"<NAME>\\u0000<SIGNATURE>\".",
						"encodedMember");
			name        = encodedMember.Substring (0, n);
			signature   = encodedMember.Substring (n + 1);
		}
	}
}

