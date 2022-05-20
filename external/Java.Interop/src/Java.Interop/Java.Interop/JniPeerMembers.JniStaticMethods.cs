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
				if (StaticMethods.TryGetValue (encodedMember, out var m)) {
					return m;
				}
			}
			string method, signature;
			JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
			var info = GetMethodInfo (method, signature);
			lock (StaticMethods) {
				if (StaticMethods.TryGetValue (encodedMember, out var m)) {
					return m;
				}
				StaticMethods.Add (encodedMember, info);
			}
			return info;
		}

		JniMethodInfo GetMethodInfo (string method, string signature)
		{
#if NET
			var m              = (JniMethodInfo?) null;
			var newMethod      = JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (Members.JniPeerTypeName, method, signature);
			if (newMethod.HasValue) {
				using var t = new JniType (newMethod.Value.TargetJniType ?? Members.JniPeerTypeName);
				if (t.TryGetStaticMethod (
						newMethod.Value.TargetJniMethodName ?? method,
						newMethod.Value.TargetJniMethodSignature ?? signature,
						out m)) {
					return m;
				}
			}
			if (Members.JniPeerType.TryGetStaticMethod (method, signature, out m)) {
				return m;
			}
			m   = FindInFallbackTypes (method, signature);
			if (m != null) {
				return m;
			}
#endif  // NET
			return Members.JniPeerType.GetStaticMethod (method, signature);
		}

#if NET
		JniMethodInfo? FindInFallbackTypes (string method, string signature)
		{
			var fallbackTypes  = JniEnvironment.Runtime.TypeManager.GetStaticMethodFallbackTypes (Members.JniPeerTypeName);
			if (fallbackTypes == null) {
				return null;
			}
			foreach (var ft in fallbackTypes) {
				JniType? t = null;
				try {
					if (!JniType.TryParse (ft, out t)) {
						continue;
					}
					if (t.TryGetStaticMethod (method, signature, out var m)) {
						return m;
					}
				}
				finally {
					t?.Dispose ();
				}
			}
			return null;
		}
#endif  // NET

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

