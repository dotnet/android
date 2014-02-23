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
			string methodAndSignature   = method + signature;
			lock (InstanceMethods) {
				JniInstanceMethodID m;
				if (!InstanceMethods.TryGetValue (methodAndSignature, out m)) {
					m = JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (methodAndSignature, m);
				}
				return m;
			}
		}

		public JniInstanceMethodID GetInstanceMethodID (string method, string signature, string methodAndSignature)
		{
			lock (InstanceMethods) {
				JniInstanceMethodID m;
				if (!InstanceMethods.TryGetValue (methodAndSignature, out m)) {
					m = JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (methodAndSignature, m);
				}
				return m;
			}
		}

		public JniInstanceFieldID GetInstanceFieldID (string field, string signature)
		{
			lock (InstanceFields) {
				JniInstanceFieldID f;
				if (!InstanceFields.TryGetValue (field, out f)) {
					f = JniPeerType.GetInstanceField (field, signature);
					InstanceFields.Add (field, f);
				}
				return f;
			}
		}

		public void CallInstanceVoidMethod (
				string method,
				string signature,
				string methodAndSignature,
				IJavaObject self)
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (method, signature, methodAndSignature);
			if (self.GetType () == ManagedPeerType)
				m.CallVirtualVoidMethod (self.SafeHandle);
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (method, signature, methodAndSignature);
				m.CallNonvirtualVoidMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		internal static void AssertSelf (IJavaObject self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (self.SafeHandle == null || self.SafeHandle.IsInvalid)
				throw new ObjectDisposedException (self.GetType ().FullName);
		}

		public int CallInstanceInt32Method (
				string method,
				string signature,
				string methodAndSignature,
				IJavaObject self)
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (method, signature, methodAndSignature);
			if (self.GetType () == ManagedPeerType) {
				return m.CallVirtualInt32Method (self.SafeHandle);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (method, signature, methodAndSignature);
				return m.CallNonvirtualInt32Method (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public JniLocalReference CallInstanceObjectMethod (
				string method,
				string signature,
				string methodAndSignature,
				IJavaObject self)
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (method, signature, methodAndSignature);
			if (self.GetType () == ManagedPeerType) {
				return m.CallVirtualObjectMethod (self.SafeHandle);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (method, signature, methodAndSignature);
				return m.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public T CallInstanceObjectMethod<T> (
				string method,
				string signature,
				string methodAndSignature,
				IJavaObject self)
			where T : IJavaObject
		{
			AssertSelf (self);
			var m = GetInstanceMethodID (method, signature, methodAndSignature);
			var e = JniEnvironment.Current.JavaVM;
			if (self.GetType () == ManagedPeerType) {
				var lref = m.CallVirtualObjectMethod (self.SafeHandle);
				return e.GetObject<T> (lref, JniHandleOwnership.Transfer);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.GetInstanceMethodID (method, signature, methodAndSignature);
				var lref = m.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
				return e.GetObject<T> (lref, JniHandleOwnership.Transfer);
			}
		}

		public bool GetBooleanInstanceFieldValue (IJavaObject self, string name)
		{
			AssertSelf (self);
			return GetInstanceFieldID (name, "Z").GetBooleanValue (self.SafeHandle);
		}

		public void SetInstanceFieldValue (IJavaObject self, string name, bool value)
		{
			AssertSelf (self);
			GetInstanceFieldID (name, "Z").SetValue (self.SafeHandle, value);
		}
	}
}

