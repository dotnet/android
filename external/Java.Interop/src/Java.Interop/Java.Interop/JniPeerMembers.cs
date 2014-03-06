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
		}

		JniType     jniPeerType;

		readonly Dictionary<string, JniInstanceMethodID>    InstanceMethods = new Dictionary<string, JniInstanceMethodID>();
		readonly Dictionary<string, JniInstanceFieldID>     InstanceFields  = new Dictionary<string, JniInstanceFieldID>();
		readonly Dictionary<string, JniStaticMethodID>      StaticMethods   = new Dictionary<string, JniStaticMethodID>();
		readonly Dictionary<string, JniStaticFieldID>       StaticFields    = new Dictionary<string, JniStaticFieldID>();

		public      Type        ManagedPeerType {get; private set;}
		public      string      JniPeerTypeName {get; private set;}
		public      JniType     JniPeerType {
			get {
				var t = JniType.GetCachedJniType (ref jniPeerType, JniPeerTypeName);
				t.RegisterWithVM ();
				return t;
			}
		}

		public JniInstanceMethodID GetConstructor (string signature)
		{
			string method               = "<init>";
			lock (InstanceMethods) {
				JniInstanceMethodID m;
				if (!InstanceMethods.TryGetValue (signature, out m)) {
					m = JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (signature, m);
				}
				return m;
			}
		}

		public JniInstanceMethodID GetInstanceMethodID (string encodedMember)
		{
			lock (InstanceMethods) {
				JniInstanceMethodID m;
				if (!InstanceMethods.TryGetValue (encodedMember, out m)) {
					string method, signature;
					GetNameAndSignature (encodedMember, out method, out signature);
					m = JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (encodedMember, m);
				}
				return m;
			}
		}

		static void GetNameAndSignature (string encodedMember, out string name, out string signature)
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

		public JniInstanceFieldID GetInstanceFieldID (string encodedMember)
		{
			lock (InstanceFields) {
				JniInstanceFieldID f;
				if (!InstanceFields.TryGetValue (encodedMember, out f)) {
					string field, signature;
					GetNameAndSignature (encodedMember, out field, out signature);
					f = JniPeerType.GetInstanceField (field, signature);
					InstanceFields.Add (encodedMember, f);
				}
				return f;
			}
		}

		public void CallInstanceVoidMethod (
				string encodedMember,
				IJavaObject self)
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (encodedMember);
			if (self.GetType () == ManagedPeerType)
				m.CallVirtualVoidMethod (self.SafeHandle);
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (encodedMember);
				m.CallNonvirtualVoidMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
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

		public int CallInstanceInt32Method (
				string encodedMember,
				IJavaObject self)
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (encodedMember);
			if (self.GetType () == ManagedPeerType) {
				return m.CallVirtualInt32Method (self.SafeHandle);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (encodedMember);
				return m.CallNonvirtualInt32Method (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public JniLocalReference CallInstanceObjectMethod (
				string encodedMember,
				IJavaObject self)
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (encodedMember);
			if (self.GetType () == ManagedPeerType) {
				return m.CallVirtualObjectMethod (self.SafeHandle);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (encodedMember);
				return m.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public T CallInstanceObjectMethod<T> (
				string encodedMember,
				IJavaObject self)
			where T : IJavaObject
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (encodedMember);
			var e = JniEnvironment.Current.JavaVM;
			if (self.GetType () == ManagedPeerType) {
				var lref = m.CallVirtualObjectMethod (self.SafeHandle);
				return e.GetObject<T> (lref, JniHandleOwnership.Transfer);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (encodedMember);
				var lref = m.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
				return e.GetObject<T> (lref, JniHandleOwnership.Transfer);
			}
		}

		public bool GetBooleanInstanceFieldValue (IJavaObject self, string encodedMember)
		{
			AssertSelf (self);
			return GetInstanceFieldID (encodedMember).GetBooleanValue (self.SafeHandle);
		}

		public void SetInstanceFieldValue (IJavaObject self, string encodedMember, bool value)
		{
			AssertSelf (self);
			GetInstanceFieldID (encodedMember).SetValue (self.SafeHandle, value);
		}
	}
}

