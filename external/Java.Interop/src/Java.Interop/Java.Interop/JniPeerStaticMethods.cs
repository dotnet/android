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

		Dictionary<string, JniStaticMethodInfo>             StaticMethods   = new Dictionary<string, JniStaticMethodInfo>();

		internal void Dispose ()
		{
			if (StaticMethods == null)
				return;

			StaticMethods   = null;
		}

		public JniStaticMethodInfo GetMethodInfo (string encodedMember)
		{
			lock (StaticMethods) {
				JniStaticMethodInfo m;
				if (!StaticMethods.TryGetValue (encodedMember, out m)) {
					string method, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
					m = Members.JniPeerType.GetStaticMethod (method, signature);
					StaticMethods.Add (encodedMember, m);
				}
				return m;
			}
		}

		public unsafe void InvokeVoidMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			m.CallVoidMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe bool InvokeBooleanMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallBooleanMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe sbyte InvokeSByteMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallSByteMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe char InvokeCharMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallCharMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe short InvokeInt16Method (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallInt16Method (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe int InvokeInt32Method (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallInt32Method (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe long InvokeInt64Method (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallInt64Method (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe float InvokeSingleMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallSingleMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe double InvokeDoubleMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallDoubleMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe JniObjectReference InvokeObjectMethod (string encodedMember, JValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.CallObjectMethod (Members.JniPeerType.PeerReference, parameters);
		}
	}
}

