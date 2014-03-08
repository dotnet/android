using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerInstanceMethods
	{
		internal JniPeerInstanceMethods (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;
		readonly Dictionary<string, JniInstanceMethodID>    InstanceMethods = new Dictionary<string, JniInstanceMethodID>();

		public JniInstanceMethodID GetConstructor (string signature)
		{
			string method   = "<init>";
			lock (InstanceMethods) {
				JniInstanceMethodID m;
				if (!InstanceMethods.TryGetValue (signature, out m)) {
					m = Members.JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (signature, m);
				}
				return m;
			}
		}

		public JniInstanceMethodID GetMethodID (string encodedMember)
		{
			lock (InstanceMethods) {
				JniInstanceMethodID m;
				if (!InstanceMethods.TryGetValue (encodedMember, out m)) {
					string method, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
					m = Members.JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (encodedMember, m);
				}
				return m;
			}
		}

		public void CallVoidMethod (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);
			var m = GetMethodID (encodedMember);
			if (self.GetType () == Members.ManagedPeerType)
				m.CallVirtualVoidMethod (self.SafeHandle);
			else {
				var j = self.JniPeerMembers;
				m = j.InstanceMethods.GetMethodID (encodedMember);
				m.CallNonvirtualVoidMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public int CallInt32Method (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);
			var m = GetMethodID (encodedMember);
			if (self.GetType () == Members.ManagedPeerType) {
				return m.CallVirtualInt32Method (self.SafeHandle);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.InstanceMethods.GetMethodID (encodedMember);
				return m.CallNonvirtualInt32Method (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public JniLocalReference CallObjectMethod (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);
			var m = GetMethodID (encodedMember);
			if (self.GetType () == Members.ManagedPeerType) {
				return m.CallVirtualObjectMethod (self.SafeHandle);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.InstanceMethods.GetMethodID (encodedMember);
				return m.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
			}
		}

		public T CallObjectMethod<T> (
			string encodedMember,
			IJavaObject self)
			where T : IJavaObject
		{
			JniPeerMembers.AssertSelf (self);
			var m = GetMethodID (encodedMember);
			var e = JniEnvironment.Current.JavaVM;
			if (self.GetType () == Members.ManagedPeerType) {
				var lref = m.CallVirtualObjectMethod (self.SafeHandle);
				return e.GetObject<T> (lref, JniHandleOwnership.Transfer);
			}
			else {
				var j = self.JniPeerMembers;
				m = j.InstanceMethods.GetMethodID (encodedMember);
				var lref = m.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle);
				return e.GetObject<T> (lref, JniHandleOwnership.Transfer);
			}
		}

		public int CallInt32Method<T> (
			string encodedMember,
			IJavaObject self,
			T argument)
		{
			JniPeerMembers.AssertSelf (self);

			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T> (argument);

			var args = new [] {
				arg.JValue,
			};

			var m = GetMethodID (encodedMember);
			try {
				if (self.GetType () == Members.ManagedPeerType) {
					return m.CallVirtualInt32Method (self.SafeHandle, args);
				}
				else {
					var j = self.JniPeerMembers;
					m = j.InstanceMethods.GetMethodID (encodedMember);
					return m.CallNonvirtualInt32Method (self.SafeHandle, j.JniPeerType.SafeHandle, args);
				}
			} finally {
				arg.Cleanup (argument);
			}
		}
	}

	struct JniArgumentMarshalInfo<T> {
		JValue                          jvalue;
		JniLocalReference               lref;
		IJavaObject                     obj;
		Action<IJavaObject, object>     cleanup;

		internal JniArgumentMarshalInfo (T value)
		{
			this        = new JniArgumentMarshalInfo<T> ();
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniMarshalInfoForType (typeof (T));
			if (info.CreateMarshalCollection != null) {
				obj     = info.CreateMarshalCollection (value);
				jvalue  = new JValue (obj);
			} else if (info.CreateLocalRef != null) {
				lref    = info.CreateLocalRef (value);
				jvalue  = new JValue (lref);
			} else if (info.CreateJValue != null) {
				jvalue  = info.CreateJValue (value);
			} else
				throw new NotSupportedException ("Don't know how to get a JValue for: " + typeof (T).FullName);
			cleanup     = info.CleanupMarshalCollection;
		}

		public JValue JValue {
			get {return jvalue;}
		}

		public  void    Cleanup (object value)
		{
			if (cleanup != null && obj != null)
				cleanup (obj, value);
			if (lref != null)
				lref.Dispose ();
		}
	}
}

