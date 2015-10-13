using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerStaticMethods {

		internal JniPeerStaticMethods (JniPeerMembers members)
		{
			Members = members;
		}

		internal    readonly    JniPeerMembers              Members;

		Dictionary<string, JniStaticMethodID>               StaticMethods   = new Dictionary<string, JniStaticMethodID>();

		internal void Dispose ()
		{
			if (StaticMethods == null)
				return;

			foreach (var m in StaticMethods.Values)
				m.Dispose ();
			StaticMethods   = null;
		}

		public JniStaticMethodID GetMethodID (string encodedMember)
		{
			lock (StaticMethods) {
				JniStaticMethodID m;
				if (!StaticMethods.TryGetValue (encodedMember, out m)) {
					string method, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
					m = Members.JniPeerType.GetStaticMethod (method, signature);
					StaticMethods.Add (encodedMember, m);
				}
				return m;
			}
		}

		public unsafe void CallVoidMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			m.CallVoidMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe bool CallBooleanMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallBooleanMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe sbyte CallSByteMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallSByteMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe char CallCharMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallCharMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe short CallInt16Method (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallInt16Method (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe int CallInt32Method (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallInt32Method (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe long CallInt64Method (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallInt64Method (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe float CallSingleMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallSingleMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe double CallDoubleMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallDoubleMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe JniLocalReference CallObjectMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallObjectMethod (Members.JniPeerType.SafeHandle, parameters);
		}
	}
}

