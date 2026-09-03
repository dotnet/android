#nullable enable

using System;
using System.Collections.Concurrent;
using System.Text;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniStaticMethods {

		internal JniStaticMethods (JniPeerMembers members)
		{
			Members = members;
		}

		internal    readonly    JniPeerMembers              Members;

		readonly ConcurrentDictionary<string, JniMethodInfo> StaticMethods = new ConcurrentDictionary<string, JniMethodInfo> (1, 3, StringComparer.Ordinal);
		readonly Utf8ValueCache<JniMethodInfo>                Utf8StaticMethods = new Utf8ValueCache<JniMethodInfo> ();

		internal void Dispose ()
		{
			StaticMethods.Clear ();
			Utf8StaticMethods.Clear ();
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			return StaticMethods.GetOrAdd (encodedMember, static (member, methods) => {
				string method, signature;
				JniPeerMembers.GetNameAndSignature (member, out method, out signature);
				return methods.GetMethodInfo (method, signature);
			}, this);
		}

		public JniMethodInfo GetMethodInfo (ReadOnlySpan<byte> encodedMember)
		{
			return Utf8StaticMethods.GetOrAdd (encodedMember, static (member, methods) => {
				int separator = JniPeerMembers.GetSignatureSeparatorIndex (member);
				return methods.GetMethodInfo (member.Slice (0, separator), member.Slice (separator + 1));
			}, this);
		}

		JniMethodInfo GetMethodInfo (ReadOnlySpan<byte> method, ReadOnlySpan<byte> signature)
		{
			Span<byte> terminatedMethod = method.Length + 1 <= 256
				? stackalloc byte [method.Length + 1]
				: new byte [method.Length + 1];
			Span<byte> terminatedSignature = signature.Length + 1 <= 512
				? stackalloc byte [signature.Length + 1]
				: new byte [signature.Length + 1];
			method.CopyTo (terminatedMethod);
			signature.CopyTo (terminatedSignature);
			terminatedMethod [method.Length]       = 0;
			terminatedSignature [signature.Length] = 0;
			var m                   = (JniMethodInfo?) null;
			var newMethod           = Members.GetReplacementMethodInfo (method, signature);
			if (newMethod.HasValue) {
				var typeName        = newMethod.Value.TargetJniType ?? Members.JniPeerTypeName;
				var replacementName = newMethod.Value.TargetJniMethodName ?? Encoding.UTF8.GetString (method);
				var replacementSig  = newMethod.Value.TargetJniMethodSignature ?? Encoding.UTF8.GetString (signature);

				using var t = new JniType (typeName);
				if (t.TryGetStaticMethod (replacementName, replacementSig, out m)) {
					return m;
				}
			}
			if (Members.JniPeerType.TryGetStaticMethod (terminatedMethod, terminatedSignature, out m))
				return m;
			var methodName = Encoding.UTF8.GetString (method);
			var methodSig  = Encoding.UTF8.GetString (signature);
			m = FindInFallbackTypes (methodName, methodSig);
			if (m != null)
				return m;
			return Members.JniPeerType.GetStaticMethod (terminatedMethod, terminatedSignature);
		}

		JniMethodInfo GetMethodInfo (string method, string signature)
		{
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
			return Members.JniPeerType.GetStaticMethod (method, signature);
		}

#pragma warning disable CA1801
		JniType GetMethodDeclaringType (JniMethodInfo method)
		{
			if (method.StaticRedirect != null) {
				return method.StaticRedirect;
			}
			return Members.JniPeerType;
		}
#pragma warning restore CA1801

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
						m.StaticRedirect    = t;
						t                   = null;
						return m;
					}
				}
				finally {
					t?.Dispose ();
				}
			}
			return null;
		}

		public unsafe void InvokeVoidMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe void InvokeVoidMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe bool InvokeBooleanMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe bool InvokeBooleanMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe sbyte InvokeSByteMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticByteMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe sbyte InvokeSByteMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticByteMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe char InvokeCharMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticCharMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe char InvokeCharMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticCharMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe short InvokeInt16Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticShortMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe short InvokeInt16Method (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticShortMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe int InvokeInt32Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticIntMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe int InvokeInt32Method (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticIntMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe long InvokeInt64Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticLongMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe long InvokeInt64Method (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticLongMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe float InvokeSingleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe float InvokeSingleMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe double InvokeDoubleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe double InvokeDoubleMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe JniObjectReference InvokeObjectMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe JniObjectReference InvokeObjectMethod (ReadOnlySpan<byte> encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}
	}}
}
