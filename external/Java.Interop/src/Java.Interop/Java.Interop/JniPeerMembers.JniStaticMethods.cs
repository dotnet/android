#nullable enable

using System;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniStaticMethods {

		internal JniStaticMethods (JniPeerMembers members)
		{
			Members = members;
		}

		internal    readonly    JniPeerMembers              Members;

		JniValueCache<string, JniMethodInfo>? staticMethods;

		JniValueCache<string, JniMethodInfo> StaticMethods => JniValueCache<string, JniMethodInfo>.GetOrCreate (ref staticMethods, 1, 3, static value => value.StaticRedirect?.Dispose ());

		internal void Dispose ()
		{
			JniValueCache<string, JniMethodInfo>.Dispose (ref staticMethods);
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			return StaticMethods.GetOrAdd (encodedMember, static (member, methods) => {
				ReadOnlySpan<char> method, signature;
				JniPeerMembers.GetNameAndSignature (member, out method, out signature);
				return methods.GetMethodInfo (method, signature);
			}, this);
		}

		JniMethodInfo GetMethodInfo (ReadOnlySpan<char> method, ReadOnlySpan<char> signature)
		{
			var m              = (JniMethodInfo?) null;
			var newMethod      = JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (Members.JniPeerTypeName, method, signature);
			if (newMethod.HasValue) {
				JniType? t = new JniType (newMethod.Value.TargetJniType ?? Members.JniPeerTypeName);
				try {
					if (t.TryGetStaticMethod (
							newMethod.Value.TargetJniMethodName is string name ? name.AsSpan () : method,
							newMethod.Value.TargetJniMethodSignature is string sig ? sig.AsSpan () : signature,
							out m)) {
						if (!JniEnvironment.Types.IsSameObject (t.PeerReference, Members.JniPeerType.PeerReference)) {
							m.StaticRedirect = t;
							t = null;
						}
						return m;
					}
				} finally {
					t?.Dispose ();
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

		JniMethodInfo? FindInFallbackTypes (ReadOnlySpan<char> method, ReadOnlySpan<char> signature)
		{
			var fallbackTypes  = JniEnvironment.Runtime.TypeManager.GetStaticMethodFallbackTypes (Members.JniPeerTypeName);
			if (fallbackTypes == null) {
				return null;
			}
			JniType? t = null;
			try {
				JniMethodInfo? m = null;
				foreach (var ft in fallbackTypes) {
					if (!JniType.TryParse (ft, out t)) {
						continue;
					}
					if (t.TryGetStaticMethod (method, signature, out m)) {
						break;
					}
					t.Dispose ();
					t = null;
				}
				if (m != null) {
					// Transfer ownership only after the fallback enumerator has been disposed.
					m.StaticRedirect = t;
					t = null;
				}
				return m;
			} finally {
				t?.Dispose ();
			}
		}

		public unsafe void InvokeVoidMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe bool InvokeBooleanMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticBooleanMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe sbyte InvokeSByteMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticByteMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe char InvokeCharMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticCharMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe short InvokeInt16Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticShortMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe int InvokeInt32Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticIntMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe long InvokeInt64Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticLongMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe float InvokeSingleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticFloatMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe double InvokeDoubleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticDoubleMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}

		public unsafe JniObjectReference InvokeObjectMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
		}
	}}
}
