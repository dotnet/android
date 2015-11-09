using System;
using System.Collections.Generic;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniStaticMethods {

		internal JniStaticMethods (JniPeerMembers members)
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

		public unsafe void InvokeVoidMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			m.InvokeVoidMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe bool InvokeBooleanMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeBooleanMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe sbyte InvokeSByteMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeSByteMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe char InvokeCharMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeCharMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe short InvokeInt16Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeInt16Method (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe int InvokeInt32Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeInt32Method (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe long InvokeInt64Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeInt64Method (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe float InvokeSingleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeSingleMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe double InvokeDoubleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeDoubleMethod (Members.JniPeerType.PeerReference, parameters);
		}

		public unsafe JniObjectReference InvokeObjectMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return m.InvokeObjectMethod (Members.JniPeerType.PeerReference, parameters);
		}
	}}
}

