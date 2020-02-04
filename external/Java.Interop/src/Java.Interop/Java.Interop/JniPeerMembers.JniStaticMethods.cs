#nullable enable

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

		Dictionary<string, JniMethodInfo>                   StaticMethods   = new Dictionary<string, JniMethodInfo>(StringComparer.Ordinal);

		internal void Dispose ()
		{
			StaticMethods.Clear ();
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			lock (StaticMethods) {
				JniMethodInfo m;
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
			JniEnvironment.StaticMethods.CallStaticVoidMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe bool InvokeBooleanMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe sbyte InvokeSByteMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticByteMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe char InvokeCharMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticCharMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe short InvokeInt16Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticShortMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe int InvokeInt32Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticIntMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe long InvokeInt64Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticLongMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe float InvokeSingleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe double InvokeDoubleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (Members.JniPeerType.PeerReference, m, parameters);
		}

		public unsafe JniObjectReference InvokeObjectMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (Members.JniPeerType.PeerReference, m, parameters);
		}
	}}
}

